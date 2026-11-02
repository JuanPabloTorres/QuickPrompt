using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;
using static QuickPrompt.Accounts.Tests.AccountHttpTests;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Records frozen executions. Structural success never certifies the factual roles in their prose.</summary>
public sealed class LocalExecutionFactualityRealProviderTests
{
    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task RecordsExplicitAttendeesAndASeparateNonAttendingReferenceWithoutSemanticAutoAcceptance()
    {
        const string template = "Convierte las siguientes notas en un acta clara en español. Incluye decisiones, tareas con responsable y fecha, bloqueos y pendientes. "
            + "Sólo usa hechos explícitos; si falta información indícalo. No inventes participantes, fechas ni acuerdos.\n\nNotas:\n<notas>";
        const string notes = "Asistieron únicamente Carla Núñez y Diego Pérez.\nSe decidió revisar el prototipo antes de publicarlo.\n"
            + "Carla Núñez preparará la lista de ajustes para el 12 de noviembre de 2026.\nDiego Pérez revisará accesibilidad; no se indicó fecha.\n"
            + "Se mencionó que la documentación de Laura Méndez servirá de referencia; Laura Méndez no asistió a la reunión.\nNo se acordó una fecha de publicación.";
        var expectedFacts = new {
            attendees = new[] { "Carla Núñez", "Diego Pérez" }, nonAttendingMention = "Laura Méndez",
            tasks = new[] { new { owner = "Carla Núñez", task = "preparar la lista de ajustes", date = "12 de noviembre de 2026" },
                new { owner = "Diego Pérez", task = "revisar accesibilidad", date = "NO_ESPECIFICADA" } },
            decision = "revisar el prototipo antes de publicarlo", publicationDate = "NO_ACORDADA",
            criterion = "Mentioning a name or assigning a task does not authorize classifying that person as attending. No additional factual roles, dates or agreements."
        };
        await Probe("acta-control-b-explicit-roles", template, new() { ["notas"] = notes }, expectedFacts);
    }

    [RealQwenFact, Trait("Category", "RealQwen")]
    public async Task RecordsTheExactRecoveredRc7ExecutionWithoutTreatingMentionedPeopleAsConfirmedAttendees()
    {
        const string template = "Convierte las notas de <reunion> en un acta en español. Identifica acuerdos, tareas con responsable y plazo explícitos, "
            + "y preguntas pendientes. Usa sólo estas notas: <notas>. Marca como pendiente cualquier dato que no aparezca; no lo inventes. Entrega Markdown.";
        var values = new Dictionary<string, string> {
            ["reunion"] = "Revisión del prototipo · 28 de octubre de 2026",
            ["notas"] = "Ana terminó el prototipo. Luis revisará las pruebas el 30 de octubre de 2026. El equipo acordó mantener el alcance actual. La fecha de publicación no se ha confirmado."
        };
        var sourcePath = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_EXECUTION_INCIDENT_SOURCE")
            ?? throw new InvalidOperationException("Select the owner-recovered QA instruction and original response; never invent a replacement incident.");
        using var source = JsonDocument.Parse(await File.ReadAllTextAsync(sourcePath));
        var originalInstruction = source.RootElement.GetProperty("blocks")[0].GetString();
        var originalResponse = source.RootElement.GetProperty("blocks")[1].GetString();
        Assert.False(string.IsNullOrWhiteSpace(originalResponse));
        Assert.Equal(originalInstruction, EditorTools.Compile(template, values).Text); // Exact punctuation, including the source's final double full stop.
        var expectedFacts = new {
            attendees = "NO_ESPECIFICADOS; mentioning Ana, Luis or the team does not establish attendance",
            completedFact = "Ana terminó el prototipo",
            futureTask = new { owner = "Luis", task = "revisar las pruebas", date = "30 de octubre de 2026" },
            agreement = "El equipo acordó mantener el alcance actual; no additional individual owner or deadline",
            publicationDate = "NO_CONFIRMADA",
            criterion = "No invented attendees, roles, future tasks, dates or agreements. Completed work is not a newly assigned future task."
        };
        await Probe("acta-original-a-recovered-rc7", template, values, expectedFacts, originalResponse);
    }

    private static async Task Probe(string id, string template, Dictionary<string, string> values, object expectedFacts, string? originalResponse = null)
    {
        var directory = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_REAL_EVIDENCE")
            ?? throw new InvalidOperationException("Select a new isolated synthetic evidence directory before sampling.");
        Directory.CreateDirectory(directory);
        var reportPath = Path.Combine(directory, id + ".json");
        if (File.Exists(reportPath)) throw new InvalidOperationException("This sample already has evidence; never overwrite it or retry it to obtain PASS.");
        var compiled = EditorTools.Compile(template, values);
        Assert.Empty(compiled.MissingVariables); Assert.NotNull(compiled.Text);
        foreach (var value in values.Values) Assert.Contains(value, compiled.Text, StringComparison.Ordinal);
        // Exact current LocalWorkProtocol adapter shape from CompanionConnection.ClaimInference.
        // This diagnostic does not introduce a different system prompt, format, tool or decoding path.
        var packet = JsonSerializer.SerializeToElement(new {
            model = "qwen3:8b", stream = false, think = false,
            messages = new[] { new { role = "user", content = compiled.Text } },
            options = new { num_ctx = LocalModelPolicy.AgentContextLength, num_predict = 2600, temperature = 0 }
        });
        var sourcePath = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_COMPANION_SOURCE")
            ?? throw new InvalidOperationException("Select the inspected CompanionConnection source path to identify the adapter under test.");
        var sourceSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(sourcePath)));
        using var lease = new FileStream(Path.Combine(Path.GetTempPath(), "QuickPrompt.Nova.SemanticModel.lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        JsonElement? response = null; string? text = null; Exception? error = null;
        var calls = 0; var watch = Stopwatch.StartNew();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            calls++;
            using var reply = await RealProvider().Send(packet, timeout.Token, "companion.inference");
            response = reply.RootElement.Clone();
            text = reply.RootElement.GetProperty("message").GetProperty("content").GetString();
            Assert.False(string.IsNullOrWhiteSpace(text)); Assert.InRange(text!.Length, 1, 64000);
        }
        catch (Exception failure) { error = failure; }
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new {
            id, scope = "NATIVE_SELECTED_PROVIDER_EXECUTION_COMPONENT_ONLY_NO_DATABASE_NO_UI_NO_PERSISTENCE",
            status = error is null ? "REQUIRES_INDEPENDENT_SEMANTIC_REVIEW" : "TECHNICAL_FAILURE", semanticCertification = false,
            originalIncidentSourceRecovered = originalResponse is not null, originalResponse, template, values, expectedFacts, compiledInstruction = compiled.Text,
            compiledSha256 = LiveRuns.Hash(compiled.Text!), sourceSha256, packet, response, text, calls, maximumCalls = 1,
            elapsedMs = watch.ElapsedMilliseconds, timeoutSeconds = 90, repeatedToObtainPass = false,
            error = error?.GetType().Name, message = error?.Message
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Assert.Null(error); // Transport/result framing only; the independent factual oracle is reviewed separately.
    }
}
