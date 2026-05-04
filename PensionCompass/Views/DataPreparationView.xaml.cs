using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PensionCompass.Services;
using PensionCompass.ViewModels;
using Windows.Storage.Pickers;

namespace PensionCompass.Views;

public sealed partial class DataPreparationView : Page
{
    public DataPreparationViewModel ViewModel { get; } = new();

    public DataPreparationView()
    {
        InitializeComponent();
        ViewModel.LoadFromAppState();
    }

    private async void LoadHtmlButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = CreateHtmlPicker();
        var files = await picker.PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;

        await ViewModel.ImportHtmlAsync(files.Select(f => f.Path).ToList(), replaceExisting: true);
    }

    private async void MergeHtmlButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = CreateHtmlPicker();
        var files = await picker.PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;

        await ViewModel.ImportHtmlAsync(files.Select(f => f.Path).ToList(), replaceExisting: false);
    }

    private async void SaveCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder == null) return;
        await ViewModel.SaveCsvAsync(folder);
    }

    private async void LoadCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder == null) return;
        await ViewModel.LoadCsvAsync(folder);
    }

    private async void ResetCatalogButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "카탈로그 초기화 확인",
            Content = "불러온 상품 카탈로그를 모두 삭제합니다. 다음 실행 시에도 복원되지 않습니다. 진행할까요?",
            PrimaryButtonText = "초기화",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            ViewModel.ResetCatalog();
    }

    private async void LoadPortfolioHtmlButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = CreateHtmlPicker();
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var snapshot = await ViewModel.ParsePortfolioHtmlAsync(file.Path);
        if (snapshot is null) return;
        await ConfirmAndApplyPortfolioAsync(snapshot);
    }

    private async void LoadPortfolioCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is null) return;

        var snapshot = await ViewModel.ParsePortfolioCsvAsync(folder);
        if (snapshot is null) return;
        await ConfirmAndApplyPortfolioAsync(snapshot);
    }

    private async void SavePortfolioCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is null) return;
        await ViewModel.SavePortfolioCsvAsync(folder);
    }

    private async void PortfolioGuideButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "삼성생명 HTML 받는 방법",
            Content = BuildPortfolioGuideContent(),
            CloseButtonText = "닫기",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async System.Threading.Tasks.Task ConfirmAndApplyPortfolioAsync(PensionCompass.Core.Models.PortfolioSnapshot snapshot)
    {
        if (ViewModel.AccountHasUserData())
        {
            var confirm = new ContentDialog
            {
                Title = "내 계좌 덮어쓰기 확인",
                Content = $"기존 계좌 합계와 보유 상품 {AppStateAccountHoldingsCount()}개를 모두 새 데이터로 교체합니다. 매도 가능 체크는 모두 기본값(매도 가능)으로 초기화됩니다. 진행할까요?",
                PrimaryButtonText = "덮어쓰기",
                CloseButtonText = "취소",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };
            var result = await confirm.ShowAsync();
            if (result != ContentDialogResult.Primary) return;
        }
        ViewModel.ApplyPortfolio(snapshot);
    }

    private static int AppStateAccountHoldingsCount()
        => Services.AppState.Instance.Account.OwnedItems.Count;

    private static UIElement BuildPortfolioGuideContent()
    {
        var panel = new StackPanel { Spacing = 12 };

        panel.Children.Add(MakeHeading("내 계좌 (보유 포트폴리오) HTML"));
        panel.Children.Add(MakeBody(
            "Chrome에서 삼성생명 홈페이지에 로그인 → My삼성생명 → 퇴직연금 종합관리 → " +
            "‘총 적립금’ 클릭 → ‘다른 이름으로 저장’으로 HTML 파일을 받습니다."));

        panel.Children.Add(MakeHeading("상품 카탈로그 HTML (참고)"));
        panel.Children.Add(MakeBody(
            "My삼성생명 → 퇴직연금 상품운용 → 퇴직연금 전체상품 → 펀드 → " +
            "정렬 기준 선택(이 시점에 선택한 기간이 펀드 수익률 컬럼에 반영됩니다 — 1개월/3개월/6개월/1년/3년 중 하나) → " +
            "‘다른 이름으로 저장’. 모든 기간을 채우려면 정렬을 바꿔가며 여러 번 저장해 ‘다른 기간 HTML 추가 머지’ 버튼으로 합쳐야 합니다."));

        panel.Children.Add(MakeBody(
            "Tip: 저장 전에 ‘나의 투자 성향’을 ‘공격투자형’으로 설정해 두면 카탈로그에 노출되는 상품 폭이 가장 넓어집니다."));

        return panel;
    }

    private static TextBlock MakeHeading(string text) => new()
    {
        Text = text,
        Style = (Microsoft.UI.Xaml.Style)Microsoft.UI.Xaml.Application.Current.Resources["BodyStrongTextBlockStyle"],
    };

    private static TextBlock MakeBody(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Style = (Microsoft.UI.Xaml.Style)Microsoft.UI.Xaml.Application.Current.Resources["BodyTextBlockStyle"],
    };

    private static async System.Threading.Tasks.Task<string?> PickFolderAsync()
    {
        if (App.Window is null) return null;

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");
        WindowHelper.Initialize(picker, App.Window);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private static FileOpenPicker CreateHtmlPicker()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.Downloads,
        };
        picker.FileTypeFilter.Add(".html");
        picker.FileTypeFilter.Add(".htm");
        if (App.Window is { } window)
            WindowHelper.Initialize(picker, window);
        return picker;
    }
}
