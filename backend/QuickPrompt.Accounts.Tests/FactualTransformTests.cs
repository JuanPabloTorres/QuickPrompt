using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class FactualTransformTests
{
    internal const string OriginalANotes = "Ana terminó el prototipo. Luis revisará las pruebas el 30 de octubre de 2026. El equipo acordó mantener el alcance actual. La fecha de publicación no se ha confirmado.";
    // Exact public structured content of the first native factual campaign. The misspelled quote is evidence, not repaired fixture data.
    internal const string OriginalAProposal = """
        {"facts": [{"role": "completado", "text": "Ana terminó el prototipo", "quote": "Ana terminó el prototipo.", "sourceField": "notas"}, {"role": "tarea_futura", "text": "Luis revisará las pruebas el 30 de octubre de 2026", "quote": "Luis revisarán las pruebas el 30 de octubre de 2026.", "sourceField": "notas"}, {"role": "decisióndecidida", "text": "El equipo acordó mantener el alcance actual", "quote": "El equipo acordó mantener el alcance actual.", "sourceField": "notas"}, {"role": "información_faltante", "text": "La fecha de publicación no se ha confirmado", "quote": "La fecha de publicación no se ha confirmado.", "sourceField": "notas"}]}
        """;
    private static FactualSource Source(string text) => new(Guid.NewGuid(), 1, "Procesa <notas>.", new() { ["notas"] = text }, new() { ["notas"] = text }, "Procesa " + text + ".");
    [Fact]
    public void RecordedOriginalAReachesReviewUnmodifiedButItsNonliteralQuoteCannotBeConfirmed()
    {
        var source = Source(OriginalANotes);
        var proposal = new FactualProposalGenerator().Read(JsonSerializer.SerializeToElement(new { message = new { content = OriginalAProposal } }), source);
        Assert.Equal(4, proposal.Facts.Length);
        Assert.Equal("Luis revisarán las pruebas el 30 de octubre de 2026.", proposal.Facts[1].Quote);
        Assert.Throws<ValidationException>(() => FactualTransform.Compose("Acta revisada", proposal.Facts, source));
        var confirmed = proposal.Facts.Select((fact, index) => index == 1 ? fact with { Quote = "Luis revisará las pruebas el 30 de octubre de 2026." } : fact).ToArray();
        Assert.Equal("# Acta revisada\n\n## completado\n\n- Ana terminó el prototipo\n\n## tarea\\_futura\n\n- Luis revisará las pruebas el 30 de octubre de 2026\n\n## decisióndecidida\n\n- El equipo acordó mantener el alcance actual\n\n## información\\_faltante\n\n- La fecha de publicación no se ha confirmado\n\n", FactualTransform.Compose("Acta revisada", confirmed, source));
        Assert.Equal("Luis revisarán las pruebas el 30 de octubre de 2026.", proposal.Facts[1].Quote); // No normalization or mutation of original evidence.
    }
    [Fact]
    public void AValidQuoteDoesNotGiveAProposedRoleAuthorityAndCompositionUsesOnlyTheConfirmedCorrection()
    {
        var source = Source("Ana terminó el prototipo. Luis revisará pruebas el 30 de octubre.");
        FactualStatement[] proposed = [new("Participantes", "Ana y Luis", "notas", "Ana terminó el prototipo.")];
        FactualTransform.Validate(proposed, source); // Provenance is valid; the semantic claim remains untrusted.
        FactualStatement[] confirmed = [new("Trabajo terminado", "Ana terminó el prototipo.", "notas", "Ana terminó el prototipo."),
            new("Tareas", "Luis revisará pruebas el 30 de octubre.", "notas", "Luis revisará pruebas el 30 de octubre.")];
        var document = FactualTransform.Compose("Acta revisada", confirmed, source);
        Assert.DoesNotContain("Participantes", document); Assert.Contains("## Tareas", document);
        Assert.Contains("Ana terminó el prototipo", document);
    }
    [Fact]
    public void AnotherDomainKeepsOwnershipDistinctFromAConditionAndDoesNotInventOperationalRoles()
    {
        var source = Source("El sensor pertenece a Marta. Su calibración está pendiente; no se indicó responsable.");
        FactualStatement[] confirmed = [new("Propiedad", "El sensor pertenece a Marta.", "notas", "El sensor pertenece a Marta."),
            new("Pendientes", "Calibración pendiente; responsable no indicado.", "notas", "Su calibración está pendiente; no se indicó responsable.")];
        var document = FactualTransform.Compose("Inventario", confirmed, source);
        Assert.Contains("## Propiedad", document); Assert.Contains("responsable no indicado", document);
        Assert.DoesNotContain("Marta calibrará", document); Assert.DoesNotContain("Participantes", document);
    }
    [Fact]
    public void ATaskWithoutDateStaysInTheConfirmedTaskRoleRatherThanBecomingABlocker()
    {
        var source = Source("Diego revisará accesibilidad; no se indicó fecha.");
        var document = FactualTransform.Compose("Revisión", [new("Tareas", "Diego revisará accesibilidad; fecha no indicada.", "notas", source.SourceFields["notas"])], source);
        Assert.Contains("## Tareas", document); Assert.DoesNotContain("Bloqueos", document);
    }
    [Fact]
    public void NoProposedFactsIsVisibleDataButCannotBeConfirmedAsAnEmptyDocument()
    {
        var source = Source("Texto insuficiente.");
        var codec = new FactualProposalGenerator();
        var reply = JsonSerializer.SerializeToElement(new { message = new { content = "{\"facts\":[]}" } });
        Assert.Empty(codec.Read(reply, source).Facts);
        Assert.Throws<ValidationException>(() => FactualTransform.Compose("Vacío", [], source));
    }
    [Fact]
    public void FabricatedQuotesAndSourcesAreRejectedWithoutTreatingAValidQuoteAsSemanticAcceptance()
    {
        var source = Source("Hecho literal.");
        Assert.Throws<ValidationException>(() => FactualTransform.Validate([new("Hechos", "Otro", "notas", "No existe")], source));
        Assert.Throws<ValidationException>(() => FactualTransform.Validate([new("Hechos", "Otro", "campo-ajeno", "Hecho literal.")], source));
        FactualTransform.Validate([new("Dato no especificado", "La fecha no está indicada; revisión humana requerida.", "", "")], source);
    }
    [Fact]
    public void OversizedSourceIsRejectedInsteadOfTruncatedAndModelConfigurationIsPreserved()
    {
        var codec = new FactualProposalGenerator();
        Assert.Throws<ValidationException>(() => codec.Packet(Source(new string('x', 5000))));
        var packet = codec.Packet(Source("Hecho literal."));
        Assert.Equal("qwen3:8b", packet.GetProperty("model").GetString());
        Assert.Equal(12288, packet.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(2600, packet.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.False(packet.TryGetProperty("tools", out _));
    }
    [Fact]
    public void ConfirmedLiteralTextCannotInjectExtraMarkdownSectionsOrHtml()
    {
        var source = Source("Texto.");
        var document = FactualTransform.Compose("<script>", [new("Hechos", "# Otro\n<script>", "", "")], source);
        Assert.DoesNotContain("<script>", document); Assert.Contains("\\<script\\>", document);
        Assert.DoesNotContain("\n# Otro", document);
    }
}
