using System.Text.Json;
using Bpm.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xrm.Core.Services;

namespace Bpm.Core.Activities;

/// <summary>
/// Updates a field on a record via XRM services.
/// </summary>
public class UpdateFieldActivity : IActivity
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<UpdateFieldActivity> _logger;

    public UpdateFieldActivity(IServiceProvider sp, ILogger<UpdateFieldActivity> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public string TypeName => "UpdateField";

    public async Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config,
        TransitionContext context,
        StepContext stepContext,
        CancellationToken ct = default)
    {
        var entityName = Resolve(config.GetValueOrDefault("entity", context.EntityName), context, stepContext);
        var recordId = Resolve(config.GetValueOrDefault("recordId", context.RecordId.ToString()), context, stepContext);
        var fieldName = config.GetValueOrDefault("field", "");
        var value = Resolve(config.GetValueOrDefault("value", ""), context, stepContext);

        if (string.IsNullOrEmpty(fieldName))
            return ActionOutcome.Failed;
        if (!Guid.TryParse(recordId, out var id))
            return ActionOutcome.Failed;

        using var scope = _sp.CreateScope();
        var entities = scope.ServiceProvider.GetRequiredService<IEntityService>();
        var records = scope.ServiceProvider.GetRequiredService<IRecordService>();

        var allEntities = await entities.GetAllAsync();
        var entity = allEntities.FirstOrDefault(e => e.Name == entityName);
        if (entity is null)
            return ActionOutcome.Failed;

        var record = await records.GetByIdAsync(entity.Id, id);
        if (record is null)
            return ActionOutcome.Failed;

        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(record.DataJson)!;
        data[fieldName] = value;
        var result = await records.UpdateAsync(entity.Id, id, JsonSerializer.Serialize(data));

        _logger.LogInformation(
            "Field updated: {Entity}/{RecordId}.{Field} = {Value}",
            entityName, id, fieldName, value);

        return result.Success ? ActionOutcome.Success : ActionOutcome.Failed;
    }

    private static string Resolve(string template, TransitionContext context, StepContext stepContext)
    {
        template = template
            .Replace("{{EntityName}}", context.EntityName)
            .Replace("{{RecordId}}", context.RecordId.ToString())
            .Replace("{{FieldName}}", context.FieldName)
            .Replace("{{OldValue}}", context.OldValue ?? "")
            .Replace("{{NewValue}}", context.NewValue ?? "");
        return stepContext.Resolve(template);
    }
}
