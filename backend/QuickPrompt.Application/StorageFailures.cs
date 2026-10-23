namespace QuickPrompt.Nova.Application;

// Presentation needs a safe failure category, not the database provider's types.
// Implementations are pure/nonthrowing and do not inspect user-facing messages.
public interface IStorageFailureClassifier
{
    bool IsFailure(Exception error);
}
