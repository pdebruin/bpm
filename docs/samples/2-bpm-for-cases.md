# Sample: XRM for Cases — BPM Stage 1 Integration

This sample shows how to integrate BPM Stage 1 (transition actions) into a
CaseMgmt host that runs on XRM.

## Scenario

When a Case's **Status** transitions:
- `New → Triaged`: create a follow-up Activity and send a notification
- `* → Closed`: send a resolution notification

## Prerequisites

- CaseMgmt host running on XRM (accounts, contacts, activities, cases)
- BPM Stage 1 library (`Bpm.Core`)

## Integration steps

### 1. Add project reference

```xml
<!-- CaseMgmt.Server.csproj -->
<ProjectReference Include="../../../bpm/src/Bpm.Core/Bpm.Core.csproj" />
```

### 2. Implement IRecordProvider (XRM adapter)

Bridges BPM's `IRecordProvider` to XRM's `IRecordService`:

```csharp
public class XrmRecordProvider : IRecordProvider
{
    private readonly IRecordService _records;
    private readonly IEntityService _entities;

    public XrmRecordProvider(IRecordService records, IEntityService entities)
    {
        _records = records;
        _entities = entities;
    }

    public async Task CreateRecordAsync(string entityName, Dictionary<string, string> fields, CancellationToken ct)
    {
        var entity = (await _entities.GetAllAsync()).First(e => e.Name == entityName);
        var json = JsonSerializer.Serialize(fields);
        await _records.CreateAsync(entity.Id, json);
    }

    public async Task UpdateFieldAsync(string entityName, Guid recordId, string fieldName, string value, CancellationToken ct)
    {
        var entity = (await _entities.GetAllAsync()).First(e => e.Name == entityName);
        var record = await _records.GetByIdAsync(entity.Id, recordId);
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(record.DataJson)!;
        data[fieldName] = value;
        await _records.UpdateAsync(entity.Id, recordId, JsonSerializer.Serialize(data));
    }
}
```

### 3. Implement ITransitionActionStore

Seeds the action definitions for the Case entity:

```csharp
public class CaseActionStore : ITransitionActionStore
{
    private static readonly List<TransitionActionDefinition> _definitions = new()
    {
        new()
        {
            Name = "Create follow-up on triage",
            EntityName = "Case",
            FieldName = "Status",
            FromValue = "New",
            ToValue = "Triaged",
            Steps = new()
            {
                new ActionStep
                {
                    ActivityType = "CreateRecord",
                    Config = new()
                    {
                        ["entity"] = "Activity",
                        ["field.Subject"] = "Triage follow-up for {{RecordId}}",
                        ["field.Type"] = "Task",
                        ["field.Status"] = "Open",
                        ["field.Priority"] = "Normal"
                    }
                },
                new ActionStep
                {
                    ActivityType = "SendNotification",
                    Config = new()
                    {
                        ["to"] = "support-team",
                        ["template"] = "case-triaged",
                        ["subject"] = "Case triaged: {{RecordId}}"
                    }
                }
            }
        },
        new()
        {
            Name = "Notify on case closed",
            EntityName = "Case",
            FieldName = "Status",
            FromValue = null, // any status
            ToValue = "Closed",
            Steps = new()
            {
                new ActionStep
                {
                    ActivityType = "SendNotification",
                    Config = new()
                    {
                        ["to"] = "customer",
                        ["template"] = "case-closed",
                        ["subject"] = "Your case has been resolved"
                    }
                }
            }
        }
    };

    public Task<IReadOnlyList<TransitionActionDefinition>> GetByTriggerAsync(
        string entityName, string fieldName, CancellationToken ct = default)
    {
        var matches = _definitions
            .Where(d => d.EntityName == entityName && d.FieldName == fieldName)
            .ToList();
        return Task.FromResult<IReadOnlyList<TransitionActionDefinition>>(matches);
    }
}
```

### 4. Implement IActionLog

```csharp
public class ConsoleActionLog : IActionLog
{
    private readonly ILogger<ConsoleActionLog> _logger;

    public ConsoleActionLog(ILogger<ConsoleActionLog> logger) => _logger = logger;

    public Task LogAsync(TransitionContext context, TransitionActionResult result, CancellationToken ct)
    {
        foreach (var step in result.StepResults)
        {
            _logger.LogInformation(
                "BPM [{Action}] {Activity}: {Outcome} ({Duration}ms){Error}",
                result.DefinitionName, step.ActivityType, step.Outcome,
                step.Duration.TotalMilliseconds,
                step.Error is not null ? $" — {step.Error}" : "");
        }
        return Task.CompletedTask;
    }
}
```

### 5. Bridge XRM lifecycle hook → BPM dispatcher

```csharp
public class BpmLifecycleHandler : IRecordLifecycleHandler
{
    private readonly TransitionActionDispatcher _dispatcher;

    public BpmLifecycleHandler(TransitionActionDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task OnUpdatedAsync(Record record, string oldDataJson, EntityDefinition entity, CancellationToken ct)
    {
        using var oldDoc = JsonDocument.Parse(oldDataJson);
        using var newDoc = JsonDocument.Parse(record.DataJson);

        // Find fields that changed
        foreach (var prop in newDoc.RootElement.EnumerateObject())
        {
            var newVal = prop.Value.ToString();
            var oldVal = oldDoc.RootElement.TryGetProperty(prop.Name, out var ov) ? ov.ToString() : null;

            if (newVal != oldVal)
            {
                var context = new TransitionContext
                {
                    EntityName = entity.Name,
                    RecordId = record.Id,
                    FieldName = prop.Name,
                    OldValue = oldVal,
                    NewValue = newVal,
                    UserId = "system" // replace with real user context
                };

                await _dispatcher.DispatchAsync(context, ct);
            }
        }
    }
}
```

### 6. Register services in Program.cs

```csharp
// BPM Stage 1
builder.Services.AddBpmCore();
builder.Services.AddSingleton<ITransitionActionStore, CaseActionStore>();
builder.Services.AddSingleton<IActionLog, ConsoleActionLog>();
builder.Services.AddScoped<IRecordProvider, XrmRecordProvider>();
builder.Services.AddTransient<IRecordLifecycleHandler, BpmLifecycleHandler>();
```

## Expected behavior

1. Open CaseMgmt at http://localhost:5200
2. Navigate to Cases → open case CS00003 (Status: "New")
3. Change Status to "Triaged" → Save
4. **BPM fires**: creates a follow-up Activity + logs notification
5. Navigate to Activities → verify the new "Triage follow-up" task exists
6. Change Case status to "Closed"
7. **BPM fires**: logs the closed notification

## Console output

```
info: BPM [Create follow-up on triage] CreateRecord: Success (12ms)
info: BPM [Create follow-up on triage] SendNotification: Success (1ms)
info: BPM [Notify on case closed] SendNotification: Success (0ms)
```
