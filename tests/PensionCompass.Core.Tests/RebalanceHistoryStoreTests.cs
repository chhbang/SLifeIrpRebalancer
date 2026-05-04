using PensionCompass.Core.Ai;
using PensionCompass.Core.History;
using PensionCompass.Core.Models;

namespace PensionCompass.Core.Tests;

public class RebalanceHistoryStoreTests
{
    [Fact]
    public void Save_Then_Load_RoundTripsAllFields()
    {
        var session = MakeSession("Claude", "claude-opus-4-7", new DateTime(2026, 5, 4, 15, 30, 22));

        using var temp = TempFolder.Create();
        var path = RebalanceHistoryStore.Save(temp.Path, session);
        var loaded = RebalanceHistoryStore.Load(path);

        Assert.NotNull(loaded);
        Assert.Equal(session.Meta.Timestamp, loaded!.Meta.Timestamp);
        Assert.Equal(session.Meta.ProviderName, loaded.Meta.ProviderName);
        Assert.Equal(session.Meta.ModelId, loaded.Meta.ModelId);
        Assert.Equal(session.Meta.ThinkingLevel, loaded.Meta.ThinkingLevel);
        Assert.Equal(session.Meta.HoldingsCount, loaded.Meta.HoldingsCount);
        Assert.Equal(session.Meta.TotalAmount, loaded.Meta.TotalAmount);
        Assert.Equal(session.UserAdditionalQuery, loaded.UserAdditionalQuery);
        Assert.Equal(session.RecommendationMarkdown, loaded.RecommendationMarkdown);
        Assert.Equal(session.Account.TotalAmount, loaded.Account.TotalAmount);
        Assert.Equal(session.Account.OwnedItems.Count, loaded.Account.OwnedItems.Count);
        Assert.Equal(session.Account.OwnedItems[0].ProductName, loaded.Account.OwnedItems[0].ProductName);
    }

    [Fact]
    public void List_ReturnsNewestFirst_AcrossMultipleRoots()
    {
        using var rootA = TempFolder.Create();
        using var rootB = TempFolder.Create();
        Directory.CreateDirectory(Path.Combine(rootA.Path, RebalanceHistoryStore.HistoryFolderName));
        Directory.CreateDirectory(Path.Combine(rootB.Path, RebalanceHistoryStore.HistoryFolderName));

        // Older session in root A, newer session in root B — listing should put B first.
        RebalanceHistoryStore.Save(
            Path.Combine(rootA.Path, RebalanceHistoryStore.HistoryFolderName),
            MakeSession("Claude", "claude-opus-4-7", new DateTime(2026, 1, 1, 10, 0, 0)));
        RebalanceHistoryStore.Save(
            Path.Combine(rootB.Path, RebalanceHistoryStore.HistoryFolderName),
            MakeSession("Gemini", "gemini-3.0-pro", new DateTime(2026, 5, 1, 10, 0, 0)));

        var entries = RebalanceHistoryStore.List([rootA.Path, rootB.Path]);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Gemini", entries[0].Meta.ProviderName);
        Assert.Equal("Claude", entries[1].Meta.ProviderName);
    }

    [Fact]
    public void List_DeduplicatesByCanonicalPath()
    {
        // If the same root is passed twice, the same files mustn't be listed twice.
        using var root = TempFolder.Create();
        Directory.CreateDirectory(Path.Combine(root.Path, RebalanceHistoryStore.HistoryFolderName));
        RebalanceHistoryStore.Save(
            Path.Combine(root.Path, RebalanceHistoryStore.HistoryFolderName),
            MakeSession("Claude", "claude-opus-4-7", new DateTime(2026, 3, 15, 12, 0, 0)));

        var entries = RebalanceHistoryStore.List([root.Path, root.Path]);

        Assert.Single(entries);
    }

    [Fact]
    public void Save_FileNameHasSortableTimestampAndProvider()
    {
        var session = MakeSession("Claude", "claude-opus-4-7", new DateTime(2026, 5, 4, 15, 30, 22, DateTimeKind.Local));

        using var temp = TempFolder.Create();
        var path = RebalanceHistoryStore.Save(temp.Path, session);

        var fileName = Path.GetFileName(path);
        Assert.StartsWith("2026-05-04_153022_", fileName);
        Assert.EndsWith(".json", fileName);
        Assert.Contains("Claude", fileName);
    }

