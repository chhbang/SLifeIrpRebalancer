using System.Globalization;
using System.Text;
using PensionCompass.Core.Models;

namespace PensionCompass.Core.Csv;

/// <summary>
/// Writes a <see cref="PortfolioSnapshot"/> to two CSV files inside a folder, mirroring the
/// catalog's HTML→CSV→reload pattern. Numeric values are emitted as raw decimals (no 원/%/일
/// suffix) so the round-trip into <see cref="OwnedProductModel"/> stays lossless.
/// UTF-8 with BOM, RFC 4180 quoting.
/// </summary>
public static class PortfolioCsvWriter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static void Write(string folderPath, PortfolioSnapshot snapshot)
    {
        Directory.CreateDirectory(folderPath);
        WriteSummary(Path.Combine(folderPath, PortfolioCsvLoader.SummaryFileName), snapshot);
        WriteHoldings(Path.Combine(folderPath, PortfolioCsvLoader.HoldingsFileName), snapshot.Holdings);
    }

    private static void WriteSummary(string path, PortfolioSnapshot snapshot)
    {
        using var writer = new StreamWriter(path, append: false, Utf8WithBom);
        WriteRow(writer, ["총적립금", "입금액", "운용수익"]);
        WriteRow(writer, [
            FormatDecimal(snapshot.TotalAmount),
            FormatDecimal(snapshot.DepositAmount),
            FormatDecimal(snapshot.ProfitAmount),
        ]);
    }

    private static void WriteHoldings(string path, IReadOnlyList<OwnedProductModel> holdings)
    {
        using var writer = new StreamWriter(path, append: false, Utf8WithBom);
        WriteRow(writer, ["상품명", "적립금", "수익률", "연환산수익률", "운용일수", "좌수"]);
        foreach (var h in holdings)
        {
            WriteRow(writer, [
                h.ProductName,
                FormatDecimal(h.CurrentValue),
                FormatDecimal(h.ReturnRate),
                FormatDecimal(h.AnnualizedReturn),
                h.InvestedDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                FormatDecimal(h.TotalShares),
            ]);
        }
    }

    private static string FormatDecimal(decimal? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static void WriteRow(StreamWriter writer, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) writer.Write(',');
            writer.Write(Quote(fields[i]));
        }
        writer.Write("\r\n");
    }

    private static string Quote(string field)
    {
        var needsQuoting = field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
        if (!needsQuoting) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
