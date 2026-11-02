using Xunit;

namespace QuickPrompt.Nova.Tests;

/// <summary>Explicit resource integration; the release workflow runs it on its separately built pinned image.</summary>
public sealed class TranslationResourceFactAttribute : FactAttribute
{
    public TranslationResourceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_APERTIUM") != "1")
            Skip = "Requires the pinned linguistic image; mandatory in the linguistic-resource release job.";
    }
}
