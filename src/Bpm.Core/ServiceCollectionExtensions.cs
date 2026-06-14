using Bpm.Core.Activities;
using Bpm.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bpm.Core;

/// <summary>
/// Extension methods to register BPM Stage 1 services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers BPM core services and built-in activity types.
    /// Host must also register ITransitionActionStore, IActionLog, and IRecordProvider.
    /// </summary>
    public static IServiceCollection AddBpmCore(this IServiceCollection services)
    {
        services.AddSingleton<TransitionActionDispatcher>();

        // Built-in activities
        services.AddSingleton<IActivity, SendNotificationActivity>();
        services.AddSingleton<IActivity, CreateRecordActivity>();
        services.AddSingleton<IActivity, UpdateFieldActivity>();

        return services;
    }
}
