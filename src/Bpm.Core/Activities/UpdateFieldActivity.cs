using Bpm.Core.Models;
using Microsoft.Extensions.Logging;

namespace Bpm.Core.Activities;

/// <summary>
/// Updates a field on a record via IRecordProvider.
/// </summary>
public class UpdateFieldActivity : IActivity
{
    private readonly IRecordProvider _records;
    private readonly ILogger<UpdateFieldActivity> _logger;

    public UpdateFieldActivity(IRecordProvider records, ILogger<UpdateFieldActivity> logger)
    {
        _records = records;
        _logger = logger;
    }

    public string TypeName => "UpdateField";

    public async Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config,
        TransitionContext context,
        CancellationToken ct = default)
    {
        var entityName = config.GetValueOrDefault("entity", context.EntityName);
        var recordId = config.GetValueOrDefault("recordId", context.RecordId.ToString());
        var fieldName = config.GetValueOrDefault("field", "");
        var value = ResolveTemplate(config.GetValueOrDefault("value", ""), context);

        if (string.IsNullOrEmpty(fieldName))
            return ActionOutcome.Failed;

        if (!Guid.TryParse(recordId, out var id))
            return ActionOutcome.Failed;

        await _records.UpdateFieldAsync(entityName, id, fieldName, value, ct);

        _logger.LogInformation(
            "Field updated: {Entity}/{RecordId}.{Field} = {Value}",
            entityName, id, fieldName, value);

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
