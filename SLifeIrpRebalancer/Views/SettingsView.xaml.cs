using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SLifeIrpRebalancer.ViewModels;

namespace SLifeIrpRebalancer.Views;

public sealed partial class SettingsView : Page
{
    public SettingsViewModel ViewModel { get; } = new();

    public SettingsView()
    {
        InitializeComponent();
        // PasswordBox.Password is intentionally not bindable in WinUI 3 (security policy),
        // so we initialize it imperatively and forward changes through PasswordChanged.
        ApiKeyBox.Password = ViewModel.ApiKey;
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = ApiKeyBox.Password;
    }
}
