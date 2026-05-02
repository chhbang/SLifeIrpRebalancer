using SLifeIrpRebalancer.Core.Ai;
using SLifeIrpRebalancer.Core.Models;

namespace SLifeIrpRebalancer.Core.Tests;

public class PromptBuilderTests
{
    private static AccountStatusModel SampleAccount() => new()
    {
        TotalAmount = 192_808_229m,
        DepositAmount = 181_000_000m,
        ProfitAmount = 11_808_229m,
        RebalanceTiming = RebalanceTiming.Immediate,
        OwnedItems =
        [
            new OwnedProductModel
            {
                ProductName = "이율보증형(3년)",
                CurrentValue = 30_646_260m,
                IsSellable = false,
            },
            new OwnedProductModel
            {
                ProductName = "[온라인]우리BIG2플러스증권(채권혼)ClassC-Pe",
                CurrentValue = 23_129_650m,
                ReturnRate = 14.17m,
                IsSellable = true,
            },
        ],
    };

    private static ProductCatalog SampleCatalog() => new(
        PrincipalGuaranteed:
        [
            new("C02011", "무배당 메리츠 신탁제공용 이율보증형보험Ⅲ (IRP 3년)", "메리츠", "3.80%", "3년"),
        ],
        Funds:
        [
            new("G04783", "[온라인전용]미래에셋코어테크증권투자신탁(주식)", "미래에셋자산운용㈜", "매우높은위험",
                new Dictionary<ReturnPeriod, string> { [ReturnPeriod.Month3] = "45.00%" }),
        ],
        FundReturnPeriods: [ReturnPeriod.Month3]);

    [Fact]
    public void Build_AlwaysProducesKoreanSystemPrompt()
    {
        var output = PromptBuilder.Build(new PromptInput(null, new AccountStatusModel(), false, ""));

        Assert.Contains("IRP", output.SystemPrompt);
        Assert.Contains("한국어", output.SystemPrompt);
    }

    [Fact]
    public void Build_ImmediateTiming_DoesNotIncludeExecutionDate()
    {
        var account = SampleAccount();
        account.RebalanceTiming = RebalanceTiming.Immediate;
        account.ExecutionDate = null;

        var output = PromptBuilder.Build(new PromptInput(null, account, false, ""));

        Assert.Contains("최대한 즉시", output.UserPrompt);
        Assert.DoesNotContain("실행 예정일", output.UserPrompt);
    }

    [Fact]
    public void Build_MaturityReservation_IncludesExecutionDate()
    {
        var account = SampleAccount();
        account.RebalanceTiming = RebalanceTiming.MaturityReservation;
        account.ExecutionDate = new DateOnly(2027, 9, 20);

        var output = PromptBuilder.Build(new PromptInput(null, account, false, ""));

        Assert.Contains("만기 예약용", output.UserPrompt);
        Assert.Contains("2027년 9월 20일", output.UserPrompt);
    }

    [Fact]
    public void Build_LifelongAnnuity_IncludesManagerConstraint()
    {
        var output = PromptBuilder.Build(new PromptInput(null, SampleAccount(), RestrictToSamsungLifeForLifelongAnnuity: true, ""));

        Assert.Contains("운용사 제약", output.UserPrompt);
        Assert.Contains("삼성생명", output.UserPrompt);
    }

    [Fact]
    public void Build_NoLifelongAnnuity_OmitsManagerConstraint()
    {
        var output = PromptBuilder.Build(new PromptInput(null, SampleAccount(), RestrictToSamsungLifeForLifelongAnnuity: false, ""));

        Assert.DoesNotContain("운용사 제약", output.UserPrompt);
    }

    [Fact]
    public void Build_HoldingsTable_DistinguishesSellableFromLocked()
    {
        var output = PromptBuilder.Build(new PromptInput(null, SampleAccount(), false, ""));

        // The 이율보증형 row was IsSellable=false, the bond fund row was IsSellable=true.
        Assert.Contains("매도 금지", output.UserPrompt);
        Assert.Contains("매도 가능", output.UserPrompt);
        Assert.Contains("이율보증형(3년)", output.UserPrompt);
        Assert.Contains("₩30,646,260", output.UserPrompt);
        Assert.Contains("14.17%", output.UserPrompt);
    }

    [Fact]
    public void Build_Catalog_RendersBothPrincipalGuaranteedAndFundsTables()
    {
        var output = PromptBuilder.Build(new PromptInput(SampleCatalog(), SampleAccount(), false, ""));

        Assert.Contains("### 원리금보장형", output.UserPrompt);
        Assert.Contains("### 펀드", output.UserPrompt);
        Assert.Contains("미래에셋코어테크", output.UserPrompt);
        Assert.Contains("45.00%", output.UserPrompt);
    }

    [Fact]
    public void Build_NullCatalog_NotesItIsMissing()
    {
        var output = PromptBuilder.Build(new PromptInput(null, SampleAccount(), false, ""));

        Assert.Contains("상품 카탈로그가 입력되지 않았습니다", output.UserPrompt);
    }

    [Fact]
    public void Build_UserAddendum_AppearsVerbatim()
    {
        var query = "이란 전쟁 상황을 반영해주세요.";
        var output = PromptBuilder.Build(new PromptInput(null, SampleAccount(), false, query));

        Assert.Contains(query, output.UserPrompt);
    }

    [Fact]
    public void Build_EmptyAddendum_RendersAsNone()
    {
        var output = PromptBuilder.Build(new PromptInput(null, SampleAccount(), false, ""));

        Assert.Contains("## 사용자 추가 요구사항", output.UserPrompt);
        Assert.Contains("(없음)", output.UserPrompt);
    }
}
