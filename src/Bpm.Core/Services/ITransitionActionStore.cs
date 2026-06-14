using Bpm.Core.Models;

namespace Bpm.Core.Services;

/// <summary>
/// Provides transition action definitions. Host implements this backed by
/// a database, JSON file, or in-memory collection.
/// </summary>
public interface ITransitionActionStore
{
    Task<IReadOnlyList<TransitionActionDefinition>> GetByTriggerAsync(
        string entityName, string fieldName, CancellationToken ct = default);
}
