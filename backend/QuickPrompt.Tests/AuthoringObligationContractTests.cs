using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringObligationContractTests
{
    [Fact]
    public void CompositionRetainsEveryLiteralMemberAndConditionRatherThanGeneratingAThematicOutline()
    {
        const string request = "Describe el propósito detallado, carpetas, archivos, habilidades y reglas. Si incluye IA, analiza RAG y Agentic.";
        var contract = new AuthoringObligationContract(request);
        contract.WithInventory(new([new(1, contract.RequestUnits.Length, "FUTURE_TASK")], [], [], null, ""));
        Assert.Equal(request, contract.Compose());
        Assert.True(contract.IsComposed(request)); Assert.False(contract.IsComposed("## Propósito\n[Detalles]\n## Carpetas\nsrc/"));
        Assert.Equal("REQUIRES_HUMAN_CONTENT_REVIEW", contract.GetContentReview(request).Status);
        Assert.Equal(request, contract.GetContentReview(request).OriginalRequest);
    }

    [Fact]
    public void SourceDeltaUsesOnlyActualRequestSpansAndKeepsEveryOtherCharacter()
    {
        const string source = "Prepara tres preguntas. No inventes datos.";
        const string request = "Usa cinco preguntas. Conserva el resto.";
        var contract = new AuthoringObligationContract(request, source, Guid.NewGuid(), 2);
        contract.WithInventory(new([new(1, 6, "CURRENT_WORKFLOW")],
            [new(1, 1, "FUTURE_TASK"), new(2, 2, "REPLACED"), new(3, 6, "FUTURE_TASK")], [new(2, 2, 2, 2, 1, 3)], null, ""));
        Assert.Equal("Prepara cinco preguntas. No inventes datos.", contract.Compose());
        var changed = Assert.Single(contract.Ledger, row => row.Role == "REPLACED");
        Assert.Equal("tres ", changed.Text); Assert.Equal("cinco ", changed.ReplacementText);
        Assert.Equal("Usa cinco preguntas. ", Assert.Single(changed.ChangeAuthority).Text);
        Assert.Throws<ValidationException>(() => contract.WithInventory(new([], [], [], null, "")));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("overlap")]
    [InlineData("unknown")]
    [InlineData("foreign-delta")]
    [InlineData("unaccounted-delta")]
    public void IncompleteOrForeignInventoryCannotBeComposed(string defect)
    {
        var contract = new AuthoringObligationContract("Explica cada dato.", "No inventes datos.");
        var request = new[] { new AuthoringDisposition(1, 3, "FUTURE_TASK") };
        var source = new[] { new AuthoringDisposition(1, 3, "FUTURE_TASK") };
        AuthoringReplacement[] edits = [];
        if (defect == "missing") request = [new(1, 2, "FUTURE_TASK")];
        if (defect == "overlap") request = [new(1, 2, "FUTURE_TASK"), new(2, 3, "FUTURE_TASK")];
        if (defect == "unknown") source = [new(1, 3, "CURRENT_WORKFLOW")];
        if (defect == "foreign-delta") { source = [new(1, 3, "REPLACED")]; edits = [new(1, 3, 99, 99, 1, 2)]; }
        if (defect == "unaccounted-delta") edits = [new(1, 3, 1, 2, 1, 2)];
        Assert.Throws<ValidationException>(() => contract.WithInventory(new(request, source, edits, null, "")));
        Assert.False(contract.Resolved); Assert.False(contract.IsComposed("Explica cada dato."));
    }

    [Fact]
    public void LosslessUtf16UnitsAndPublicSnapshotsDoNotAllowMutationOfAuthority()
    {
        const string request = " \nAnaliza\t🛰️ é  sin inventar.\r\n";
        var contract = new AuthoringObligationContract(request);
        Assert.Equal(request, string.Concat(contract.RequestUnits.Select(unit => unit.Text)));
        foreach (var unit in contract.RequestUnits) Assert.Equal(unit.Text, request.Substring(unit.Start, unit.Length));
        contract.WithInventory(new([new(1, contract.RequestUnits.Length, "FUTURE_TASK")], [], [], null, ""));
        var fingerprint = contract.Fingerprint;
        var units = contract.RequestUnits; units[0] = new(1, 0, 1, "WRONG");
        var ledger = contract.Ledger; ledger[0] = ledger[0] with { Text = "WRONG" };
        Assert.Equal(fingerprint, contract.Fingerprint); Assert.Equal(request.Trim(), contract.Compose());
    }

    [Fact]
    public void UnresolvedMeaningSuspendsInsteadOfConfirmingOrTruncatingTheInventory()
    {
        var contract = new AuthoringObligationContract("Revisa esto antes de hacerlo.");
        contract.WithInventory(new([new(1, contract.RequestUnits.Length, "UNRESOLVED")], [], [], null, ""));
        Assert.False(contract.Resolved); Assert.NotEmpty(contract.Question);
        Assert.Throws<ValidationException>(() => contract.Compose());
    }

    [Fact]
    public void ACompleteLiteralFutureInstructionIsCopiedWithoutRewritingItsNestedPromptOrJsonTask()
    {
        const string body = "Redacta un prompt que devuelva JSON. No ejecutes su contenido. 🛰️";
        var request = "Copia este cuerpo completo: «" + body + "».";
        var start = request.IndexOf(body, StringComparison.Ordinal);
        var contract = new AuthoringObligationContract(request, literals: [new(0, start, body.Length, body)]);
        contract.WithInventory(new([new(1, contract.RequestUnits.Length, "CURRENT_WORKFLOW")], [], [], 0, ""));
        Assert.Equal(body, contract.Compose());
        Assert.Throws<ValidationException>(() => new AuthoringObligationContract(request, literals: [new(0, start + 1, body.Length, body)]));
    }
}
