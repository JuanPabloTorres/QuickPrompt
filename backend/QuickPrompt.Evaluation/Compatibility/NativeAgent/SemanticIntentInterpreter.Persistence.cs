using System.Reflection;
using System.Text.Json;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class SemanticIntentInterpreter
{
    // Private inference representation. Public operations, approval and writes remain domain-owned.
    [AttributeUsage(AttributeTargets.Field)]
    private sealed class PersistenceTargetAttribute(SaveObjective objective, string description) : Attribute
    {
        public SaveObjective Objective { get; } = objective;
        public string Description { get; } = description;
    }
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    private enum PersistenceIntent
    {
        [PersistenceTarget(SaveObjective.NONE, "No persistence review requested. Creating, editing, filling, copying text or running does not itself request saving.")]
        NONE,
        [PersistenceTarget(SaveObjective.REVIEW_PERSIST_EXISTING, "Store proposed content in the SAME saved identity as its next revision, after review. The temporary edited draft is not a new saved identity.")]
        UPDATE_CURRENT_RECORD,
        [PersistenceTarget(SaveObjective.REVIEW_PERSIST_COPY, "Insert a SEPARATE saved identity after review, leaving saved sources unchanged. Also used when explicitly saving an unsaved new draft.")]
        CREATE_SEPARATE_RECORD,
        [PersistenceTarget(SaveObjective.REVIEW_DELETE_EXISTING, "Request review of removing the saved identity from the library, supported by the actual deletion passage.")]
        DELETE_CURRENT_RECORD
    }
    // Nested initialization avoids ordering dependencies between partial declarations.
    private static class PersistenceMetadata
    {
        public static readonly IReadOnlyDictionary<PersistenceIntent, PersistenceTargetAttribute> Targets =
            Enum.GetValues<PersistenceIntent>().ToDictionary(value => value,
                value => typeof(PersistenceIntent).GetField(value.ToString())!.GetCustomAttribute<PersistenceTargetAttribute>()
                    ?? throw new InvalidOperationException("Persistence target metadata is missing."));
        public static readonly IReadOnlyDictionary<PersistenceIntent, SaveObjective> Objectives =
            Targets.ToDictionary(pair => pair.Key, pair => pair.Value.Objective);
        public static readonly string Definitions = string.Join(" ", Targets.Select(pair => pair.Key + ": " + pair.Value.Description));
    }
    private static IReadOnlyDictionary<PersistenceIntent, SaveObjective> PersistenceObjectives => PersistenceMetadata.Objectives;
    private static string PersistenceDefinitions => PersistenceMetadata.Definitions;
    private static string PersistenceWireName(SaveObjective objective) =>
        PersistenceObjectives.Single(pair => pair.Value == objective).Key.ToString();
    private static JsonElement PersistenceContract() => JsonSerializer.SerializeToElement(new {
        type = "string", @enum = Enum.GetNames<PersistenceIntent>(), description = PersistenceDefinitions
    });
}
