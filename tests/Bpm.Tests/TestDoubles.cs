using Bpm.Core.Activities;
using Bpm.Core.Models;
using Bpm.Core.Services;

namespace Bpm.Tests;

/// <summary>
/// In-memory implementation of ITransitionActionStore for testing.
/// </summary>
public class InMemoryActionStore : ITransitionActionStore
{
    private readonly List<TransitionActionDefinition> _definitions = new();

    public void Add(TransitionActionDefinition definition) => _definitions.Add(definition);

    public Task<IReadOnlyList<TransitionActionDefinition>> GetByTriggerAsync(
        string entityName, string fieldName, CancellationToken ct = default)
    {
        var matches = _definitions
            .Where(d => d.EntityName == entityName && d.FieldName == fieldName)
            .ToList();
        return Task.FromResult<IReadOnlyList<TransitionActionDefinition>>(matches);
    }
}

/// <summary>
/// In-memory action log for test assertions.
/// </summary>
public class InMemoryActionLog : IActionLog
{
    public List<(TransitionContext Context, TransitionActionResult Result)> Entries { get; } = new();

    public Task LogAsync(TransitionContext context, TransitionActionResult result, CancellationToken ct = default)
    {
        Entries.Add((context, result));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Fake activity for testing that always succeeds and records calls.
/// </summary>
public class FakeActivity : IActivity
{
    public string TypeName { get; }
    public List<(Dictionary<string, string> Config, TransitionContext Context)> Calls { get; } = new();

    public FakeActivity(string typeName) => TypeName = typeName;

    public Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config,
        TransitionContext context,
        StepContext stepContext,
        CancellationToken ct = default)
    {
        Calls.Add((config, context));
        return Task.FromResult(ActionOutcome.Success);
    }
}
