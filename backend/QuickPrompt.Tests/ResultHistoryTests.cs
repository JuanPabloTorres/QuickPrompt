using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ResultHistoryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nova-result-history-" + Guid.NewGuid());
    private LocalStore Store() => new(Path.Combine(directory, "synthetic.sqlite"));
    [Fact] public void ReviewedClearIsAtomicRejectsNewHistoryAndPreservesTemplatesAndPreferences()
    {
        var store = Store();
        var template = store.Save(new Template { Id = Guid.NewGuid(), Title = "Fuente", Template = "Explica <tema>." });
        store.Compile(template.Id, new() { ["tema"] = "C#\nΩ" });
        var review = store.ResultHistory(0);
        Assert.Throws<ValidationException>(() => store.ClearResultHistory(new(false, review.ExpectedSnapshot)));
        var second = Store();
        var later = second.Compile(template.Id, new() { ["tema"] = "Otro" });
        second.SaveExecution(new(Guid.NewGuid(), later.Id, "qwen3:8b", "Respuesta sintética", 10, DateTime.UtcNow));
        var before = second.Export();
        Assert.Throws<ConflictException>(() => store.ClearResultHistory(new(true, review.ExpectedSnapshot)));
        Assert.Equal(before, second.Export());
        var current = store.ResultHistory(0);
        Assert.Equal(2, current.Total); Assert.Equal(1, current.ResponseCount);
        Assert.Equal(template.Revision, Assert.Single(current.Items.Where(item => item.Id == later.Id)).Revision);
        var preferences = store.Settings(); var preserved = store.FindTemplate(template.Id)!;
        store.ClearResultHistory(new(true, current.ExpectedSnapshot));
        var reopened = Store(); Assert.Empty(reopened.History()); Assert.Empty(reopened.Executions());
        Assert.Equal(preferences, reopened.Settings()); Assert.Equal(preserved.Revision, reopened.FindTemplate(template.Id)!.Revision);
        Assert.Equal(preserved.Template, reopened.FindTemplate(template.Id)!.Template);
        Assert.Throws<ConflictException>(() => reopened.ClearResultHistory(new(true, current.ExpectedSnapshot)));
    }
    [Theory] [InlineData(-1)] [InlineData(100001)]
    public void InvalidPageDoesNotTouchStorage(int page)
    {
        var store = Store(); var before = store.Export();
        Assert.Throws<ValidationException>(() => store.ResultHistory(page)); Assert.Equal(before, store.Export());
    }
    [Fact] public void ActiveVisibleRunBlocksClearingTheReviewedHistory()
    {
        var store = Store();
        var template = store.Save(new Template { Id = Guid.NewGuid(), Title = "Fuente", Template = "Texto." });
        store.Compile(template.Id, []); var review = store.ResultHistory(0);
        Assert.True(store.TryCreateLiveRun(new(Guid.NewGuid(), Guid.NewGuid(), "test-fingerprint", "SAME_TAB", LiveStatus.CREATED, 0, DateTime.UtcNow)));
        Assert.Throws<ConflictException>(() => store.ClearResultHistory(new(true, review.ExpectedSnapshot)));
        Assert.Single(store.History());
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
