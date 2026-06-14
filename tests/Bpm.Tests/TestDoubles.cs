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
/// In-memory record provider for testing.
/// </summary>
public class InMemoryRecordProvider : IRecordProvider
{
    public List<(string Entity, Dictionary<string, string> Fields)> CreatedRecords { get; } = new();
    public List<(string Entity, Guid RecordId, string Field, string Value)> UpdatedFields { get; } = new();

    public Task CreateRecordAsync(string entityName, Dictionary<string, string> fields, CancellationToken ct = default)
    {
        CreatedRecords.Add((entityName, fields));
        return Task.CompletedTask;
    }

    public Task UpdateFieldAsync(string entityName, Guid recordId, string fieldName, string value, CancellationToken ct = default)
    {
        UpdatedFields.Add((entityName, recordId, fieldName, value));
        return Task.CompletedTask;
    }
}
