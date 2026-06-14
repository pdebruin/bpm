using Bpm.Core.Models;
using Microsoft.Extensions.Logging;

namespace Bpm.Core.Activities;

/// <summary>
/// Creates a record in the data platform via IRecordProvider.
/// </summary>
public class CreateRecordActivity : IActivity
{
    private readonly IRecordProvider _records;
    private readonly ILogger<CreateRecordActivity> _logger;

    public CreateRecordActivity(IRecordProvider records, ILogger<CreateRecordActivity> logger)
    {
        _records = records;
        _logger = logger;
    }

    public string TypeName => "CreateRecord";

    public async Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config,
        TransitionContext context,
        CancellationToken ct = default)
    {
        var entityName = config.GetValueOrDefault("entity", "");
        if (string.IsNullOrEmpty(entityName))
            return ActionOutcome.Failed;

        // Build field values from config (keys starting with "field.")
        var fields = config
            .Where(kv => kv.Key.StartsWith("field."))
            .ToDictionary(
                kv => kv.Key["field.".Length..],
                kv => ResolveTemplate(kv.Value, context));

        await _records.CreateRecordAsync(entityName, fields, ct);

        _logger.LogInformation(
            "Record created: entity={Entity}, triggered by {SourceEntity}/{RecordId} {Field} → {NewValue}",
            entityName, context.EntityName, context.RecordId, context.FieldName, context.NewValue);

        return ActionOutcome.Success;
    }

    private static string ResolveTemplate(string template, TransitionContext context)
    {
        return template
            .Replace("{{EntityName}}", context.EntityName)
            .Replace("{{RecordId}}", context.RecordId.ToString())
            .Replace("{{FieldName}}", context.FieldName)
            .Replace("{{OldValue}}", context.OldValue ?? "")
            .Replace("{{NewValue}}", context.NewValue ?? "");
    }
}
