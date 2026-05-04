using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PensionCompass.Core.Csv;
using PensionCompass.Core.Models;
using PensionCompass.Core.Parsing;
using PensionCompass.Services;

namespace PensionCompass.ViewModels;

public partial class DataPreparationViewModel : ObservableObject
{
    private readonly SamsungLifeHtmlParser _parser = new();
    private readonly SamsungLifePortfolioHtmlParser _portfolioParser = new();

    public ObservableCollection<PrincipalGuaranteedProduct> PrincipalGuaranteed { get; } = [];
    public ObservableCollection<FundRow> Funds { get; } = [];

    [ObservableProperty]
    private string _statusMessage = "HTML 파일을 불러와 CSV로 변환하세요.";

    [ObservableProperty]
    private string _periodSummary = "수익률 기간: (없음)";

    [ObservableProperty]
    private string _samsungLifeSummary = string.Empty;

    [ObservableProperty]
    private bool _hasCatalog;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _portfolioStatusMessage = "내 계좌 HTML을 불러오면 보유 상품과 합계가 자동 입력됩니다.";

    [ObservableProperty]
    private bool _hasPortfolio;

    public async Task ImportHtmlAsync(IReadOnlyList<string> filePaths, bool replaceExisting)
    {
        if (filePaths.Count == 0) return;

        IsBusy = true;
        try
        {
            var snapshots = new List<ProductCatalog>();
            if (!replaceExisting && AppState.Instance.Catalog is { } existing)
                snapshots.Add(existing);

            foreach (var path in filePaths)
            {
                var html = await File.ReadAllTextAsync(path);
                snapshots.Add(_parser.Parse(html));
            }

            var merged = ProductCatalogMerger.Merge(snapshots);
            AppState.Instance.Catalog = merged;
            ApplyCatalog(merged);

            var fundCount = merged.Funds.Count;
            var pgCount = merged.PrincipalGuaranteed.Count;
            StatusMessage = $"불러왔습니다. 원리금보장형 {pgCount:N0}개, 펀드 {fundCount:N0}개.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"오류: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadCsvAsync(string folderPath)
    {
        IsBusy = true;
        try
        {
            var loaded = await Task.Run(() => CsvCatalogLoader.Load(folderPath));
            if (loaded.PrincipalGuaranteed.Count == 0 && loaded.Funds.Count == 0)
            {
                StatusMessage = "선택한 폴더에 CSV 파일이 없거나 비어 있습니다.";
                return;
            }

            AppState.Instance.Catalog = loaded;
            ApplyCatalog(loaded);
            StatusMessage = $"CSV 불러옴. 원리금보장형 {loaded.PrincipalGuaranteed.Count:N0}개, 펀드 {loaded.Funds.Count:N0}개.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"CSV 오류: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveCsvAsync(string folderPath)
    {
        if (AppState.Instance.Catalog is not { } catalog)
        {
            StatusMessage = "저장할 데이터가 없습니다. 먼저 HTML을 불러오세요.";
            return;
        }

        IsBusy = true;
        try
        {
            var pgPath = Path.Combine(folderPath, "원리금보장형_상품목록.csv");
            var fundPath = Path.Combine(folderPath, "펀드_상품목록.csv");

            await Task.Run(() =>
            {
                CsvWriter.WritePrincipalGuaranteed(pgPath, catalog.PrincipalGuaranteed);
                CsvWriter.WriteFunds(fundPath, catalog.Funds, AllPeriodsForCsv(catalog));
            });

            StatusMessage = $"저장 완료: {pgPath}, {fundPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 오류: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void LoadFromAppState()
    {
        if (AppState.Instance.Catalog is { } catalog)
            ApplyCatalog(catalog);
        RefreshPortfolioStatus();
    }

    /// <summary>
    /// Parses the saved Samsung Life "적립금/수익률 조회" HTML and returns a snapshot.
    /// The view is responsible for confirming overwrite (when an account already has data)
    /// before applying the snapshot via <see cref="ApplyPortfolio"/>.
    /// </summary>
    public async Task<PortfolioSnapshot?> ParsePortfolioHtmlAsync(string filePath)
    {
        IsBusy = true;
        try
        {
            var html = await File.ReadAllTextAsync(filePath);
            var snapshot = await Task.Run(() => _portfolioParser.Parse(html));
            PortfolioStatusMessage = $"불러올 보유 상품 {snapshot.Holdings.Count:N0}개, 총적립금 {Format(snapshot.TotalAmount)}원. 적용할까요?";
            return snapshot;
        }
        catch (Exception ex)
        {
            PortfolioStatusMessage = $"내 계좌 HTML 오류: {ex.Message}";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<PortfolioSnapshot?> ParsePortfolioCsvAsync(string folderPath)
    {
        IsBusy = true;
        try
        {
            var snapshot = await Task.Run(() => PortfolioCsvLoader.Load(folderPath));
            if (snapshot.Holdings.Count == 0 && snapshot.TotalAmount is null)
            {
                PortfolioStatusMessage = "선택한 폴더에 내 계좌 CSV가 없거나 비어 있습니다.";
                return null;
            }
            PortfolioStatusMessage = $"불러올 보유 상품 {snapshot.Holdings.Count:N0}개, 총적립금 {Format(snapshot.TotalAmount)}원. 적용할까요?";
            return snapshot;
        }
        catch (Exception ex)
        {
            PortfolioStatusMessage = $"내 계좌 CSV 오류: {ex.Message}";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Overwrites the current account's totals and OwnedItems with the snapshot.
    /// Subscriber info (current age, lifelong-annuity flag, etc.) and rebalance settings are preserved
    /// because those are user choices the parser doesn't know about.
    /// </summary>
    public void ApplyPortfolio(PortfolioSnapshot snapshot)
    {
        var account = AppState.Instance.Account;

        // Totals: only overwrite when the snapshot actually has a value, so a CSV with blank
        // totals doesn't clobber numbers the user already typed.
        if (snapshot.TotalAmount is { } total) account.TotalAmount = total;
        if (snapshot.DepositAmount is { } deposit) account.DepositAmount = deposit;
        if (snapshot.ProfitAmount is { } profit) account.ProfitAmount = profit;

        // Holdings: replace wholesale. The previous IsSellable flags are reset to the model
        // default (true); the user marks locked rows again on the SellTargets screen.
        account.OwnedItems.Clear();
        foreach (var h in snapshot.Holdings)
            account.OwnedItems.Add(h);

        AppState.Instance.SaveAccount();
        PortfolioStatusMessage = $"적용 완료. 보유 상품 {snapshot.Holdings.Count:N0}개, 총적립금 {Format(snapshot.TotalAmount)}원.";
        RefreshPortfolioStatus();
    }

    public async Task SavePortfolioCsvAsync(string folderPath)
    {
        var account = AppState.Instance.Account;
        var snapshot = new PortfolioSnapshot(
            TotalAmount: account.TotalAmount > 0 ? account.TotalAmount : null,
            DepositAmount: account.DepositAmount,
            ProfitAmount: account.ProfitAmount,
            Holdings: account.OwnedItems.Select(Clone).ToList());

        IsBusy = true;
        try
        {
            await Task.Run(() => PortfolioCsvWriter.Write(folderPath, snapshot));
            PortfolioStatusMessage = $"내 계좌 CSV 저장 완료: {folderPath}";
        }
        catch (Exception ex)
        {
            PortfolioStatusMessage = $"내 계좌 CSV 저장 오류: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool AccountHasUserData()
    {
        var account = AppState.Instance.Account;
        return account.TotalAmount > 0 || account.OwnedItems.Count > 0;
    }

    private void RefreshPortfolioStatus()
    {
        var account = AppState.Instance.Account;
        HasPortfolio = account.TotalAmount > 0 || account.OwnedItems.Count > 0;
    }

    private static OwnedProductModel Clone(OwnedProductModel src) => new()
    {
        ProductName = src.ProductName,
        CurrentValue = src.CurrentValue,
        ReturnRate = src.ReturnRate,
        AnnualizedReturn = src.AnnualizedReturn,
        InvestedDays = src.InvestedDays,
        TotalShares = src.TotalShares,
        IsSellable = src.IsSellable,
    };

    private static string Format(decimal? value)
        => value.HasValue ? value.Value.ToString("N0") : "—";

    public void ResetCatalog()
    {
        AppState.Instance.ResetCatalog();
        PrincipalGuaranteed.Clear();
        Funds.Clear();
        HasCatalog = false;
        PeriodSummary = "수익률 기간: (없음)";
        StatusMessage = "카탈로그를 초기화했습니다.";
    }

    private void ApplyCatalog(ProductCatalog catalog)
    {
        PrincipalGuaranteed.Clear();
        foreach (var p in catalog.PrincipalGuaranteed)
            PrincipalGuaranteed.Add(p);

        Funds.Clear();
        foreach (var f in catalog.Funds)
            Funds.Add(new FundRow(f));

        HasCatalog = catalog.PrincipalGuaranteed.Count > 0 || catalog.Funds.Count > 0;
        PeriodSummary = catalog.FundReturnPeriods.Count == 0
            ? "수익률 기간: (없음)"
            : "수익률 기간: " + string.Join(", ", catalog.FundReturnPeriods.Select(p => p.ToKoreanLabel()));

        // Count of products run by 삼성생명보험주식회사 itself (not 삼성자산운용 etc.) —
        // this is the set eligible for the lifelong-annuity-payout option. Includes both PG
        // products and Samsung Life-managed funds like S Selection / 삼성그룹주식형 / 인덱스주식형.
        var samsungPg = catalog.PrincipalGuaranteed.Count(p => AssetManagerResolver.IsSamsungLifeInsurance(p.AssetManager));
        var samsungFunds = catalog.Funds.Count(f => AssetManagerResolver.IsSamsungLifeInsurance(f.AssetManager));
        SamsungLifeSummary = HasCatalog
            ? $"삼성생명보험주식회사 상품: 원리금보장형 {samsungPg}건, 펀드 {samsungFunds}건  (종신형 연금 옵션 활성 시 추천 후보 한정)"
            : string.Empty;
    }

    private static IReadOnlyList<ReturnPeriod> AllPeriodsForCsv(ProductCatalog catalog)
    {
        // CSV always emits all five columns so the layout is stable across snapshots.
        // Cells fall back to empty strings for periods the user hasn't imported yet.
        return [ReturnPeriod.Month1, ReturnPeriod.Month3, ReturnPeriod.Month6, ReturnPeriod.Year1, ReturnPeriod.Year3];
    }
}
