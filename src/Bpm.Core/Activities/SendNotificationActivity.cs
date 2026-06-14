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
        StepContext stepContext,
        CancellationToken ct = default)
    {
        var to = Resolve(config.GetValueOrDefault("to", ""), context, stepContext);
        var template = config.GetValueOrDefault("template", "default");
        var subject = Resolve(config.GetValueOrDefault("subject", ""), context, stepContext);

        _logger.LogInformation(
            "Notification sent: to={To}, template={Template}, subject={Subject}, entity={Entity}, record={RecordId}",
            to, template, subject, context.EntityName, context.RecordId);

        return Task.FromResult(ActionOutcome.Success);
    }

    private static string Resolve(string template, TransitionContext context, StepContext stepContext)
    {
        template = template
            .Replace("{{EntityName}}", context.EntityName)
            .Replace("{{RecordId}}", context.RecordId.ToString())
            .Replace("{{FieldName}}", context.FieldName)
            .Replace("{{OldValue}}", context.OldValue ?? "")
            .Replace("{{NewValue}}", context.NewValue ?? "");
        return stepContext.Resolve(template);
    }
}
