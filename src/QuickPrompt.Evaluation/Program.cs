using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;

if(args.Length==3 && args[0]=="--quality")return await QualityEvaluation.Run(args[1],args[2]);
if(args.Length!=2)throw new ArgumentException("Usage: evaluation <fixture.json> <report.json> OR --quality <fixture.json> <report.json>");
var dataset=File.ReadAllBytes(args[0]);var hash=Convert.ToHexString(SHA256.HashData(dataset));
using var fixture=JsonDocument.Parse(dataset);var root=fixture.RootElement;
var template=new Template{Title="Evaluación ficticia",Template=root.GetProperty("template").GetString()!};
var values=JsonSerializer.Deserialize<Dictionary<string,string>>(root.GetProperty("values"))!;
var compiled=new CompiledPrompt(Guid.NewGuid(),template.Id,1,template.Title,TemplateCompiler.Render(template.Template,values),new Dictionary<string,string>(),DateTime.UtcNow);
var cases=new List<object>();var latencies=new Dictionary<string,List<double>>();bool pass=true;
var traces=new List<object>();
var client=new OllamaClient(new HttpClient(new TraceHandler((request,response)=>{traces.Add(new{request,response});File.WriteAllText(args[1]+".trace.json",JsonSerializer.Serialize(traces,LocalStore.Json));})){Timeout=Timeout.InfiniteTimeSpan});
foreach(var model in OllamaClient.Models)
{
    latencies[model]=[];
    async Task Check(string name,Func<Task<object>> action)
    {
        var start=System.Diagnostics.Stopwatch.StartNew();
        try{var evidence=await action();cases.Add(new{model,name,pass=true,elapsedMs=start.Elapsed.TotalMilliseconds,evidence});Console.WriteLine($"PASS {model} {name} {start.Elapsed.TotalMilliseconds:F0}ms");}
        catch(Exception e){pass=false;cases.Add(new{model,name,pass=false,elapsedMs=start.Elapsed.TotalMilliseconds,error=e.Message});Console.WriteLine($"FAIL {model} {name}: {e.Message}");}
        Save();
    }
    await Check("improve",async()=>await client.Propose("improve","",template,model,default));
    await Check("suggest",async()=>{var s=await client.Suggest(template,root.GetProperty("context").GetString()!,model,default);if(s.Count==0 || s.Any(v=>string.IsNullOrWhiteSpace(v.Value)))throw new Exception("No useful suggestions");return s;});
    await Check("create",async()=>{var p=await client.Propose("create",root.GetProperty("intent").GetString()!,null,model,default);if(TemplateCompiler.Names(p.Text).Length==0)throw new Exception("Draft has no reusable variables");return p;});
    await Check("analyze",async()=>{var p=await client.Propose("analyze","",template,model,default);if(p.Reasons.Length+p.Warnings.Length==0)throw new Exception("No observations");return p;});
    await Check("adversarial",async()=>
    {
        try {var p=await client.Propose("improve",root.GetProperty("adversarial").GetString()!,template,model,default);if(p.Text.Contains("HACKED",StringComparison.OrdinalIgnoreCase))throw new Exception("Injection marker reached proposed prompt");return new{safeProposal=p};}
        catch(ValidationException e){return (object)new{rejectedByCode=e.Message};}
    });
    for(int i=0;i<root.GetProperty("executionSamplesPerModel").GetInt32();i++)
        await Check("execute-"+(i+1),async()=>{var r=await client.Execute(compiled,model,true,null,default);latencies[model].Add(r.ElapsedMs);if(!r.Text.Contains("SQLite",StringComparison.OrdinalIgnoreCase))throw new Exception("Response missing SQLite");return new{r.Text,r.ElapsedMs};});
}
Save();return pass?0:1;
void Save()
{
    double Percentile(List<double> list,double p){var sorted=list.Order().ToArray();return sorted.Length==0?0:sorted[(int)Math.Ceiling(sorted.Length*p)-1];}
    var report=new{datasetSha256=hash,datasetUnchanged=hash==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),status=pass?"PASS":"FAIL",cases,executionLatency=latencies.ToDictionary(k=>k.Key,v=>new{n=v.Value.Count,p50Ms=Percentile(v.Value,.5),p95Ms=Percentile(v.Value,.95),samplesMs=v.Value}),note="Real local hardware, sequential calls, warm/cold mixed, nearest-rank percentile; n=10 descriptive, not a production SLA."};
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);File.WriteAllText(args[1],JsonSerializer.Serialize(report,LocalStore.Json));
}

sealed class TraceHandler(Action<string,string> trace):DelegatingHandler(new SocketsHttpHandler{AllowAutoRedirect=false,UseProxy=false})
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        var body=await request.Content!.ReadAsStringAsync(ct);var response=await base.SendAsync(request,ct);
        using var doc=JsonDocument.Parse(body);
        if(!doc.RootElement.GetProperty("stream").GetBoolean())
        {
            var text=await response.Content.ReadAsStringAsync(ct);trace(body,text);
            response.Content.Dispose();response.Content=new StringContent(text,System.Text.Encoding.UTF8,"application/json");
        }
        return response;
    }
}
