using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SLifeIrpRebalancer.Core.Markdown;
using SLifeIrpRebalancer.Services;
using SLifeIrpRebalancer.ViewModels;
using Windows.Storage.Pickers;

namespace SLifeIrpRebalancer.Views;

public sealed partial class AiRebalanceView : Page
{
    public AiRebalanceViewModel ViewModel { get; } = new();

    public AiRebalanceView()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += AiRebalanceView_Loaded;
    }

    private async void AiRebalanceView_Loaded(object sender, RoutedEventArgs e)
    {
        // CoreWebView2 must be initialized before NavigateToString works.
        await ResponseWebView.EnsureCoreWebView2Async();
        if (ViewModel.HasResponse)
            UpdateWebViewContent(ViewModel.AiResponse);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AiRebalanceViewModel.AiResponse))
            UpdateWebViewContent(ViewModel.AiResponse);
    }

    private void UpdateWebViewContent(string markdown)
    {
        if (ResponseWebView.CoreWebView2 == null) return;
        var html = MarkdownToHtml.Convert(markdown);
        ResponseWebView.NavigateToString(html);
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.GenerateProposalAsync();
    }

    private async void ExportPdfButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.Window is null) return;

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"리밸런싱_제안_{System.DateTime.Now:yyyyMMdd_HHmm}",
        };
        picker.FileTypeChoices.Add("PDF 문서", [".pdf"]);
        WindowHelper.Initialize(picker, App.Window);

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        await ViewModel.ExportPdfAsync(file.Path);
    }
}
