using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ContractArrayBoundsTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void ArrayCardinalityAppliesInsideNestedContracts(int count, bool valid)
    {
        var schema = ContractSchema.Object(new() {
            ["checks"] = new { type = "array", minItems = 2, maxItems = 3, items = ContractSchema.Text(30, ["FUTURE_TASK"]) }
        }, "checks");
        var value = JsonSerializer.SerializeToElement(new { checks = Enumerable.Repeat("FUTURE_TASK", count).ToArray() });
        if (valid) ContractSchema.Validate(value, schema);
        else Assert.Throws<ValidationException>(() => ContractSchema.Validate(value, schema));
    }
}
