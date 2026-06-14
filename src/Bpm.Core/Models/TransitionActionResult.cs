namespace Bpm.Core.Models;

/// <summary>
/// Full result of evaluating and executing a transition action definition.
/// </summary>
public class TransitionActionResult
{
    public Guid DefinitionId { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public List<ActionStepResult> StepResults { get; set; } = new();
    public bool Success => StepResults.All(r => r.Outcome != ActionOutcome.Failed);
}
