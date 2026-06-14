using Bpm.Core.Activities;
using Bpm.Core.Seeding;
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
    /// Uses XRM as the backing store for process definitions.
    /// </summary>
    public static IServiceCollection AddBpmCore(this IServiceCollection services)
    {
        // Engine
        services.AddSingleton<TransitionActionDispatcher>();
        services.AddSingleton<ITransitionActionStore, XrmTransitionActionStore>();

        // Seeder
        services.AddTransient<BpmEntitySeeder>();

        // Built-in activities
        services.AddSingleton<IActivity, SendNotificationActivity>();
        services.AddSingleton<IActivity, CreateRecordActivity>();
        services.AddSingleton<IActivity, UpdateFieldActivity>();
        services.AddSingleton<IActivity, LinkRecordActivity>();

        return services;
    }
}
