using SLifeIrpRebalancer.Core.Models;

namespace SLifeIrpRebalancer.Core.Pdf;

public sealed record PdfReport(
    DateTime GeneratedAt,
    string ProviderName,
    string ModelId,
    AccountStatusModel Account,
    string AiResponseMarkdown);
