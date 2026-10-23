namespace QuickPrompt.Web.Infrastructure;

// Shared diagnostic facts; no provider protocol, runtime or task authority.
public record InferenceObservation(string Kind, string? Model = null, int? PromptTokens = null, int? GeneratedTokens = null);
