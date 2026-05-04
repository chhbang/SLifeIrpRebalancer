using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using PensionCompass.Core.Models;

namespace PensionCompass.Core.Parsing;

/// <summary>
/// Parses a saved Samsung Life "적립금/수익률 조회" page (the user's own portfolio dump).
/// Extracts only what's needed to populate <see cref="OwnedProductModel"/> + account totals;
/// PII like 가입자 성명, 계좌번호, 명세일자 are intentionally NOT read.
///
/// The page renders the same data twice (once per tab — "현재 적립금" and "기간 수익률 조회").
/// Tab 1 fund cards carry 좌수 in display:none rows; Tab 2 PG cards carry 수익률·연환산·운용일수
/// that Tab 1 PG cards lack. Fields are merged per product name (first non-empty wins),
/// so a single parse pass yields the union of available data.
/// </summary>
public sealed class SamsungLifePortfolioHtmlParser
{
    // First alternative: thousands-separated form (must have at least one comma group, e.g. "30,674,752").
    // Second alternative: plain digits with optional decimal ("2294", "20.21"). The first alt must
    // require ≥1 comma group, otherwise it greedily matches only the first 3 digits of "2294".
    private static readonly Regex DigitsAndDecimal = new(@"-?\d{1,3}(?:,\d{3})+(?:\.\d+)?|-?\d+(?:\.\d+)?", RegexOptions.Compiled);

    public PortfolioSnapshot Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var (total, deposit, profit) = ExtractTotals(doc);
        var holdings = ExtractHoldings(doc);

        return new PortfolioSnapshot(total, deposit, profit, holdings);
    }

    private static (decimal? Total, decimal? Deposit, decimal? Profit) ExtractTotals(HtmlDocument doc)
    {
        // The first .prd-summary container holds account-level totals. There are two on the page
        // (one per tab) but they're identical — the first wins.
        var summary = doc.DocumentNode.SelectSingleNode("//div[contains(concat(' ', normalize-space(@class), ' '), ' prd-summary ')]");
        if (summary == null) return (null, null, null);

        // 총 적립금 lives in its own .prd-amount slot, NOT inside the labeled desc-list rows.
        var totalNode = summary.SelectSingleNode(".//div[contains(@class,'prd-amount')]//em[contains(@class,'amount-desc')]//span");
        var total = ParseDecimal(ReadText(totalNode));

        // The labeled desc-list inside the same .prd-summary holds 입금액(+) / 운용수익 / 출금액(-) etc.
        decimal? deposit = null;
        decimal? profit = null;

        var rows = summary.SelectNodes(".//li[contains(@class,'desc-item')]");
        if (rows == null) return (total, deposit, profit);

        foreach (var row in rows)
        {
            var label = NormalizeLabel(ReadText(row.SelectSingleNode(".//div[contains(@class,'tit')]/em")));
            if (string.IsNullOrEmpty(label)) continue;
            var valueText = ReadText(row.SelectSingleNode(".//div[contains(@class,'desc')]//span[last()]"));

            // 입금액(+) — the parenthesised "+" is part of the label rendered by Samsung Life.
            if (label.StartsWith("입금액", StringComparison.Ordinal) && deposit is null)
                deposit = ParseDecimal(valueText);
            else if (label == "운용수익" && profit is null)
                profit = ParseDecimal(valueText);
        }

        return (total, deposit, profit);
    }

    private static List<OwnedProductModel> ExtractHoldings(HtmlDocument doc)
    {
        // Per-product fields are accumulated across all card occurrences of a product name.
        // First non-empty value wins (so visible UI text takes precedence over a later duplicate).
        var fieldsByName = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var orderedNames = new List<string>();

        var items = doc.DocumentNode.SelectNodes("//li[contains(concat(' ', normalize-space(@class), ' '), ' data-list-item ')]");
        if (items == null) return [];

        foreach (var li in items)
        {
            // Product name comes from .desc-title strong — works for both <a class="desc-title"> and
            // <p class="desc-title"> variants (the latter is used by 삼성생명 고유대 which has no detail link).
            var name = ReadText(li.SelectSingleNode(".//*[contains(@class,'desc-title')]//strong"));
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (!fieldsByName.TryGetValue(name, out var fields))
            {
                fields = new Dictionary<string, string>(StringComparer.Ordinal);
                fieldsByName[name] = fields;
                orderedNames.Add(name);
            }

            var rows = li.SelectNodes(".//li[contains(@class,'desc-item')]");
            if (rows == null) continue;

            foreach (var row in rows)
            {
                var label = NormalizeLabel(ReadText(row.SelectSingleNode(".//div[contains(@class,'tit')]/em")));
                if (string.IsNullOrEmpty(label)) continue;
                var valueText = ReadText(row.SelectSingleNode(".//div[contains(@class,'desc')]//span[last()]"));
                if (string.IsNullOrEmpty(valueText)) continue;
                fields.TryAdd(label, valueText);
            }
        }

        var holdings = new List<OwnedProductModel>(orderedNames.Count);
        foreach (var name in orderedNames)
        {
            var fields = fieldsByName[name];
            var currentValue = fields.TryGetValue("적립금", out var v) ? ParseDecimal(v) : null;
            // A card without 적립금 is metadata noise (header pseudo-card, etc.); skip.
            if (currentValue is null or <= 0m) continue;

            var holding = new OwnedProductModel
            {
                ProductName = name,
                CurrentValue = currentValue.Value,
            };

            if (fields.TryGetValue("수익률", out var ret)) holding.ReturnRate = ParseDecimal(ret);
            if (fields.TryGetValue("연환산수익률", out var ann)) holding.AnnualizedReturn = ParseDecimal(ann);
            if (fields.TryGetValue("운용일수", out var days) && ParseDecimal(days) is { } d)
                holding.InvestedDays = (int)d;
            if (fields.TryGetValue("좌수", out var shares)) holding.TotalShares = ParseDecimal(shares);

            holdings.Add(holding);
        }
        return holdings;
    }

    private static string ReadText(HtmlNode? node)
        => node == null ? string.Empty : HtmlEntity.DeEntitize(node.InnerText).Trim();

    /// <summary>
    /// Collapses internal whitespace so "연환산 수익률" (Tab 2) and "연환산수익률" (Tab 1)
    /// resolve to the same dictionary key.
    /// </summary>
    private static string NormalizeLabel(string label)
        => string.IsNullOrEmpty(label) ? label : Regex.Replace(label, @"\s+", "");

    /// <summary>
    /// Pulls the first signed numeric token out of text like "30,674,752원", "20.21%", "565일",
    /// and returns it as a decimal. Returns null when no numeric token is present.
    /// </summary>
    private static decimal? ParseDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = DigitsAndDecimal.Match(text);
        if (!match.Success) return null;
        var token = match.Value.Replace(",", "");
        return decimal.TryParse(token, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }
}
