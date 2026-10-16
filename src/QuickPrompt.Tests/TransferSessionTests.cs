using QuickPrompt.Local;
using Xunit;
namespace QuickPrompt.Web.Tests;
public sealed class TransferSessionTests
{
    [Fact]public void TransferAuditIsDurableIdempotentAndExcludedFromLocalInference()
    {
        var folder=Path.Combine(Path.GetTempPath(),"quickprompt-transfer-audit-"+Guid.NewGuid());try{
            var path=Path.Combine(folder,"test.sqlite");var store=new QuickPrompt.Web.Infrastructure.LocalStore(path);
            var a=new WebTransferActivity(Guid.NewGuid(),null,null,ProviderId.ChatGptWeb,TransferAction.PrepareOnly,TransferState.NeedsManualPaste,TransferPhase.COPIED_OPENED,"copied-manual-paste",0,"1.0.0",DateTime.UtcNow);
            store.RecordTransfer(a);store.RecordTransfer(a);Assert.Single(new QuickPrompt.Web.Infrastructure.LocalStore(path).Transfers());Assert.Empty(store.Executions());var archive=store.Export();store.ClearHistory();Assert.Empty(store.Transfers());store.Import(archive,true);Assert.Equal(a,store.Transfers().Single());Assert.DoesNotContain("compiledPrompt",archive);
        }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [Fact]public void PreparationCannotBecomeSubmissionAndTerminalOutcomeCannotBeReplayed()
    {
        var s=new WebTransferSession(Guid.NewGuid(),TransferAction.PrepareOnly);s.Advance(TransferPhase.PREPARED);s.Advance(TransferPhase.TRANSFERRING);s.Observe(new(s.RequestId,TransferState.Prepared,"exact-editor-match"));Assert.Equal(TransferPhase.INSERTED,s.Phase);Assert.Throws<ValidationException>(()=>s.Advance(TransferPhase.SUBMISSION_REQUESTED));Assert.Equal(4,s.Trace.Count);
    }
    [Fact]public void ExplicitSendRequiresInsertionAndRecordsOnlyUiConfirmation()
    {
        var s=new WebTransferSession(Guid.NewGuid(),TransferAction.PrepareAndSend);s.Advance(TransferPhase.PREPARED);s.Advance(TransferPhase.TRANSFERRING);Assert.Throws<ValidationException>(()=>s.Advance(TransferPhase.SUBMITTED_UI));s.Observe(new(s.RequestId,TransferState.Prepared,"exact-editor-match"));s.Observe(new(s.RequestId,TransferState.SubmissionRequested,"one-ui-attempt"));s.Observe(new(s.RequestId,TransferState.SubmittedUI,"ui-transition-observed"));Assert.Equal(TransferPhase.SUBMITTED_UI,s.Phase);Assert.Throws<ValidationException>(()=>s.Advance(TransferPhase.TRANSFERRING));
    }
    [Fact]public void FallbackIsCopiedOpenedNeverSubmittedAndForeignResultsAreRejected()
    {
        var s=new WebTransferSession(Guid.NewGuid(),TransferAction.PrepareOnly);s.Advance(TransferPhase.PREPARED);Assert.Throws<ValidationException>(()=>s.Observe(new(Guid.NewGuid(),TransferState.SubmittedUI,"forged")));s.Observe(new(s.RequestId,TransferState.NeedsManualPaste,"copied-manual-paste"));Assert.Equal(TransferPhase.COPIED_OPENED,s.Phase);
    }
    [Theory][InlineData("extension-disconnected")][InlineData("cancel-outcome-unknown")][InlineData("submission-unconfirmed")][InlineData("editor-text-mismatch")]
    public void LostConfirmationIsUnknownNotAFalseFailedOrUnsentGuarantee(string code)=>Assert.Equal(TransferPhase.UNKNOWN,WebTransferSession.PhaseFor(new(Guid.NewGuid(),TransferState.Failed,code)));
}
