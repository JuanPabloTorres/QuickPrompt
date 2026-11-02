using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed partial class RuntimeSettingsSnapshotTests
{
    [Fact]
    public void LinguisticResourcesUseTheRegistrationSnapshotAndRemainOffUntilExplicitlySelected()
    {
        var configuration = Configuration();
        var initial = Register(configuration);
        configuration["QuickPrompt:Preparation:TranslationSource"] = "apertium-native";
        var selected = Register(configuration);
        using var before = initial.BuildServiceProvider();
        using var after = selected.BuildServiceProvider();
        Assert.Null(before.GetService<IAuthoringTranslationSourceViews>());
        Assert.IsType<ApertiumTranslationSourceViews>(after.GetRequiredService<IAuthoringTranslationSourceViews>());
        Assert.NotNull(after.GetRequiredService<QuickPrompt.Nova.Application.IPromptGenerator>());
    }

    [Theory]
    [InlineData("auto", null)]
    [InlineData("none", "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("apertium-native", "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("apertium-docker", null)]
    public void UnselectedOrInconsistentResourceSettingsAreRejectedInsteadOfFallingBack(string mode, string? image)
    {
        Assert.Throws<InvalidOperationException>(() => new AuthoringTranslationResourceOptions(mode, image).Create());
    }

    [Fact]
    public void DockerResourceRequiresAnImmutableExplicitDigest()
    {
        Assert.Throws<ArgumentException>(() => new AuthoringTranslationResourceOptions("apertium-docker", "latest").Create());
        Assert.IsType<ApertiumTranslationSourceViews>(new AuthoringTranslationResourceOptions("apertium-docker",
            "sha256:916315150c8a7a047a9bcb9950ede51bf2fc1ea0c0a8cfe3a1b9dc1d2545a880").Create());
    }
}
