using System.Globalization;
using System.Text;
using SLifeIrpRebalancer.Core.Models;

namespace SLifeIrpRebalancer.Core.Ai;

public sealed record PromptInput(
    ProductCatalog? Catalog,
    AccountStatusModel Account,
    bool RestrictToSamsungLifeForLifelongAnnuity,
    string UserAdditionalQuery);

public sealed record PromptOutput(string SystemPrompt, string UserPrompt);

/// <summary>
/// Builds the system+user prompt sent to the AI provider. Pure (no I/O), so it's testable
/// in isolation. The user prompt is a single markdown document with sections for timing,
/// account state, holdings (with per-row sellable flag), the matched product universe,
/// any manager-restriction note, and the user's free-text addendum.
/// </summary>
public static class PromptBuilder
{
    private const string SystemPromptText =
        "당신은 한국 퇴직연금(IRP) 포트폴리오 전문가입니다. 사용자가 제공한 보유 상품, 매수 가능한 상품 유니버스, " +
        "그리고 매도 정책과 시장 환경을 종합하여 구체적이고 실행 가능한 리밸런싱 제안을 작성하세요. " +
        "답변은 한국어 마크다운으로 거시 환경 진단 → 매도 후보 선정 → 매수 배분 → 제안 근거 요약 순으로 구조화하고, " +
        "구체적인 금액과 비중을 명시해주세요.";

    public static PromptOutput Build(PromptInput input)
    {
        var sb = new StringBuilder();
        AppendTiming(sb, input.Account);
        AppendManagerConstraintIfNeeded(sb, input.RestrictToSamsungLifeForLifelongAnnuity);
        AppendAccountSummary(sb, input.Account);
        AppendHoldings(sb, input.Account.OwnedItems);
        AppendCatalog(sb, input.Catalog);
        AppendUserAddendum(sb, input.UserAdditionalQuery);
        AppendInstructions(sb);
        return new PromptOutput(SystemPromptText, sb.ToString().TrimEnd());
    }

    private static void AppendTiming(StringBuilder sb, AccountStatusModel account)
    {
        sb.AppendLine("## 리밸런싱 시점");
        if (account.RebalanceTiming == RebalanceTiming.Immediate)
        {
            sb.AppendLine("- 최대한 즉시 일반 리밸런싱을 실행합니다. 현재 시점의 시장 상황을 반영해주세요.");
        }
        else
        {
            sb.AppendLine("- 만기 예약용 리밸런싱입니다. 즉시 실행이 아니라 아래 실행 예정일을 기준으로 제안해주세요.");
            if (account.ExecutionDate is { } date)
                sb.AppendLine($"- 실행 예정일: {date:yyyy년 M월 d일}");
            else
                sb.AppendLine("- 실행 예정일: (미지정 — 사용자가 입력하지 않았습니다)");
        }
        sb.AppendLine();
    }

    private static void AppendManagerConstraintIfNeeded(StringBuilder sb, bool restrict)
    {
        if (!restrict) return;
        sb.AppendLine("## 운용사 제약 (필수)");
        sb.AppendLine("- 사용자는 연금 개시 시 종신형 수령을 계획 중이며, 삼성생명 고객센터 안내에 따르면 종신형 지급을 받으려면 연금 개시 직전에 운용사가 \"삼성생명\"인 상품들로만 운용되어야 합니다.");
        sb.AppendLine("- 따라서 신규 매수 추천은 운용사 이름에 \"삼성생명\" 또는 \"삼성\"이 포함된 상품으로만 제한해주세요.");
        sb.AppendLine();
    }

    private static void AppendAccountSummary(StringBuilder sb, AccountStatusModel account)
    {
        sb.AppendLine("## 사용자 IRP 계좌 현황");
        sb.AppendLine($"- 총 적립금: {Won(account.TotalAmount)}");
        if (account.DepositAmount.HasValue)
            sb.AppendLine($"- 입금액 (원금): {Won(account.DepositAmount.Value)}");
        if (account.ProfitAmount.HasValue)
            sb.AppendLine($"- 운용수익: {Won(account.ProfitAmount.Value)}");
        sb.AppendLine();
    }

