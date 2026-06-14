namespace Bpm.Core.Models;

/// <summary>
/// Metadata defining what actions to execute when a field transitions between values.
/// </summary>
public class TransitionActionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;

    // Trigger: which entity/field/transition fires this
    public string EntityName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? FromValue { get; set; }  // null = any
    public string? ToValue { get; set; }    // null = any

    // Actions to execute (in order)
    public List<ActionStep> Steps { get; set; } = new();

    // Execution mode
    public bool Blocking { get; set; } = false;
}

/// <summary>
/// A single action step within a transition action definition.
/// </summary>
public class ActionStep
{
    public string ActivityType { get; set; } = string.Empty;
    public Dictionary<string, string> Config { get; set; } = new();
}
