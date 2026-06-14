using Bpm.Core.Models;

namespace Bpm.Core.Activities;

/// <summary>
/// Interface for a coded activity type. Implementations are the "Lego bricks"
/// that process definitions compose via metadata.
/// </summary>
public interface IActivity
{
    /// <summary>
    /// Unique type name used in ActionStep.ActivityType (e.g., "SendNotification").
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Execute the activity with the given configuration and trigger context.
    /// </summary>
    Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config,
        TransitionContext context,
        CancellationToken ct = default);
}
