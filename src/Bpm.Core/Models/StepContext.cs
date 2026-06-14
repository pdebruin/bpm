namespace Bpm.Core.Models;

/// <summary>
/// Mutable context that flows between activity steps within a single dispatch.
/// Activities can write values; subsequent steps can reference them in templates.
/// </summary>
public class StepContext
{
    private readonly Dictionary<string, string> _values = new();

    public void Set(string key, string value) => _values[key] = value;
    public string? Get(string key) => _values.GetValueOrDefault(key);

    /// <summary>
    /// Resolve {{StepContext.Key}} placeholders in a template string.
    /// </summary>
    public string Resolve(string template)
    {
        foreach (var (key, value) in _values)
        {
            template = template.Replace($"{{{{StepContext.{key}}}}}", value);
        }
        return template;
    }
}
