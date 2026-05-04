using PensionCompass.Core.Csv;
using PensionCompass.Core.Models;

namespace PensionCompass.Core.Tests;

public class PortfolioCsvRoundTripTests
{
    [Fact]
    public void RoundTrip_PreservesTotalsAndAllOptionalFields()
    {
        var snapshot = new PortfolioSnapshot(
            TotalAmount: 193_998_473m,
            DepositAmount: 149_914_600m,
            ProfitAmount: 44_083_873m,
            Holdings: [
                new OwnedProductModel
                {
                    ProductName = "삼성밀당다람쥐글로벌EMP증권자투자신탁[주식혼합-재간접형]Cpe",
                    CurrentValue = 29_955_325m,
                    ReturnRate = 20.21m,
                    AnnualizedReturn = 12.63m,
                    InvestedDays = 565,
                    TotalShares = 15_697_799m,
                },
                new OwnedProductModel
                {
                    ProductName = "이율보증형(3년)",
                    CurrentValue = 30_674_752m,
                    ReturnRate = 9.11m,
                    AnnualizedReturn = 1.40m,
                    InvestedDays = 2294,
                    // PG products have no 좌수 — leave null.
                },
            ]);

        using var tempDir = TempFolder.Create();
        PortfolioCsvWriter.Write(tempDir.Path, snapshot);
        var loaded = PortfolioCsvLoader.Load(tempDir.Path);

        Assert.Equal(snapshot.TotalAmount, loaded.TotalAmount);
        Assert.Equal(snapshot.DepositAmount, loaded.DepositAmount);
        Assert.Equal(snapshot.ProfitAmount, loaded.ProfitAmount);
        Assert.Equal(snapshot.Holdings.Count, loaded.Holdings.Count);

        for (var i = 0; i < snapshot.Holdings.Count; i++)
        {
            var src = snapshot.Holdings[i];
            var dst = loaded.Holdings[i];
            Assert.Equal(src.ProductName, dst.ProductName);
            Assert.Equal(src.CurrentValue, dst.CurrentValue);
            Assert.Equal(src.ReturnRate, dst.ReturnRate);
            Assert.Equal(src.AnnualizedReturn, dst.AnnualizedReturn);
            Assert.Equal(src.InvestedDays, dst.InvestedDays);
            Assert.Equal(src.TotalShares, dst.TotalShares);
        }
    }

    [Fact]
    public void RoundTrip_HandlesNullTotalsAndOmittedFields()
    {
        var snapshot = new PortfolioSnapshot(
            TotalAmount: null,
            DepositAmount: null,
            ProfitAmount: null,
            Holdings: [
                new OwnedProductModel { ProductName = "최소상품", CurrentValue = 1m },
            ]);

        using var tempDir = TempFolder.Create();
        PortfolioCsvWriter.Write(tempDir.Path, snapshot);
        var loaded = PortfolioCsvLoader.Load(tempDir.Path);

        Assert.Null(loaded.TotalAmount);
        Assert.Null(loaded.DepositAmount);
        Assert.Null(loaded.ProfitAmount);
        var single = Assert.Single(loaded.Holdings);
        Assert.Equal("최소상품", single.ProductName);
        Assert.Equal(1m, single.CurrentValue);
        Assert.Null(single.ReturnRate);
        Assert.Null(single.AnnualizedReturn);
        Assert.Null(single.InvestedDays);
        Assert.Null(single.TotalShares);
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; }
        private TempFolder(string path) { Path = path; }
        public static TempFolder Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PensionCompass_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempFolder(path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
