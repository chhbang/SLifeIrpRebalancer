using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SLifeIrpRebalancer.Core.Csv;
using SLifeIrpRebalancer.Core.Models;
using SLifeIrpRebalancer.Core.Parsing;
using SLifeIrpRebalancer.Services;

namespace SLifeIrpRebalancer.ViewModels;

public partial class DataPreparationViewModel : ObservableObject
{
    private readonly SamsungLifeHtmlParser _parser = new();

    public ObservableCollection<PrincipalGuaranteedProduct> PrincipalGuaranteed { get; } = [];
    public ObservableCollection<FundRow> Funds { get; } = [];

    [ObservableProperty]
    private string _statusMessage = "HTML 파일을 불러와 CSV로 변환하세요.";

    [ObservableProperty]
    private string _periodSummary = "수익률 기간: (없음)";

    [ObservableProperty]
    private bool _hasCatalog;

    [ObservableProperty]
    private bool _isBusy;

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
    }

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
    }

    private static IReadOnlyList<ReturnPeriod> AllPeriodsForCsv(ProductCatalog catalog)
    {
        // CSV always emits all five columns so the layout is stable across snapshots.
        // Cells fall back to empty strings for periods the user hasn't imported yet.
        return [ReturnPeriod.Month1, ReturnPeriod.Month3, ReturnPeriod.Month6, ReturnPeriod.Year1, ReturnPeriod.Year3];
    }
}
