using System.Text.Json;
using Bpm.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xrm.Core.Services;

namespace Bpm.Core.Services;

/// <summary>
/// Reads transition action definitions from a "ProcessDefinition" XRM entity.
/// Each record represents one flow definition with its steps stored as JSON.
/// </summary>
public class XrmTransitionActionStore : ITransitionActionStore
{
    private readonly IServiceProvider _sp;

    public XrmTransitionActionStore(IServiceProvider sp)
    {
        _sp = sp;
    }

    public async Task<IReadOnlyList<TransitionActionDefinition>> GetByTriggerAsync(
        string entityName, string fieldName, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var entities = scope.ServiceProvider.GetRequiredService<IEntityService>();
        var records = scope.ServiceProvider.GetRequiredService<IRecordService>();

        var allEntities = await entities.GetAllAsync();
        var processDefEntity = allEntities.FirstOrDefault(e => e.Name == "ProcessDefinition");
        if (processDefEntity is null)
            return Array.Empty<TransitionActionDefinition>();

        var allRecords = (await records.GetAllAsync(processDefEntity.Id, pageSize: 1000)).Records;
        var results = new List<TransitionActionDefinition>();

        foreach (var record in allRecords)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(record.DataJson);
            if (data is null) continue;

            var recEntityName = GetString(data, "EntityName");
            var recFieldName = GetString(data, "FieldName");

            if (recEntityName != entityName || recFieldName != fieldName)
                continue;

            var definition = new TransitionActionDefinition
            {
                Id = record.Id,
                Name = GetString(data, "Name") ?? "",
                Description = GetString(data, "Description"),
                Enabled = GetBool(data, "Enabled", true),
                EntityName = recEntityName ?? "",
                FieldName = recFieldName ?? "",
                FromValue = GetString(data, "FromValue"),
                ToValue = GetString(data, "ToValue"),
                Blocking = GetBool(data, "Blocking", false),
                Steps = DeserializeSteps(GetString(data, "StepsJson"))
            };

            results.Add(definition);
        }

        return results;
    }

    private static List<ActionStep> DeserializeSteps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new();

        try
        {
            return JsonSerializer.Deserialize<List<ActionStep>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static string? GetString(Dictionary<string, JsonElement> data, string key)
    {
        if (data.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    private static bool GetBool(Dictionary<string, JsonElement> data, string key, bool defaultValue)
    {
        if (data.TryGetValue(key, out var el))
        {
            if (el.ValueKind == JsonValueKind.True) return true;
            if (el.ValueKind == JsonValueKind.False) return false;
            if (el.ValueKind == JsonValueKind.String)
                return bool.TryParse(el.GetString(), out var b) && b;
        }
        return defaultValue;
    }
}
