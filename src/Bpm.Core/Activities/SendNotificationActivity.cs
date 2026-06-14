using Bpm.Core.Models;
using Microsoft.Extensions.Logging;

namespace Bpm.Core.Activities;

/// <summary>
/// Sends a notification (logs it in Stage 1; real implementations can override).
/// </summary>
public class SendNotificationActivity : IActivity
{
    private readonly ILogger<SendNotificationActivity> _logger;

    public SendNotificationActivity(ILogger<SendNotificationActivity> logger)
    {
        _logger = logger;
    }

    public string TypeName => "SendNotification";

    public Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config,
        TransitionContext context,
        CancellationToken ct = default)
    {
        var to = ResolveTemplate(config.GetValueOrDefault("to", ""), context);
        var template = config.GetValueOrDefault("template", "default");
        var subject = ResolveTemplate(config.GetValueOrDefault("subject", ""), context);

        _logger.LogInformation(
            "Notification sent: to={To}, template={Template}, subject={Subject}, entity={Entity}, record={RecordId}",
            to, template, subject, context.EntityName, context.RecordId);

        return Task.FromResult(ActionOutcome.Success);
    }

    private static string ResolveTemplate(string template, TransitionContext context)
    {
        return template
            .Replace("{{EntityName}}", context.EntityName)
            .Replace("{{RecordId}}", context.RecordId.ToString())
            .Replace("{{FieldName}}", context.FieldName)
            .Replace("{{OldValue}}", context.OldValue ?? "")
            .Replace("{{NewValue}}", context.NewValue ?? "");
    }
}
