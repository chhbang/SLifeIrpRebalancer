using CommunityToolkit.Mvvm.ComponentModel;
using SLifeIrpRebalancer.Services;

namespace SLifeIrpRebalancer.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private SettingsService Settings => AppState.Instance.Settings;

    /// <summary>
    /// Mirrors the <see cref="Microsoft.UI.Xaml.Controls.RadioButtons.SelectedIndex"/> for the
    /// provider list (0=Claude, 1=Gemini, 2=GPT). The string label is what gets persisted.
    /// </summary>
    public int ProviderIndex
    {
        get => Settings.AiProvider switch
        {
            "Claude" => 0,
            "Gemini" => 1,
            "GPT" => 2,
            _ => 0,
        };
        set
        {
            var label = value switch
            {
                0 => "Claude",
                1 => "Gemini",
                2 => "GPT",
                _ => "Claude",
            };
            if (Settings.AiProvider == label) return;
            Settings.AiProvider = label;
            OnPropertyChanged();
        }
    }

    public string ApiKey
    {
        get => Settings.ApiKey;
        set
        {
            if (Settings.ApiKey == value) return;
            Settings.ApiKey = value;
            OnPropertyChanged();
        }
    }

    public bool RestrictToSamsungLifeForLifelongAnnuity
    {
        get => Settings.RestrictToSamsungLifeForLifelongAnnuity;
        set
        {
            if (Settings.RestrictToSamsungLifeForLifelongAnnuity == value) return;
            Settings.RestrictToSamsungLifeForLifelongAnnuity = value;
            OnPropertyChanged();
        }
    }
}
