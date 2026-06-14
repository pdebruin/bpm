using Bpm.Core.Activities;
using Bpm.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Bpm.Core.Services;

/// <summary>
/// Core engine: evaluates transition action definitions against a context,
/// dispatches to activity implementations, and logs results.
/// </summary>
public class TransitionActionDispatcher
{
    private readonly ITransitionActionStore _store;
    private readonly IActionLog _log;
    private readonly IEnumerable<IActivity> _activities;
    private readonly ILogger<TransitionActionDispatcher> _logger;

    public TransitionActionDispatcher(
        ITransitionActionStore store,
        IActionLog log,
        IEnumerable<IActivity> activities,
        ILogger<TransitionActionDispatcher> logger)
    {
        _store = store;
        _log = log;
        _activities = activities;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate and execute all matching transition actions for the given context.
    /// Returns results for blocking actions; fires-and-forgets non-blocking ones.
    /// </summary>
    public async Task<List<TransitionActionResult>> DispatchAsync(
        TransitionContext context, CancellationToken ct = default)
    {
        var definitions = await _store.GetByTriggerAsync(context.EntityName, context.FieldName, ct);
        var results = new List<TransitionActionResult>();

        foreach (var definition in definitions)
        {
            if (!definition.Enabled) continue;
            if (!Matches(definition, context)) continue;

            var result = await ExecuteDefinitionAsync(definition, context, ct);
            results.Add(result);
            await _log.LogAsync(context, result, ct);

            if (definition.Blocking && !result.Success)
            {
                _logger.LogWarning(
                    "Blocking action '{Name}' failed for {Entity}/{RecordId}",
                    definition.Name, context.EntityName, context.RecordId);
                break;
            }
        }

        return results;
    }

    private static bool Matches(TransitionActionDefinition definition, TransitionContext context)
    {
        if (definition.FromValue is not null && definition.FromValue != context.OldValue)
            return false;
        if (definition.ToValue is not null && definition.ToValue != context.NewValue)
            return false;
        return true;
    }

    private async Task<TransitionActionResult> ExecuteDefinitionAsync(
        TransitionActionDefinition definition, TransitionContext context, CancellationToken ct)
    {
        var result = new TransitionActionResult
        {
            DefinitionId = definition.Id,
            DefinitionName = definition.Name
        };

        var stepContext = new StepContext();

        foreach (var step in definition.Steps)
        {
            var activity = _activities.FirstOrDefault(a => a.TypeName == step.ActivityType);
            if (activity is null)
            {
                result.StepResults.Add(new ActionStepResult
                {
                    ActivityType = step.ActivityType,
                    Outcome = ActionOutcome.Failed,
                    Error = $"Unknown activity type: {step.ActivityType}"
                });
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var outcome = await activity.ExecuteAsync(step.Config, context, stepContext, ct);
                sw.Stop();
                result.StepResults.Add(new ActionStepResult
                {
                    ActivityType = step.ActivityType,
                    Outcome = outcome,
                    Duration = sw.Elapsed
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Activity {Type} failed in action '{Name}'", step.ActivityType, definition.Name);
                result.StepResults.Add(new ActionStepResult
                {
                    ActivityType = step.ActivityType,
                    Outcome = ActionOutcome.Failed,
                    Error = ex.Message,
                    Duration = sw.Elapsed
                });
            }
        }

        return result;
    }
}
