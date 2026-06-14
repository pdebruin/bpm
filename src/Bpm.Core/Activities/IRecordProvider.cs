namespace Bpm.Core.Activities;

/// <summary>
/// Abstraction for data platform operations. The host provides an implementation
/// (e.g., XRM adapter, EF Core adapter, external API adapter).
/// </summary>
public interface IRecordProvider
{
    Task CreateRecordAsync(string entityName, Dictionary<string, string> fields, CancellationToken ct = default);
    Task UpdateFieldAsync(string entityName, Guid recordId, string fieldName, string value, CancellationToken ct = default);
}
