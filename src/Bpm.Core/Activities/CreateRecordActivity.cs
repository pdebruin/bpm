using System.Text.Json;
using Bpm.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xrm.Core.Services;

namespace Bpm.Core.Activities;

/// <summary>
/// Creates a record in XRM and outputs the new record's ID to StepContext.
/// </summary>
public class CreateRecordActivity : IActivity
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<CreateRecordActivity> _logger;

    public CreateRecordActivity(IServiceProvider sp, ILogger<CreateRecordActivity> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public string TypeName => "CreateRecord";

    public async Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config,
        TransitionContext context,
        StepContext stepContext,
        CancellationToken ct = default)
    {
        var entityName = Resolve(config.GetValueOrDefault("entity", ""), context, stepContext);
        if (string.IsNullOrEmpty(entityName))
            return ActionOutcome.Failed;

        // Build field values from config (keys starting with "field.")
        var fields = config
            .Where(kv => kv.Key.StartsWith("field."))
            .ToDictionary(
                kv => kv.Key["field.".Length..],
                kv => Resolve(kv.Value, context, stepContext));

        using var scope = _sp.CreateScope();
        var entities = scope.ServiceProvider.GetRequiredService<IEntityService>();
        var records = scope.ServiceProvider.GetRequiredService<IRecordService>();

        var allEntities = await entities.GetAllAsync();
        var entity = allEntities.FirstOrDefault(e => e.Name == entityName);
        if (entity is null)
            return ActionOutcome.Failed;

        var json = JsonSerializer.Serialize(fields);
        var result = await records.CreateAsync(entity.Id, json);

        if (result.Success && result.Record is not null)
        {
            stepContext.Set("LastCreatedRecordId", result.Record.Id.ToString());

            _logger.LogInformation(
                "Record created: entity={Entity}, id={Id}, triggered by {SourceEntity}/{RecordId}",
                entityName, result.Record.Id, context.EntityName, context.RecordId);
        }

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
