using System.Globalization;
using System.Text;
using PensionCompass.Core.Models;

namespace PensionCompass.Core.Csv;

/// <summary>
/// Reads the portfolio CSV pair produced by <see cref="PortfolioCsvWriter"/> back into a
/// <see cref="PortfolioSnapshot"/>. Either file may be absent — a missing summary file
/// yields all-null totals, a missing holdings file yields an empty list.
/// </summary>
public static class PortfolioCsvLoader
{
    public const string SummaryFileName = "내_계좌_요약.csv";
    public const string HoldingsFileName = "내_보유_상품.csv";

    public static PortfolioSnapshot Load(string folderPath)
    {
        var summaryPath = Path.Combine(folderPath, SummaryFileName);
        var holdingsPath = Path.Combine(folderPath, HoldingsFileName);

        var (total, deposit, profit) = File.Exists(summaryPath)
            ? LoadSummary(summaryPath)
            : (null, null, null);

        var holdings = File.Exists(holdingsPath)
            ? LoadHoldings(holdingsPath)
            : new List<OwnedProductModel>();

        return new PortfolioSnapshot(total, deposit, profit, holdings);
    }

    public static (decimal? Total, decimal? Deposit, decimal? Profit) LoadSummary(string filePath)
    {
        var rows = ParseFile(filePath);
        if (rows.Count < 2) return (null, null, null);

        var col = new ColumnLookup(rows[0]);
        var first = rows[1];
        return (
            ParseDecimal(col.Get(first, "총적립금")),
            ParseDecimal(col.Get(first, "입금액")),
            ParseDecimal(col.Get(first, "운용수익")));
    }

    public static List<OwnedProductModel> LoadHoldings(string filePath)
    {
        var rows = ParseFile(filePath);
        if (rows.Count == 0) return [];

        var col = new ColumnLookup(rows[0]);
        col.Require("상품명", "적립금");

        var result = new List<OwnedProductModel>(rows.Count - 1);
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (IsBlankRow(row)) continue;

            var name = col.Get(row, "상품명").Trim();
            if (string.IsNullOrEmpty(name)) continue;

            var currentValue = ParseDecimal(col.Get(row, "적립금")) ?? 0m;
            var holding = new OwnedProductModel
            {
                ProductName = name,
                CurrentValue = currentValue,
                ReturnRate = ParseDecimal(col.Get(row, "수익률")),
                AnnualizedReturn = ParseDecimal(col.Get(row, "연환산수익률")),
                InvestedDays = ParseInt(col.Get(row, "운용일수")),
                TotalShares = ParseDecimal(col.Get(row, "좌수")),
            };
            result.Add(holding);
        }
        return result;
    }

    private static decimal? ParseDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return decimal.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static int? ParseInt(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static bool IsBlankRow(IReadOnlyList<string> row)
        => row.Count == 0 || row.All(string.IsNullOrEmpty);

    private static List<List<string>> ParseFile(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ParseRfc4180(reader.ReadToEnd());
    }

    private static List<List<string>> ParseRfc4180(string text)
    {
        var rows = new List<List<string>>();
        var current = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                field.Append(c);
                i++;
                continue;
            }

            if (c == '"' && field.Length == 0) { inQuotes = true; i++; continue; }
            if (c == ',') { current.Add(field.ToString()); field.Clear(); i++; continue; }
            if (c == '\r') { i++; continue; }
            if (c == '\n') { current.Add(field.ToString()); rows.Add(current); current = []; field.Clear(); i++; continue; }
            field.Append(c);
            i++;
        }

        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            rows.Add(current);
        }
        return rows;
    }

    private sealed class ColumnLookup
    {
        private readonly Dictionary<string, int> _indexByName;

        public ColumnLookup(IReadOnlyList<string> header)
        {
            _indexByName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < header.Count; i++)
                _indexByName[header[i].Trim()] = i;
        }

        public void Require(params string[] names)
        {
            var missing = names.Where(n => !_indexByName.ContainsKey(n)).ToList();
            if (missing.Count > 0)
                throw new InvalidDataException($"CSV 헤더에 필수 컬럼이 없습니다: {string.Join(", ", missing)}");
        }

        public string Get(IReadOnlyList<string> row, string name)
        {
            if (!_indexByName.TryGetValue(name, out var idx)) return string.Empty;
            return idx < row.Count ? row[idx] : string.Empty;
        }
    }
}
