using System.Text.Json;
using QuickPrompt.Local;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public class QwenThinkingSamplingTests
{
    [Theory]
    [InlineData(false, "qwen3:8b", true)]
    [InlineData(true, "qwen3:8b", false)]
    [InlineData(true, "qwen2.5:7b-instruct", true)]
    public void DefaultNonthinkingAndOtherModelsKeepTheirExactPackets(bool enabled, string model, bool think)
    {
        var packet = JsonSerializer.SerializeToElement(new { model, think, options = new { temperature = 0, num_ctx = 12288 }, messages = new[] { new { role = "user", content = "synthetic" } } });
        Assert.Equal(packet.GetRawText(), QwenThinkingSampling.Apply(packet, enabled).GetRawText());
    }
    [Fact] public void OptInChangesOnlySamplingAndDoesNotMutateTheOriginalOrDisableThinking()
    {
        var packet = JsonSerializer.SerializeToElement(new { model = "qwen3:8b", think = true, format = new { type = "object" },
            options = new { temperature = 0, num_ctx = 12288, num_predict = 2200 }, messages = new[] { new { role = "user", content = "synthetic" } } });
        var before = packet.GetRawText(); var result = QwenThinkingSampling.Apply(packet, true); var options = result.GetProperty("options");
        Assert.Equal(before, packet.GetRawText()); Assert.True(result.GetProperty("think").GetBoolean());
        Assert.Equal(.6, options.GetProperty("temperature").GetDouble()); Assert.Equal(.95, options.GetProperty("top_p").GetDouble());
        Assert.Equal(20, options.GetProperty("top_k").GetInt32()); Assert.Equal(0, options.GetProperty("min_p").GetInt32()); Assert.Equal(42017, options.GetProperty("seed").GetInt32());
        Assert.Equal(12288, options.GetProperty("num_ctx").GetInt32()); Assert.Equal(2200, options.GetProperty("num_predict").GetInt32());
        Assert.Equal(packet.GetProperty("format").GetRawText(), result.GetProperty("format").GetRawText());
        Assert.Equal(packet.GetProperty("messages").GetRawText(), result.GetProperty("messages").GetRawText());
    }
}
