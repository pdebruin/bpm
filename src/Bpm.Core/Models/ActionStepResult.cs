namespace Bpm.Core.Models;

/// <summary>
/// Result of executing a single action step.
/// </summary>
public class ActionStepResult
{
    public string ActivityType { get; set; } = string.Empty;
    public ActionOutcome Outcome { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
}

public enum ActionOutcome
{
    Success,
    Failed,
    Skipped
}
