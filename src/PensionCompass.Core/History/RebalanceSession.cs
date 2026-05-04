using PensionCompass.Core.Ai;
using PensionCompass.Core.Models;

namespace PensionCompass.Core.History;

/// <summary>
/// One archived rebalancing session — the inputs the user gave to the AI plus the
/// markdown recommendation they got back. Saved when the user opts in via the
/// "이력에 저장" button on the AI Rebalance screen, so iterations across multiple
/// providers/models in a single session don't accidentally pile up. Files live in
/// the sync folder when configured, otherwise in LocalState\History\.
///
/// Intentionally PII-free in the same sense as <see cref="PortfolioSnapshot"/>:
/// no subscriber name, no account number, no API key. What goes in is everything
/// the user already typed into the app plus the AI's text response.
/// </summary>
public sealed record RebalanceSession(
    RebalanceSessionMeta Meta,
    AccountStatusModel Account,
    string UserAdditionalQuery,
    string RecommendationMarkdown);

/// <summary>
/// Fast-to-deserialize header for listing past sessions without paying to read the
/// full account snapshot or markdown body. Fields are picked to drive the history
/// list UI: timestamp + AI identity + a one-line summary.
/// </summary>
public sealed record RebalanceSessionMeta(
    DateTime Timestamp,
    string ProviderName,
    string ModelId,
    ThinkingLevel ThinkingLevel,
    int HoldingsCount,
    decimal TotalAmount,
    int CatalogPrincipalGuaranteedCount,
    int CatalogFundCount);
