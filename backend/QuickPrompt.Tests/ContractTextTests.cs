using System.Text.Encodings.Web;
using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class ContractTextTests
{
    [Theory]
    [InlineData("Añade una sección de riesgos.")]
    [InlineData("資料と改善")]
    [InlineData("Una \"cita\"\ny otra línea.")]
    public void EnumMembershipUsesDecodedTextRatherThanJsonEscapeSpelling(string text)
    {
        var schema = JsonSerializer.SerializeToElement(ContractSchema.Text(100, [text]));
        var literal = JsonSerializer.SerializeToElement(text, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        ContractSchema.Validate(literal, schema);
        Assert.Throws<ValidationException>(() => ContractSchema.Validate(JsonSerializer.SerializeToElement(text + " Otra orden."), schema));
    }
}
