using System.Runtime.CompilerServices;
using QuickPrompt.Web.Infrastructure;

// Existing compiled consumers can resolve the same public names after ownership
// moves to Application. No implementation or provider dependency moves with them.
[assembly: TypeForwardedTo(typeof(ILocalLlmClient))]
[assembly: TypeForwardedTo(typeof(TemplateEditInterpretation))]
[assembly: TypeForwardedTo(typeof(LlmUnavailableException))]
[assembly: TypeForwardedTo(typeof(LocalModelPolicy))]
[assembly: TypeForwardedTo(typeof(LocalModelCapability))]
