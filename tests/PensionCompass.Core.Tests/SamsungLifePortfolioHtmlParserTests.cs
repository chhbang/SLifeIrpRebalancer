using PensionCompass.Core.Models;
using PensionCompass.Core.Parsing;

namespace PensionCompass.Core.Tests;

public class SamsungLifePortfolioHtmlParserTests
{
    private static string LoadReferenceHtml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "samsunglife_portfolio.html");
        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_Totals_AreExtractedFromPrdSummary()
    {
        var parser = new SamsungLifePortfolioHtmlParser();

        var snapshot = parser.Parse(LoadReferenceHtml());

        Assert.Equal(193_998_473m, snapshot.TotalAmount);
        Assert.Equal(149_914_600m, snapshot.DepositAmount);
        Assert.Equal(44_083_873m, snapshot.ProfitAmount);
    }

    [Fact]
    public void Parse_Holdings_AreDeduplicatedByProductName()
    {
        var parser = new SamsungLifePortfolioHtmlParser();

        var snapshot = parser.Parse(LoadReferenceHtml());

        // The reference page has 3 PG + 4 funds, each appearing twice (one card per tab).
        Assert.Equal(7, snapshot.Holdings.Count);
        Assert.Equal(snapshot.Holdings.Select(h => h.ProductName).Distinct().Count(), snapshot.Holdings.Count);
    }

    [Fact]
    public void Parse_HoldingTotals_SumToAccountTotal()
    {
        var parser = new SamsungLifePortfolioHtmlParser();

        var snapshot = parser.Parse(LoadReferenceHtml());

        var sum = snapshot.Holdings.Sum(h => h.CurrentValue);
        Assert.Equal(snapshot.TotalAmount, sum);
    }

    [Fact]
    public void Parse_FundHolding_ExposesAllFiveFields()
    {
        var parser = new SamsungLifePortfolioHtmlParser();

        var snapshot = parser.Parse(LoadReferenceHtml());

        // 삼성밀당다람쥐... has 적립금, 수익률, 연환산, 운용일수 (Tab 2), 좌수 (Tab 1 hidden) all set.
        var fund = snapshot.Holdings.Single(h => h.ProductName.StartsWith("삼성밀당다람쥐", StringComparison.Ordinal));
        Assert.Equal(29_955_325m, fund.CurrentValue);
        Assert.Equal(20.21m, fund.ReturnRate);
        Assert.Equal(12.63m, fund.AnnualizedReturn);
        Assert.Equal(565, fund.InvestedDays);
        Assert.Equal(15_697_799m, fund.TotalShares);
    }

    [Fact]
    public void Parse_PrincipalGuaranteed_PicksUpReturnFieldsFromTab2()
    {
        var parser = new SamsungLifePortfolioHtmlParser();

        var snapshot = parser.Parse(LoadReferenceHtml());

        // Tab 1's PG card lacks 수익률·연환산·운용일수 — only Tab 2's PG card carries them.
        // Verifies that field merging across tabs actually pulls those in.
        var pg = snapshot.Holdings.Single(h => h.ProductName == "이율보증형(3년)");
        Assert.Equal(30_674_752m, pg.CurrentValue);
        Assert.Equal(9.11m, pg.ReturnRate);
        Assert.Equal(1.40m, pg.AnnualizedReturn);
        Assert.Equal(2294, pg.InvestedDays);
    }

    [Fact]
    public void Parse_HoldingDefaultsToSellable()
    {
        var parser = new SamsungLifePortfolioHtmlParser();

        var snapshot = parser.Parse(LoadReferenceHtml());

        // Imported holdings carry the model's default IsSellable=true; user can later mark
        // specific rows as locked on the SellTargets screen.
        Assert.All(snapshot.Holdings, h => Assert.True(h.IsSellable));
    }

    [Fact]
    public void Parse_DoesNotLeakSubscriberPii()
    {
        var parser = new SamsungLifePortfolioHtmlParser();

        var snapshot = parser.Parse(LoadReferenceHtml());

        // The subscriber name and account number must never end up in any product name.
        // (The reference page has them visible elsewhere on the document; those areas are
        // not parser inputs by design.)
        Assert.All(snapshot.Holdings, h =>
        {
            Assert.DoesNotContain("방창환", h.ProductName);
            Assert.DoesNotContain("9641179110049", h.ProductName);
            Assert.DoesNotContain("개인형IRP", h.ProductName);
        });
    }
}
