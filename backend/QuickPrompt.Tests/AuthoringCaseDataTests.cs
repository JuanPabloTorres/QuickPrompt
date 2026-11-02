using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class AuthoringCaseDataTests
{
    private const string Secret = "DATO_PRIVADO_SINTETICO";
    private const string Request = "Resume <documento>. Documento: " + Secret + "; conserva la salida en español.";

    private static AuthoringObligationContract Contract()
    {
        var scope = UserInputScope.Bind(Request, null, null, ["documento"], [new("documento", Secret, Secret)]);
        return new(Request, inputData: scope.AuthoringRanges(Request, ["documento"]));
    }

    [Fact]
    public void AppOwnedCaseRangesStayDataAndCannotBecomeReusableTextOrModelAuthority()
    {
        var contract = Contract(); var dataId = Assert.Single(contract.InputDataUnitIds);
        Assert.DoesNotContain(Secret, contract.AuthoringRequest); Assert.Equal(Request.Length, contract.AuthoringRequest.Length);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(contract.RequestUnits));
        var ranges = new[] { new AuthoringDisposition(1, dataId - 1, "FUTURE_TASK"), new(dataId, dataId, "INPUT_DATA"),
            new(dataId + 1, contract.RequestUnits.Length, "FUTURE_TASK") };
        contract.WithInventory(new(ranges, [], [], null, ""));
        var body = contract.Compose(PromptInputContract.FromUserSelection(Request, "", false, ["documento"]));
        Assert.DoesNotContain(Secret, body); Assert.Contains("<documento>", body); Assert.Contains("conserva la salida en español", body);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(contract.Ledger));
        Assert.DoesNotContain(Secret, contract.MetadataAuthorityRequest);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(contract.GetContentReview(body)));
    }

    [Fact]
    public void TheModelCannotRelabelAnAppBoundCaseValueAsFutureTaskOrAuthorization()
    {
        var contract = Contract();
        Assert.Throws<ValidationException>(() => contract.WithInventory(new([new(1, contract.RequestUnits.Length, "FUTURE_TASK")], [], [], null, "")));
        Assert.False(contract.Resolved);
    }

    [Fact]
    public void QuotedCaseValuesAreNotOfferedAsCompleteLiteralBodyCandidates()
    {
        var start = Request.IndexOf(Secret, StringComparison.Ordinal);
        var scope = UserInputScope.Bind(Request, null, null, ["documento"], [new("documento", Secret, Secret)]);
        var contract = new AuthoringObligationContract(Request, literals: [new(0, start, Secret.Length, Secret)], inputData: scope.AuthoringRanges(Request, ["documento"]));
        Assert.Empty(contract.Literals);
    }

    [Fact]
    public void InputDataBoundariesCannotSplitUnicodeOrOverlapAnotherBinding()
    {
        const string original = "🛰️ dato é";
        Assert.Throws<ValidationException>(() => new AuthoringObligationContract(original, inputData: [new(1, 1, "dato")]));
        Assert.Throws<ValidationException>(() => new AuthoringObligationContract("ABCDEF", inputData: [new(0, 4, "a"), new(3, 2, "b")]));
    }
}
