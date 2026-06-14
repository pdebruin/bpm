using Bpm.Core.Models;

namespace Bpm.Core.Services;

/// <summary>
/// Logs action execution results. Host provides the implementation.
/// </summary>
public interface IActionLog
{
    Task LogAsync(TransitionContext context, TransitionActionResult result, CancellationToken ct = default);
}
