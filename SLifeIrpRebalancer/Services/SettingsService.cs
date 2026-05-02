using Windows.Storage;

namespace SLifeIrpRebalancer.Services;

/// <summary>
/// Persists user-level settings (AI provider choice, API key, lifelong-annuity flag) across runs.
/// Backing store is <see cref="ApplicationData.Current"/>.LocalSettings, which lives under the
/// per-user packaged-app data folder. The API key is stored in plain text — adequate for a
/// single-user personal app, but if multi-user / shared-machine scenarios become relevant,
/// migrate to <c>Windows.Security.Credentials.PasswordVault</c> for OS-level encryption.
/// </summary>
public sealed class SettingsService
{
    private const string AiProviderKey = "AiProvider";
    private const string ApiKeyKey = "ApiKey";
    private const string RestrictToSamsungLifeKey = "RestrictToSamsungLifeForLifelongAnnuity";

    private readonly ApplicationDataContainer _store = ApplicationData.Current.LocalSettings;

    public string AiProvider
    {
        get => _store.Values[AiProviderKey] as string ?? "Claude";
        set => _store.Values[AiProviderKey] = value;
    }

    public string ApiKey
    {
        get => _store.Values[ApiKeyKey] as string ?? string.Empty;
        set => _store.Values[ApiKeyKey] = value;
    }

    public bool RestrictToSamsungLifeForLifelongAnnuity
    {
        get => _store.Values[RestrictToSamsungLifeKey] is bool b && b;
        set => _store.Values[RestrictToSamsungLifeKey] = value;
    }
}
