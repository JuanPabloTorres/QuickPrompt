using System.Text.Json;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;

// Public static metadata only. No user input chooses a CLR type or model policy.
if (args is not ["--api-metadata"]) throw new ArgumentException("Use --api-metadata.");
var policy = LocalModelPolicy.Production;
Type[] contracts = [typeof(NovaIntent), typeof(PreparationDecision)];
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
Console.WriteLine(JsonSerializer.Serialize(new {
    contractVersion = 1,
    defaultModel = policy.DefaultModel,
    models = policy.Names.Select(policy.Require).ToArray(),
    // Read the actual JSON wire names, including converter/member attributes,
    // rather than assuming every CLR symbol is serialized unchanged.
    enums = contracts.ToDictionary(type => type.Name, type => Enum.GetValues(type).Cast<object>()
        .Select(value => JsonSerializer.SerializeToElement(value, type, json).GetString()
            ?? throw new InvalidOperationException("The public enum must serialize as a string."))
        .Distinct(StringComparer.Ordinal).ToArray())
}, json));
