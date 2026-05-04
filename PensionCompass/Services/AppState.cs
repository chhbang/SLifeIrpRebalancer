using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PensionCompass.Core.History;
using PensionCompass.Core.Models;
using Windows.Storage;

namespace PensionCompass.Services;

/// <summary>
/// Process-wide state shared across the five screens — a poor-man's DI container.
/// On first access, restores the last-saved Account + Catalog from disk via <see cref="StateStore"/>;
/// catalog assignments auto-persist via the source-generated <c>OnCatalogChanged</c> hook,
/// account mutations require an explicit <see cref="SaveAccount"/> call from the VM that touched them.
/// </summary>
public sealed partial class AppState : ObservableObject
{
    public static AppState Instance { get; } = new();

    [ObservableProperty]
    private ProductCatalog? _catalog;

    public AccountStatusModel Account { get; private set; } = new();

    public SettingsService Settings { get; } = new();

    /// <summary>StateStore needs the sync folder path; Settings owns it. Built in the
    /// constructor so the Settings field is already initialized when we reference it.</summary>
    private readonly StateStore _store;

    private AppState()
    {
        _store = new StateStore(syncFolderProvider: () => Settings.SyncFolder);

        if (_store.LoadAccount() is { } persistedAccount)
            Account = persistedAccount;

        if (_store.LoadCatalog() is { } persistedCatalog)
            _catalog = persistedCatalog; // backing field directly to avoid re-saving during load

        MigrateLifelongAnnuityFromSettings();
    }

    /// <summary>Exposes the configured sync folder root (or null when not set) for components
    /// that need to write history files alongside the state mirror.</summary>
    public string? SyncFolderRoot
        => string.IsNullOrWhiteSpace(Settings.SyncFolder) ? null : Settings.SyncFolder;

    /// <summary>
    /// One-shot hand-off slot used by the History → AI Rebalance flow. The History screen sets
    /// this and navigates; the AI Rebalance VM reads it once on construction (which clears it),
    /// pre-selecting the picked session in its "이전 회차 참고" combo.
    /// </summary>
    public RebalanceSessionEntry? PendingPriorEntry { get; set; }

    public RebalanceSessionEntry? ConsumePendingPriorEntry()
    {
        var entry = PendingPriorEntry;
        PendingPriorEntry = null;
        return entry;
    }

    /// <summary>
    /// The folder we WRITE rebalance history sessions into: <c>&lt;syncFolder&gt;\History</c> when
    /// sync is configured, otherwise <c>&lt;LocalState&gt;\History</c>. The folder isn't created here —
    /// <see cref="RebalanceHistoryStore.Save"/> creates it on first save.
    /// </summary>
    public string ActiveHistoryFolder
        => Path.Combine(SyncFolderRoot ?? ApplicationData.Current.LocalFolder.Path, RebalanceHistoryStore.HistoryFolderName);

    /// <summary>
    /// Roots to SCAN when listing past sessions — both LocalState and (if configured and different)
    /// the sync folder. This way a user changing the SyncFolder setting later doesn't appear to
    /// "lose" history written under the old setting.
    /// </summary>
    public IEnumerable<string> CandidateHistoryRoots()
    {
        yield return ApplicationData.Current.LocalFolder.Path;
        if (SyncFolderRoot is { } sync) yield return sync;
    }

    /// <summary>
    /// Earlier builds stored the lifelong-annuity preference in LocalSettings; it's now part of
    /// <see cref="AccountStatusModel.WantsLifelongAnnuity"/> (subscriber info, not app config).
    /// Migrate the legacy value once on the first launch after upgrade, then drop the LocalSettings entry.
    /// </summary>
    private void MigrateLifelongAnnuityFromSettings()
    {
        if (Settings.ReadLegacyLifelongAnnuityFlag() is not { } legacy) return;
        Account.WantsLifelongAnnuity = legacy;
        _store.SaveAccount(Account);
        Settings.ClearLegacyLifelongAnnuityFlag();
    }

    /// <summary>Persists current Account snapshot. Call after any field/list mutation in MyAccount or SellTargets VMs.</summary>
    public void SaveAccount() => _store.SaveAccount(Account);

    /// <summary>Wipes the persisted account snapshot and replaces the in-memory model with a fresh one.</summary>
    public void ResetAccount()
    {
        Account = new AccountStatusModel();
        _store.DeleteAccount();
    }

    /// <summary>Wipes the persisted catalog snapshot and clears the in-memory catalog.</summary>
    public void ResetCatalog()
    {
        Catalog = null; // triggers OnCatalogChanged → DeleteCatalog
    }

    partial void OnCatalogChanged(ProductCatalog? value)
    {
        if (value == null)
            _store.DeleteCatalog();
        else
            _store.SaveCatalog(value);
    }
}
