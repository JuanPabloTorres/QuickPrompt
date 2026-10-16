using System.Text.Json.Serialization;

namespace QuickPrompt.Local;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransferPhase { CREATED, PREPARED, TRANSFERRING, INSERTED, FAILED, BLOCKED, UNKNOWN, COPIED_OPENED, SUBMISSION_REQUESTED, SUBMITTED_UI, CANCELLED }

// Metadata-only state machine. A UI submission never means delivery or a model response.
public sealed class WebTransferSession
{
    public Guid RequestId { get; }
    public TransferAction Action { get; }
    public TransferPhase Phase { get; private set; } = TransferPhase.CREATED;
    private readonly List<TransferPhase> trace=[TransferPhase.CREATED];
    public IReadOnlyList<TransferPhase> Trace=>trace;
    public WebTransferSession(Guid requestId,TransferAction action)
    {if(requestId==Guid.Empty||!Enum.IsDefined(action))throw new ValidationException("Invalid transfer session.");RequestId=requestId;Action=action;}
    public void Advance(TransferPhase next)
    {
        if(next==Phase)return;
        var permitted=Phase switch {
            TransferPhase.CREATED=>next is TransferPhase.PREPARED or TransferPhase.FAILED or TransferPhase.BLOCKED or TransferPhase.CANCELLED,
            TransferPhase.PREPARED=>next is TransferPhase.TRANSFERRING or TransferPhase.FAILED or TransferPhase.BLOCKED or TransferPhase.UNKNOWN or TransferPhase.COPIED_OPENED or TransferPhase.CANCELLED,
            TransferPhase.TRANSFERRING=>next is TransferPhase.INSERTED or TransferPhase.FAILED or TransferPhase.BLOCKED or TransferPhase.UNKNOWN or TransferPhase.CANCELLED,
            TransferPhase.INSERTED=>next is TransferPhase.SUBMISSION_REQUESTED or TransferPhase.UNKNOWN or TransferPhase.BLOCKED or TransferPhase.FAILED or TransferPhase.CANCELLED,
            TransferPhase.SUBMISSION_REQUESTED=>next is TransferPhase.SUBMITTED_UI or TransferPhase.UNKNOWN or TransferPhase.BLOCKED or TransferPhase.FAILED,
            _=>false};
        if(!permitted || (next is TransferPhase.SUBMISSION_REQUESTED or TransferPhase.SUBMITTED_UI && Action!=TransferAction.PrepareAndSend))throw new ValidationException("Invalid transfer state transition.");
        Phase=next;trace.Add(next);
    }
    public void Observe(PromptTransferResult result)
    {
        if(result.RequestId!=RequestId)throw new ValidationException("Transfer result does not match request.");
        var next=PhaseFor(result);
        if(Phase is TransferPhase.FAILED or TransferPhase.BLOCKED or TransferPhase.UNKNOWN or TransferPhase.COPIED_OPENED or TransferPhase.SUBMITTED_UI or TransferPhase.CANCELLED)return;
        Advance(next);
    }
    public static TransferPhase PhaseFor(PromptTransferResult r)=>r.Code switch {
        "copied-manual-paste"=>TransferPhase.COPIED_OPENED,
        "extension-disconnected" or "cancel-outcome-unknown" or "submission-unconfirmed" or "cancelled-after-ui-attempt" or "bridge-failed" or "editor-text-mismatch" or "editor-replaced"=>TransferPhase.UNKNOWN,
        "editor-not-empty" or "provider-busy"=>TransferPhase.BLOCKED,
        _=>r.State switch {
            TransferState.Ready=>TransferPhase.PREPARED,
            TransferState.ProviderTabOpening or TransferState.WaitingForEditor=>TransferPhase.TRANSFERRING,
            TransferState.Prepared=>TransferPhase.INSERTED,
            TransferState.SubmissionRequested=>TransferPhase.SUBMISSION_REQUESTED,
            TransferState.SubmittedUI=>TransferPhase.SUBMITTED_UI,
            TransferState.ExtensionMissing or TransferState.LoginRequired or TransferState.BlockedByProvider=>TransferPhase.BLOCKED,
            TransferState.NeedsManualPaste or TransferState.Timeout=>TransferPhase.UNKNOWN,
            TransferState.Cancelled=>TransferPhase.CANCELLED,
            _=>TransferPhase.FAILED}};
}
public record WebTransferActivity(Guid Id,Guid? TemplateId,int? Revision,ProviderId Provider,TransferAction Action,
    TransferState State,TransferPhase Phase,string Code,double DurationMs,string AdapterVersion,DateTime CreatedAt);
