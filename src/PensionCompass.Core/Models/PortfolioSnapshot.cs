namespace PensionCompass.Core.Models;

/// <summary>
/// Account-level totals + per-holding rows extracted from a Samsung Life
/// "적립금/수익률 조회" page. Intentionally PII-free — no subscriber name,
/// no account number, no contract number; the parser refuses to read those areas.
/// Numeric fields are nullable so an empty page yields all-null totals rather than zeros.
/// </summary>
public sealed record PortfolioSnapshot(
    decimal? TotalAmount,
    decimal? DepositAmount,
    decimal? ProfitAmount,
    IReadOnlyList<OwnedProductModel> Holdings);
