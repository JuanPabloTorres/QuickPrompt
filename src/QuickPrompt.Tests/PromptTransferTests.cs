using System.Text.Json;
using QuickPrompt.Local;
using Xunit;
namespace QuickPrompt.Web.Tests;
public class PromptTransferTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.UtcNow;
    private static PromptTransferRequest Request()=>new(1,Guid.NewGuid(),ProviderId.ChatGptWeb,"Comillas \" <literal>\nEspañol 😀",TransferAction.PrepareOnly,Now,Now.AddMinutes(1),true);
    [Fact] public void ExactTextAndEnumsRoundtripWithoutHtmlInterpretation(){var r=Request();r.Validate(Now);var json=JsonSerializer.Serialize(r);Assert.Equal(r,JsonSerializer.Deserialize<PromptTransferRequest>(json));Assert.Contains("ChatGptWeb",json);}
    [Theory] [InlineData(0)] [InlineData(20001)] public void EmptyOrOversizedTextRejected(int size)=>Assert.Throws<ValidationException>(()=>(Request() with{CompiledPrompt=new('x',size)}).Validate(Now));
    [Fact] public void ExactMaximumAccepted()=>(Request() with{CompiledPrompt=new('x',20000)}).Validate(Now);
    [Fact] public void NoLocalModelOrUnknownProviderOrAction(){foreach(var p in new[]{ProviderId.LocalOllama,(ProviderId)99})Assert.Throws<ValidationException>(()=>(Request() with{Provider=p}).Validate(Now));Assert.Throws<ValidationException>(()=>(Request() with{Action=(TransferAction)99}).Validate(Now));}
    [Fact] public void ExplicitActionMandatory()=>Assert.Throws<ValidationException>(()=>(Request() with{ExplicitUserAction=false}).Validate(Now));
    [Fact] public void IdentityAndVersionRequired(){Assert.Throws<ValidationException>(()=>(Request() with{RequestId=Guid.Empty}).Validate(Now));Assert.Throws<ValidationException>(()=>(Request() with{Version=2}).Validate(Now));}
    [Fact] public void ExpiryAndLifetimeBounded(){foreach(var r in new[]{Request() with{ExpiresAtUtc=Now},Request() with{ExpiresAtUtc=Now.AddMinutes(3)},Request() with{CreatedAtUtc=Now.AddMinutes(1)},Request() with{CreatedAtUtc=Now.AddMinutes(-3)}})Assert.Throws<ValidationException>(()=>r.Validate(Now));}
    [Fact] public void WebResultNeverPretendsToBeModelInference(){var r=new PromptTransferResult(Guid.NewGuid(),TransferState.SubmittedUI,"ui-transition-observed");var json=JsonSerializer.Serialize(r);Assert.DoesNotContain("Response",json);Assert.DoesNotContain("CompiledPrompt",json);}
    [Fact] public void OptionalEmptyIdsAreRejectedConsistentlyWithTheWorker(){foreach(var r in new[]{Request() with{TemplateId=Guid.Empty},Request() with{RevisionId=Guid.Empty}}){Assert.Throws<ValidationException>(()=>r.Validate(Now));Assert.Equal("invalid-metadata",r.ValidationCode(Now));}Assert.Equal("expired-request",(Request() with{ExpiresAtUtc=Now}).ValidationCode(Now));}
}
