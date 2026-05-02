using System.Text.RegularExpressions;

namespace SLifeIrpRebalancer.Core.Parsing;

/// <summary>
/// Derives the 운용사 (asset manager) for principal-guaranteed products,
/// which — unlike funds — do not carry the manager in a dedicated DOM element.
/// Funds expose div.desc-sm > span:first-child explicitly; this resolver is for the other case.
/// </summary>
public sealed class AssetManagerResolver
{
    private const string SamsungLifeFallback = "삼성생명";

    private static readonly string[] KnownPrefixes =
    [
        "푸본현대생명", "고려저축은행", "케이비저축은행", "신한저축은행",
        "페퍼저축은행", "웰컴저축은행", "다올저축은행", "OK저축은행",
        "메리츠", "한화", "교보", "흥국", "신한라이프", "DB", "KB",
        "삼성", "미래에셋", "NH",
    ];

    private static readonly Regex LeadingBracketTag = new(@"^\[[^\]]+\]\s*", RegexOptions.Compiled);
    private static readonly Regex LeadingMubaedang = new(@"^무배당\s+", RegexOptions.Compiled);

    public string ResolveFromProductName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return SamsungLifeFallback;

        var normalized = productName;
        normalized = LeadingBracketTag.Replace(normalized, "");
        normalized = LeadingMubaedang.Replace(normalized, "");
        normalized = normalized.TrimStart();

        foreach (var prefix in KnownPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                return prefix;
        }

        return SamsungLifeFallback;
    }
}
