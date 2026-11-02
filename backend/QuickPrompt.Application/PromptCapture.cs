using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

/// <summary>A caller's stable operation ID and the reusable instruction to capture.</summary>
public record PromptCaptureInput(Guid OperationId, PromptInput Prompt);
/// <summary>Confirmed stored content; Replayed indicates an unchanged earlier capture.</summary>
public record PromptCaptureResult(Guid OperationId, PromptDocument Prompt, bool Replayed);
public record PromptCaptureWriteResult(Template Prompt, bool Replayed);

/// <summary>Owns atomic creation and a durable receipt in the same library.</summary>
public interface IPromptCaptureWriter
{
    PromptCaptureWriteResult Capture(Guid operationId, Template prompt);
}

/// <summary>Captures literal content without inference or registering another prompt use.</summary>
public interface IPromptCapture
{
    /// <exception cref="ValidationException">The operation or prompt is invalid.</exception>
    /// <exception cref="ConflictException">The key or its stored result has changed.</exception>
    PromptCaptureResult Capture(PromptCaptureInput input);
}

public sealed class PromptCapture(IPromptCaptureWriter writer) : IPromptCapture
{
    public PromptCaptureResult Capture(PromptCaptureInput input)
    {
        if (input.OperationId == Guid.Empty || input.Prompt is null || input.Prompt.Revision != 0)
            throw new ValidationException("La captura requiere una operación y un prompt nuevo.");
        var result = writer.Capture(input.OperationId, PromptLibrary.NewTemplate(input.Prompt));
        return new(input.OperationId, PromptLibrary.Document(result.Prompt), result.Replayed);
    }
}
