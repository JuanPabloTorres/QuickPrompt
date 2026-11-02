using System.Runtime.CompilerServices;
using QuickPrompt.Web.Infrastructure;

// Existing compiled consumers can resolve the same public names after ownership
// moves to Application. No implementation or provider dependency moves with them.
[assembly: TypeForwardedTo(typeof(ILocalLlmClient))]
[assembly: TypeForwardedTo(typeof(TemplateEditInterpretation))]
[assembly: TypeForwardedTo(typeof(LlmUnavailableException))]
[assembly: TypeForwardedTo(typeof(LocalModelPolicy))]
[assembly: TypeForwardedTo(typeof(LocalModelCapability))]
[assembly: TypeForwardedTo(typeof(AdaptiveCoreOptions))]
[assembly: TypeForwardedTo(typeof(AdaptiveCoreModelTransport))]
[assembly: TypeForwardedTo(typeof(IAgentModelTransport))]
[assembly: TypeForwardedTo(typeof(IValidatedAgentModelTransport))]
[assembly: TypeForwardedTo(typeof(DevelopmentLabObserver))]
[assembly: TypeForwardedTo(typeof(ObservedLiveRunStore))]
[assembly: TypeForwardedTo(typeof(OllamaClient))]
[assembly: TypeForwardedTo(typeof(PromptDraftGenerator))]
[assembly: TypeForwardedTo(typeof(DraftFidelityReviewer))]
[assembly: TypeForwardedTo(typeof(PromptInputValueExtractor))]
[assembly: TypeForwardedTo(typeof(PromptInputScopeInterpreter))]
[assembly: TypeForwardedTo(typeof(SemanticCandidateSelector))]
[assembly: TypeForwardedTo(typeof(UiDecisionModel))]
[assembly: TypeForwardedTo(typeof(PromptContentInterpreter))]
[assembly: TypeForwardedTo(typeof(PromptAdviceResponder))]
[assembly: TypeForwardedTo(typeof(AgentStagePacket))]
[assembly: TypeForwardedTo(typeof(InferenceObservation))]
[assembly: TypeForwardedTo(typeof(ValidatedAgentJson))]
