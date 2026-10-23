using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

// Trusted host opt-in only. Observations never authorize actions or change results.
public sealed class DevelopmentLabObserver : IDisposable
{
    private readonly object gate = new();
    private readonly Timer heartbeat;
    private readonly NovaTelemetry.DebugCapture debug;
    private readonly string sourceRoot;
    private bool disposed;
    public string DirectoryPath { get; }
    public string CorePath => Path.Combine(DirectoryPath, "core.jsonl");
    public string JournalPath => Path.Combine(DirectoryPath, "app.jsonl");
    public int Errors { get; private set; }
    public bool CaptureSyntheticContent { get; }
    public static bool Allowed(bool isDevelopment, bool requested) => requested && isDevelopment;
    public DevelopmentLabObserver(bool isDevelopment, string directory, string sourceRoot, bool captureSyntheticContent=false)
    {
        if (!isDevelopment) throw new InvalidOperationException("Lab observation is only available in Development.");
        if (!Path.IsPathFullyQualified(directory) || directory.StartsWith(@"\\")) throw new InvalidOperationException("Configure an absolute local Lab directory.");
        DirectoryPath = Path.GetFullPath(directory); this.sourceRoot = Path.GetFullPath(sourceRoot);
        CaptureSyntheticContent=captureSyntheticContent;
        Directory.CreateDirectory(DirectoryPath);
        debug = NovaTelemetry.CaptureDebugPoints(4096, point => Emit(point.RunId, "domain_checkpoint", "validation",
            point.Rejected ? "FAIL" : "OBSERVED", new { boundary = point.Boundary, rejected = point.Rejected,
                facts = point.Values.Where(p => p.Value is null or bool or int or long or double or Guid).ToDictionary(p => p.Key,p => p.Value) },
            file: ResolveSource(point.Values.GetValueOrDefault("code.file") as string),
            line: point.Values.GetValueOrDefault("code.line") as int? ?? 0,
            symbol: point.Values.GetValueOrDefault("code.member") as string ?? ""));
        heartbeat = new Timer(_ => Emit(null,"host_heartbeat","inspection","OBSERVED",new { pid=Environment.ProcessId,debug.Dropped,debug.ObserverErrors,observationErrors=Errors }),null,TimeSpan.Zero,TimeSpan.FromSeconds(2));
    }
    private string ResolveSource(string? basename)
    {
        if (basename is null || Path.GetFileName(basename)!=basename) return "";
        var found = new List<string>();
        foreach (var folder in new[] { "backend", "src" }) {
            var root = Path.Combine(sourceRoot,folder); if (!Directory.Exists(root)) continue;
            found.AddRange(Directory.EnumerateFiles(root,basename,SearchOption.AllDirectories)
                .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")));
        }
        return found.Count==1 ? found[0] : "";
    }
    public void ConfigureWorker(ProcessStartInfo start, Guid? runId, string requestId)
    {
        foreach (var arg in new[] { "--events",CorePath,"--campaign-id","quickprompt-app","--case-id",(runId?.ToString() ?? requestId) }) start.ArgumentList.Add(arg);
        if(CaptureSyntheticContent)start.ArgumentList.Add("--public-structured-output");
    }
    public void Request(LiveStartInput input)=>Emit(input.RunId,"human_request","input","OBSERVED",
        new { model=input.Model,sourceId=input.SourceId,input.SourceRevision },
        inputs:CaptureSyntheticContent?new { request=input.Message,values=input.Values }:null);
    public void Inference(Guid? runId,string purpose,JsonElement request)=>Emit(runId,"model_request","input","OBSERVED",
        new { requestId=request.GetProperty("requestId"),purpose,model=request.GetProperty("model"),limits=request.GetProperty("limits") },
        inputs:CaptureSyntheticContent?new { messages=request.GetProperty("messages"),schema=request.GetProperty("outputSchema") }:null);
    public void Receipt(Guid runId,UiActionReceipt receipt)=>Emit(runId,"ui_receipt","tools",
        receipt.ErrorCode is null?"OBSERVED":"FAIL",new { receipt.ActionId,receipt.Sequence,receipt.ExecutionStatus,receipt.VerificationStatus,receipt.ErrorCode,
            state=CaptureSyntheticContent?receipt.Observation:null });
    public void Live(LiveRecord record, LiveEvent? item=null, bool created=false)
    {
        var terminal=LiveRuns.Terminal(record.Status);
        var phase=item?.Stage is "SemanticInterpretation" or "Model" or "ModelRetry" or "Generation" ? "inference"
            : item?.Stage is "Verification" ? "verification" : item?.ToolName is not null ? "tools" : "input";
        Emit(record.RunId,created ? "run_started" : terminal ? "run_finished" : "live_event",phase,
            record.Status is LiveStatus.FAILED or LiveStatus.BLOCKED ? "FAIL" : terminal ? "OBSERVED" : "RUNNING",
            new { appStatus=record.Status.ToString(),stage=item?.Stage,sequence=item?.Sequence ?? record.Sequence,
                tool=item?.ToolName,actor=item?.Actor.ToString(),targetId=item?.TargetId ?? record.TargetId,
                record.Revision,model=record.Model,record.EffectCommitted,record.Approval,actionId=item?.ActionId,
                actionSequence=item?.ActionSequence,durationMs=item?.DurationMs,errorCode=item?.ErrorCode,
                acceptance="Independent user-outcome oracle not supplied" });
    }
    public void Emit(Guid? runId,string kind,string phase,string status,object observed,
        object? inputs=null,
        [CallerFilePath] string file="",[CallerLineNumber] int line=0,[CallerMemberName] string symbol="")
    {
        try { lock(gate) {
            if (disposed) return;
            if (File.Exists(JournalPath) && new FileInfo(JournalPath).Length>16_000_000) { Errors++;return; }
            var entry=new { schema="ai-delivery-event/1",id=Guid.NewGuid().ToString("N"),observedAt=DateTime.UtcNow.ToString("o"),
                campaignId="quickprompt-app",runId=runId?.ToString(),caseId=runId?.ToString(),kind,phase,status,
                owner="QuickPrompt Development",location=string.IsNullOrEmpty(file)?null:new { file,line,symbol,kind="observed_checkpoint" },inputs,observed };
            File.AppendAllText(JournalPath,JsonSerializer.Serialize(entry)+"\n",new System.Text.UTF8Encoding(false));
        } } catch (Exception error) when(error is IOException or UnauthorizedAccessException) { Errors++; }
    }
    public void Dispose()
    {
        // Wait for the current append and close the writer before detaching
        // producers. Timer.Dispose alone does not wait for queued callbacks.
        lock (gate) {
            if (disposed) return;
            disposed = true;
        }
        heartbeat.Dispose();
        debug.Dispose();
    }
}

public sealed class ObservedLiveRunStore(ILiveRunStore inner,DevelopmentLabObserver observer) : ILiveRunStore
{
    public bool TryCreate(LiveRecord record) { var added=inner.TryCreate(record);if(added)observer.Live(record,created:true);return added; }
    public LiveRecord? Get(Guid id)=>inner.Get(id);
    public LiveEvent[] Events(Guid id,long after)=>inner.Events(id,after);
    public LiveEvent Append(LiveRecord record,LiveEvent item) { var saved=inner.Append(record,item);observer.Live(record with { Sequence=saved.Sequence,Status=saved.Status },saved);return saved; }
}
