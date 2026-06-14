namespace Bpm.Core.Models;

/// <summary>
/// Context passed to activities when a transition fires.
/// </summary>
public class TransitionContext
{
    public string EntityName { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? UserId { get; set; }
    public Dictionary<string, string> RecordData { get; set; } = new();
}
