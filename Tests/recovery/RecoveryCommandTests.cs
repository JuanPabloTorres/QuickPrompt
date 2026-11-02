using QuickPrompt.Recovery;
using Xunit;

namespace QuickPrompt.Recovery.Tests;

/// <summary>CLI contract only: no files, keys, passwords, processes or network.</summary>
public sealed class RecoveryCommandTests
{
    private static string[] VerifyArguments() => ["verify", "--repository-root", "synthetic-repository", "--input", "synthetic-archive",
        "--archive-sha256", new string('A', 64), "--plaintext-sha256", new string('B', 64),
        "--recipient-pfx", "synthetic-existing-recipient", "--recipient-sha256", new string('C', 64)];

    [Fact]
    public void VerifyHasNoOutputOptionAndRequiresTheExistingPrivateRecipient()
    {
        var command = RecoveryCommand.Parse(VerifyArguments());
        Assert.Equal("verify", command.Mode);
        Assert.Equal(6, command.Options.Count);
        Assert.False(command.Options.ContainsKey("--output"));
        Assert.True(command.Options.ContainsKey("--recipient-pfx"));
        command.RequireInteractiveInput(false);
    }

    [Theory]
    [InlineData("--output")]
    [InlineData("--password")]
    [InlineData("--password-file")]
    [InlineData("--password-env")]
    [InlineData("--recipient-cert")]
    public void VerifyRejectsOutputAndPasswordBypassOptionsBeforeAnyInput(string option) =>
        Assert.Throws<InvalidDataException>(() => RecoveryCommand.Parse([.. VerifyArguments(), option, "synthetic-not-a-secret"]));

    [Fact]
    public void DuplicateMissingAndUnknownOptionsCannotEnterVerification()
    {
        var arguments = VerifyArguments();
        Assert.Throws<InvalidDataException>(() => RecoveryCommand.Parse([.. arguments, "--input", "other"]));
        Assert.Throws<InvalidDataException>(() => RecoveryCommand.Parse(arguments[..^1]));
        Assert.Throws<InvalidDataException>(() => RecoveryCommand.Parse(arguments[..^2]));
        Assert.Throws<InvalidDataException>(() => RecoveryCommand.Parse(["unknown", .. arguments[1..]]));
    }

    [Theory]
    [InlineData("recover")]
    [InlineData("verify")]
    public void PrivateKeyOperationsRejectRedirectedInputBeforeFileAccess(string mode)
    {
        var arguments = VerifyArguments(); arguments[0] = mode;
        if (mode == "recover") arguments = [.. arguments, "--output", "synthetic-output"];
        var command = RecoveryCommand.Parse(arguments);
        Assert.Throws<InvalidOperationException>(() => command.RequireInteractiveInput(true));
        command.RequireInteractiveInput(false);
    }

    [Fact]
    public void ProtectAndRecoverRetainTheirExactRequiredOutputContracts()
    {
        var recover = VerifyArguments(); recover[0] = "recover";
        Assert.Throws<InvalidDataException>(() => RecoveryCommand.Parse(recover));
        Assert.Equal("synthetic-output", RecoveryCommand.Parse([.. recover, "--output", "synthetic-output"]).Options["--output"]);
        string[] protect = ["protect", "--repository-root", "synthetic-repository", "--input", "synthetic-dump",
            "--output", "synthetic-archive", "--plaintext-sha256", new string('B', 64), "--recipient-cert", "synthetic-public-cert",
            "--recipient-sha256", new string('C', 64), "--retain-until", "2030-01-01T00:00:00Z"];
        var command = RecoveryCommand.Parse(protect);
        command.RequireInteractiveInput(true); // Public-key protection never requests a password.
        Assert.Equal("synthetic-archive", command.Options["--output"]);
        Assert.Throws<InvalidDataException>(() => RecoveryCommand.Parse(protect[..^2]));
    }
}
