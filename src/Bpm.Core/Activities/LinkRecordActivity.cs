using Bpm.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xrm.Core.Services;

namespace Bpm.Core.Activities;

/// <summary>
/// Creates a RecordLink between two records via a named XRM relationship.
/// </summary>
public class LinkRecordActivity : IActivity
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<LinkRecordActivity> _logger;

    public LinkRecordActivity(IServiceProvider sp, ILogger<LinkRecordActivity> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public string TypeName => "LinkRecord";

    public async Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config,
        TransitionContext context,
        StepContext stepContext,
        CancellationToken ct = default)
    {
        var relationshipName = config.GetValueOrDefault("relationship", "");
        var parentId = Resolve(config.GetValueOrDefault("parentId", context.RecordId.ToString()), context, stepContext);
        var childId = Resolve(config.GetValueOrDefault("childId", ""), context, stepContext);

        if (string.IsNullOrEmpty(relationshipName) || string.IsNullOrEmpty(childId))
            return ActionOutcome.Failed;
        if (!Guid.TryParse(parentId, out var parent) || !Guid.TryParse(childId, out var child))
            return ActionOutcome.Failed;

        using var scope = _sp.CreateScope();
        var relationships = scope.ServiceProvider.GetRequiredService<IRelationshipService>();
        var records = scope.ServiceProvider.GetRequiredService<IRecordService>();

        var allRels = await relationships.GetAllAsync();
        var rel = allRels.FirstOrDefault(r => r.Name == relationshipName);
        if (rel is null)
            return ActionOutcome.Failed;

        await records.CreateLinkAsync(parent, rel.Id, child);

        _logger.LogInformation(
            "Linked: {Relationship} parent={ParentId} → child={ChildId}",
            relationshipName, parent, child);

        return ActionOutcome.Success;
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
