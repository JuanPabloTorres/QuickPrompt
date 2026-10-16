using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;

internal static class QualityEvaluation
{
    public static async Task<int> Run(string fixturePath,string output)
    {
        var original=File.ReadAllBytes(fixturePath);var hash=Convert.ToHexString(SHA256.HashData(original));using var doc=JsonDocument.Parse(original);var f=doc.RootElement;
        var records=new List<object>();var timings=new Dictionary<string,List<double>>();bool all=true;
        using var http=new HttpClient(new SocketsHttpHandler{AllowAutoRedirect=false,UseProxy=false}){Timeout=Timeout.InfiniteTimeSpan};
        var llm=new OllamaClient(http,timeout:TimeSpan.FromSeconds(90));
        foreach(var model in OllamaClient.Models)
        {
            timings[model]=[];var template=new Template{Title="Revisión real GOAL 02",Template=f.GetProperty("template").GetString()!};
            async Task Check(string id,Func<Task<(object Evidence,bool Checks)>> action)
            {
                var watch=Stopwatch.StartNew();try{var result=await action();all&=result.Checks;records.Add(new{model,id,contract="PASS",checks=result.Checks?"PASS":"FAIL",elapsedMs=watch.Elapsed.TotalMilliseconds,evidence=result.Evidence});Console.WriteLine($"{model} {id}: contract PASS, checks {result.Checks}");}
                catch(Exception ex){all=false;records.Add(new{model,id,contract="FAIL",checks="NOT_EVALUATED",elapsedMs=watch.Elapsed.TotalMilliseconds,error=ex.Message});Console.WriteLine($"{model} {id}: FAIL {ex.Message}");}Save();
            }
            await Check("improve-instructions",async()=>{var p=await llm.Propose("improve","",template,model,default);var checks=f.GetProperty("instructionChecks").EnumerateArray().ToDictionary(x=>x.GetString()!,x=>p.Text.Contains(x.GetString()!,StringComparison.OrdinalIgnoreCase));return(new{proposal=p,instructionProxies=checks},checks.Values.All(v=>v));});
            await Check("create-workflow",async()=>{var p=await llm.Propose("create",f.GetProperty("intent").GetString()!,null,model,default);return(p,TemplateCompiler.Names(p.Text).Length>=2 && p.Text.Contains("riesg",StringComparison.OrdinalIgnoreCase));});
            await Check("suggest-authorized",async()=>{var s=await llm.Suggest(template,f.GetProperty("context").GetString()!,model,default);return(s,s.Count==1 && s.ContainsKey("cambio") && !string.IsNullOrWhiteSpace(s["cambio"]));});
            await Check("analyze-workflow",async()=>{var p=await llm.Propose("analyze","",template,model,default);return(p,p.Text==template.Template && p.Reasons.Length+p.Warnings.Length>0);});
            foreach(var q in f.GetProperty("questions").EnumerateArray())
                await Check(q.GetProperty("id").GetString()!,async()=>
                {
                    var r=await llm.Execute(Compiled(q.GetProperty("prompt").GetString()!),model,true,null,default);timings[model].Add(r.ElapsedMs);var words=Regex.Matches(r.Text,@"\S+").Count;
                    var required=q.GetProperty("requiredAny").EnumerateArray().Any(x=>r.Text.Contains(x.GetString()!,StringComparison.OrdinalIgnoreCase));var forbidden=q.GetProperty("forbidden").EnumerateArray().Where(x=>r.Text.Contains(x.GetString()!,StringComparison.OrdinalIgnoreCase)).Select(x=>x.GetString()).ToArray();
                    return(new{response=r.Text,words,required,forbidden,source=q.GetProperty("source").GetString()},required && forbidden.Length==0 && words<=q.GetProperty("maxWords").GetInt32());
                });
            await Check("cancel-stream",async()=>
            {
                using var cts=new CancellationTokenSource();bool partial=false;
                try{await llm.Execute(Compiled("Escribe una lista numerada de 200 pruebas de regresión detalladas para QuickPrompt Web."),model,true,s=>{if(s.Length>0){partial=true;cts.Cancel();}return Task.CompletedTask;},cts.Token);return(new{partial,cancelled=false},false);}
                catch(OperationCanceledException){return(new{partial,cancelled=true},partial);}
            });
            await Check("recover-after-cancel",async()=>{var r=await llm.Execute(Compiled("Responde solamente con la palabra RECUPERADO, sin explicaciones."),model,true,null,default);return(new{response=r.Text,r.ElapsedMs},r.Text.Trim()=="RECUPERADO");});
        }
        Save();return all?0:1;
        void Save()
        {
            double? Percentile(List<double> x,double p)=>x.Count==0?null:x.Order().ElementAt((int)Math.Ceiling(x.Count*p)-1);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            bool unchanged=hash==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixturePath)));
            var report=new{datasetSha256=hash,datasetUnchanged=unchanged,status=all?"PASS_CHECKS":"FAIL_CHECKS",manualSemanticReview="REQUIRED_SEPARATELY",records,latency=timings.ToDictionary(x=>x.Key,x=>new{n=x.Value.Count,p50Ms=Percentile(x.Value,.5),p95Ms=Percentile(x.Value,.95)}),scope="New authorized project work prompts; automatic semantic/factual checks are proxies, not general accuracy. n=3 factual generations/model; no retries."};
            File.WriteAllText(output,JsonSerializer.Serialize(report,LocalStore.Json));
        }
    }
    private static CompiledPrompt Compiled(string text)=>new(Guid.NewGuid(),Guid.NewGuid(),1,"GOAL02",text,new Dictionary<string,string>(),DateTime.UtcNow);
}