    [Fact]
    public void SavedJson_DoesNotLeakUnexpectedFields()
    {
        // Guard against accidental field additions that could inflate the file with PII or
        // computed-property output. Whitelist of property names allowed at any nesting level.
        var session = MakeSession("Claude", "claude-opus-4-7", new DateTime(2026, 5, 4, 15, 30, 22));
        using var temp = TempFolder.Create();
        var path = RebalanceHistoryStore.Save(temp.Path, session);

        var json = File.ReadAllText(path);

        // The reference subscriber name and account number from the portfolio HTML must not
        // appear here under any circumstance — they were never input to the parser/prompt.
        Assert.DoesNotContain("방창환", json);
        Assert.DoesNotContain("9641179110049", json);
        // API key shapes shouldn't be in here either (defensive — they live in PasswordVault).
        Assert.DoesNotContain("sk-ant-", json);
        Assert.DoesNotContain("AIza", json);
    }

    [Fact]
    public void Delete_RemovesFile_AndReturnsFalseWhenAbsent()
    {
        var session = MakeSession("Claude", "claude-opus-4-7", DateTime.Now);
        using var temp = TempFolder.Create();
        var path = RebalanceHistoryStore.Save(temp.Path, session);

        Assert.True(File.Exists(path));
        Assert.True(RebalanceHistoryStore.Delete(path));
        Assert.False(File.Exists(path));
        Assert.False(RebalanceHistoryStore.Delete(path));
    }

    [Fact]
    public void Entry_DisplayLabel_IncludesTimestampProviderAndModel()
    {
        var meta = new RebalanceSessionMeta(
            Timestamp: new DateTime(2026, 5, 4, 15, 30, 0, DateTimeKind.Local),
            ProviderName: "Claude",
            ModelId: "claude-opus-4-7",
            ThinkingLevel: ThinkingLevel.High,
            HoldingsCount: 7,
            TotalAmount: 193_998_473m,
            CatalogPrincipalGuaranteedCount: 0,
            CatalogFundCount: 0);
        var entry = new RebalanceSessionEntry("/tmp/x.json", meta);

        Assert.Contains("2026-05-04 15:30", entry.DisplayLabel);
        Assert.Contains("Claude", entry.DisplayLabel);
        Assert.Contains("claude-opus-4-7", entry.DisplayLabel);
    }

    private static RebalanceSession MakeSession(string provider, string model, DateTime timestamp)
    {
        var account = new AccountStatusModel
        {
            TotalAmount = 100_000_000m,
            DepositAmount = 80_000_000m,
            ProfitAmount = 20_000_000m,
            CurrentAge = 50,
            DesiredAnnuityStartAge = 60,
            WantsLifelongAnnuity = false,
            RebalanceCycle = RebalanceCycle.SixMonths,
            OwnedItems =
            [
                new OwnedProductModel { ProductName = "이율보증형(3년)", CurrentValue = 50_000_000m, ReturnRate = 9.11m, AnnualizedReturn = 1.40m, InvestedDays = 2294, IsSellable = true },
                new OwnedProductModel { ProductName = "삼성밀당다람쥐글로벌EMP증권자투자신탁", CurrentValue = 50_000_000m, ReturnRate = 20.21m, IsSellable = true },
            ],
        };

        var meta = new RebalanceSessionMeta(
            Timestamp: timestamp,
            ProviderName: provider,
            ModelId: model,
            ThinkingLevel: ThinkingLevel.High,
            HoldingsCount: account.OwnedItems.Count,
            TotalAmount: account.TotalAmount,
            CatalogPrincipalGuaranteedCount: 12,
            CatalogFundCount: 47);

        return new RebalanceSession(
            Meta: meta,
            Account: account,
            UserAdditionalQuery: "이번엔 채권 비중을 좀 늘려주세요",
            RecommendationMarkdown: "# 리밸런싱 제안\n\n## 매도 후보\n...");
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; }
        private TempFolder(string path) { Path = path; }
        public static TempFolder Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PensionCompass_HistoryTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempFolder(path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ }
        }
    }
}