    private static void AppendHoldings(StringBuilder sb, IReadOnlyList<OwnedProductModel> holdings)
    {
        sb.AppendLine($"## 보유 상품 ({holdings.Count}개)");
        if (holdings.Count == 0)
        {
            sb.AppendLine("(보유 상품이 입력되지 않았습니다)");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| 상품명 | 적립금 | 수익률 | 연환산수익률 | 운용일수 | 좌수 | 매도 정책 |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---|");
        foreach (var h in holdings)
        {
            sb.Append("| ").Append(EscapeCell(h.ProductName));
            sb.Append(" | ").Append(Won(h.CurrentValue));
            sb.Append(" | ").Append(Percent(h.ReturnRate));
            sb.Append(" | ").Append(Percent(h.AnnualizedReturn));
            sb.Append(" | ").Append(h.InvestedDays?.ToString(CultureInfo.InvariantCulture) ?? "-");
            sb.Append(" | ").Append(h.TotalShares?.ToString("N4", CultureInfo.InvariantCulture) ?? "-");
            sb.Append(" | ").Append(h.IsSellable ? "매도 가능" : "매도 금지");
            sb.AppendLine(" |");
        }
        sb.AppendLine();
        sb.AppendLine("매도 정책 안내:");
        sb.AppendLine("- \"매도 가능\"으로 표시된 상품만 매도 후보입니다. 매도 비중과 전량/부분 여부는 당신이 시장 상황과 포트폴리오 균형을 고려해 결정해주세요.");
        sb.AppendLine("- \"매도 금지\"로 표시된 상품은 절대 매도하지 말고 그대로 보유해야 합니다.");
        sb.AppendLine();
    }

    private static void AppendCatalog(StringBuilder sb, ProductCatalog? catalog)
    {
        sb.AppendLine("## 매수 가능한 상품 유니버스");
        if (catalog is null || (catalog.PrincipalGuaranteed.Count == 0 && catalog.Funds.Count == 0))
        {
            sb.AppendLine("(상품 카탈로그가 입력되지 않았습니다 — \"상품 데이터 준비\" 화면에서 HTML 또는 CSV를 불러오세요)");
            sb.AppendLine();
            return;
        }

        AppendPrincipalGuaranteedTable(sb, catalog.PrincipalGuaranteed);
        AppendFundTable(sb, catalog.Funds);
    }

    private static void AppendPrincipalGuaranteedTable(StringBuilder sb, IReadOnlyList<PrincipalGuaranteedProduct> items)
    {
        sb.AppendLine($"### 원리금보장형 ({items.Count}개)");
        if (items.Count == 0)
        {
            sb.AppendLine("(없음)");
            sb.AppendLine();
            return;
        }
        sb.AppendLine("| 운용사 | 상품코드 | 상품명 | 금리 | 만기 |");
        sb.AppendLine("|---|---|---|---:|---|");
        foreach (var p in items)
        {
            sb.Append("| ").Append(EscapeCell(p.AssetManager));
            sb.Append(" | ").Append(EscapeCell(p.ProductCode));
            sb.Append(" | ").Append(EscapeCell(p.ProductName));
            sb.Append(" | ").Append(EscapeCell(p.AppliedRate));
            sb.Append(" | ").Append(EscapeCell(p.MaturityTerm));
            sb.AppendLine(" |");
        }
        sb.AppendLine();
    }

    private static void AppendFundTable(StringBuilder sb, IReadOnlyList<FundProduct> funds)
    {
        sb.AppendLine($"### 펀드 ({funds.Count}개)");
        if (funds.Count == 0)
        {
            sb.AppendLine("(없음)");
            sb.AppendLine();
            return;
        }
        sb.AppendLine("| 운용사 | 상품코드 | 상품명 | 위험등급 | 1개월 | 3개월 | 6개월 | 1년 | 3년 |");
        sb.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|");
        foreach (var f in funds)
        {
            sb.Append("| ").Append(EscapeCell(f.AssetManager));
            sb.Append(" | ").Append(EscapeCell(f.ProductCode));
            sb.Append(" | ").Append(EscapeCell(f.ProductName));
            sb.Append(" | ").Append(EscapeCell(f.RiskGrade));
            sb.Append(" | ").Append(GetReturnCell(f, ReturnPeriod.Month1));
            sb.Append(" | ").Append(GetReturnCell(f, ReturnPeriod.Month3));
            sb.Append(" | ").Append(GetReturnCell(f, ReturnPeriod.Month6));
            sb.Append(" | ").Append(GetReturnCell(f, ReturnPeriod.Year1));
            sb.Append(" | ").Append(GetReturnCell(f, ReturnPeriod.Year3));
            sb.AppendLine(" |");
        }
        sb.AppendLine();
    }

    private static void AppendUserAddendum(StringBuilder sb, string userQuery)
    {
        sb.AppendLine("## 사용자 추가 요구사항");
        sb.AppendLine(string.IsNullOrWhiteSpace(userQuery) ? "(없음)" : userQuery.Trim());
        sb.AppendLine();
    }

    private static void AppendInstructions(StringBuilder sb)
    {
        sb.AppendLine("## 답변 요청");
        sb.AppendLine("위 정보를 종합하여 다음을 포함한 리밸런싱 제안을 작성해주세요:");
        sb.AppendLine("1. 현재 거시경제·시장 환경 진단 (간략히)");
        sb.AppendLine("2. 매도 가능 보유 상품 중 매도 후보와 매도 금액·비중 (구체적인 ₩ 금액)");
        sb.AppendLine("3. 매도로 확보한 현금을 어떤 매수 가능 상품에 어떻게 배분할지 (구체적인 ₩ 금액과 %)");
        sb.AppendLine("4. 위 결정의 핵심 근거 요약");
    }

    private static string Won(decimal amount)
        => $"₩{amount.ToString("N0", CultureInfo.InvariantCulture)}";

    private static string Percent(decimal? value)
        => value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) + "%" : "-";

    private static string GetReturnCell(FundProduct fund, ReturnPeriod period)
        => fund.Returns.TryGetValue(period, out var v) ? EscapeCell(v) : "-";

    private static string EscapeCell(string s)
        => s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
