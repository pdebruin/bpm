using Bpm.Core.Activities;
using Bpm.Core.Models;
using Bpm.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bpm.Tests;

public class TransitionActionDispatcherTests
{
    private readonly InMemoryActionStore _store = new();
    private readonly InMemoryActionLog _log = new();
    private readonly InMemoryRecordProvider _records = new();
    private readonly TransitionActionDispatcher _dispatcher;

    public TransitionActionDispatcherTests()
    {
        var activities = new IActivity[]
        {
            new SendNotificationActivity(NullLogger<SendNotificationActivity>.Instance),
            new CreateRecordActivity(_records, NullLogger<CreateRecordActivity>.Instance),
            new UpdateFieldActivity(_records, NullLogger<UpdateFieldActivity>.Instance),
        };

        _dispatcher = new TransitionActionDispatcher(
            _store, _log, activities,
            NullLogger<TransitionActionDispatcher>.Instance);
    }

    [Fact]
    public async Task NoDefinitions_ReturnsEmpty()
    {
        var context = MakeContext("Case", "Status", "New", "Triaged");
        var results = await _dispatcher.DispatchAsync(context);
        Assert.Empty(results);
    }

    [Fact]
    public async Task MatchingDefinition_ExecutesSteps()
    {
        _store.Add(new TransitionActionDefinition
        {
            Name = "Notify on triage",
            EntityName = "Case",
            FieldName = "Status",
            FromValue = "New",
            ToValue = "Triaged",
            Steps = new()
            {
                new ActionStep { ActivityType = "SendNotification", Config = new() { ["to"] = "team@test.com", ["subject"] = "Case triaged" } }
            }
        });

        var context = MakeContext("Case", "Status", "New", "Triaged");
        var results = await _dispatcher.DispatchAsync(context);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Single(_log.Entries);
    }

    [Fact]
    public async Task NonMatchingTransition_Skipped()
    {
        _store.Add(new TransitionActionDefinition
        {
            Name = "Only on resolve",
            EntityName = "Case",
            FieldName = "Status",
            FromValue = "In Progress",
            ToValue = "Resolved",
            Steps = new()
            {
                new ActionStep { ActivityType = "SendNotification", Config = new() { ["to"] = "x" } }
            }
        });

        var context = MakeContext("Case", "Status", "New", "Triaged");
        var results = await _dispatcher.DispatchAsync(context);

        Assert.Empty(results);
    }

    [Fact]
    public async Task WildcardFrom_MatchesAnyOldValue()
    {
        _store.Add(new TransitionActionDefinition
        {
            Name = "Any to Closed",
            EntityName = "Case",
            FieldName = "Status",
            FromValue = null, // any
            ToValue = "Closed",
            Steps = new()
            {
                new ActionStep { ActivityType = "SendNotification", Config = new() { ["to"] = "mgr" } }
            }
        });

        var context = MakeContext("Case", "Status", "Resolved", "Closed");
        var results = await _dispatcher.DispatchAsync(context);

        Assert.Single(results);
        Assert.True(results[0].Success);
    }

    [Fact]
    public async Task CreateRecordActivity_CreatesRecord()
    {
        _store.Add(new TransitionActionDefinition
        {
            Name = "Create follow-up",
            EntityName = "Case",
            FieldName = "Status",
            ToValue = "Triaged",
            Steps = new()
            {
                new ActionStep
                {
                    ActivityType = "CreateRecord",
                    Config = new()
                    {
                        ["entity"] = "Activity",
                        ["field.Subject"] = "Follow up on {{RecordId}}",
                        ["field.Type"] = "Task",
                        ["field.Status"] = "Open"
                    }
                }
            }
        });

        var recordId = Guid.NewGuid();
        var context = MakeContext("Case", "Status", "New", "Triaged", recordId);
        var results = await _dispatcher.DispatchAsync(context);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Single(_records.CreatedRecords);
        Assert.Equal("Activity", _records.CreatedRecords[0].Entity);
        Assert.Equal($"Follow up on {recordId}", _records.CreatedRecords[0].Fields["Subject"]);
    }

    [Fact]
    public async Task UpdateFieldActivity_UpdatesField()
    {
        var recordId = Guid.NewGuid();
        _store.Add(new TransitionActionDefinition
        {
            Name = "Set priority on escalation",
            EntityName = "Case",
            FieldName = "Status",
            ToValue = "In Progress",
            Steps = new()
            {
                new ActionStep
                {
                    ActivityType = "UpdateField",
                    Config = new()
                    {
                        ["entity"] = "Case",
                        ["recordId"] = recordId.ToString(),
                        ["field"] = "Priority",
                        ["value"] = "High"
                    }
                }
            }
        });

        var context = MakeContext("Case", "Status", "Triaged", "In Progress", recordId);
        var results = await _dispatcher.DispatchAsync(context);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Single(_records.UpdatedFields);
        Assert.Equal("Priority", _records.UpdatedFields[0].Field);
        Assert.Equal("High", _records.UpdatedFields[0].Value);
    }

    [Fact]
    public async Task DisabledDefinition_Skipped()
    {
        _store.Add(new TransitionActionDefinition
        {
            Name = "Disabled action",
            EntityName = "Case",
            FieldName = "Status",
            Enabled = false,
            Steps = new()
            {
                new ActionStep { ActivityType = "SendNotification", Config = new() { ["to"] = "x" } }
            }
        });

        var context = MakeContext("Case", "Status", "New", "Triaged");
        var results = await _dispatcher.DispatchAsync(context);

        Assert.Empty(results);
    }

    [Fact]
    public async Task UnknownActivityType_FailsStep()
    {
        _store.Add(new TransitionActionDefinition
        {
            Name = "Bad step",
            EntityName = "Case",
            FieldName = "Status",
            Steps = new()
            {
                new ActionStep { ActivityType = "DoesNotExist", Config = new() }
            }
        });

        var context = MakeContext("Case", "Status", "New", "Triaged");
        var results = await _dispatcher.DispatchAsync(context);

        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Equal(ActionOutcome.Failed, results[0].StepResults[0].Outcome);
        Assert.Contains("Unknown activity type", results[0].StepResults[0].Error);
    }

    [Fact]
    public async Task MultipleSteps_ExecuteInOrder()
    {
        _store.Add(new TransitionActionDefinition
        {
            Name = "Multi-step",
            EntityName = "Case",
            FieldName = "Status",
            ToValue = "Resolved",
            Steps = new()
            {
                new ActionStep { ActivityType = "SendNotification", Config = new() { ["to"] = "customer" } },
                new ActionStep { ActivityType = "SendNotification", Config = new() { ["to"] = "manager" } },
            }
        });

        var context = MakeContext("Case", "Status", "In Progress", "Resolved");
        var results = await _dispatcher.DispatchAsync(context);

        Assert.Single(results);
        Assert.Equal(2, results[0].StepResults.Count);
        Assert.All(results[0].StepResults, r => Assert.Equal(ActionOutcome.Success, r.Outcome));
    }

    [Fact]
    public async Task BlockingAction_StopsOnFailure()
    {
        _store.Add(new TransitionActionDefinition
        {
            Name = "Blocking bad",
            EntityName = "Case",
            FieldName = "Status",
            Blocking = true,
            Steps = new()
            {
                new ActionStep { ActivityType = "DoesNotExist", Config = new() }
            }
        });
        _store.Add(new TransitionActionDefinition
        {
            Name = "Should not run",
            EntityName = "Case",
            FieldName = "Status",
            Steps = new()
            {
                new ActionStep { ActivityType = "SendNotification", Config = new() { ["to"] = "x" } }
            }
        });

        var context = MakeContext("Case", "Status", "New", "Triaged");
        var results = await _dispatcher.DispatchAsync(context);

        // Only the first (blocking, failed) definition runs
        Assert.Single(results);
        Assert.False(results[0].Success);
    }

    private static TransitionContext MakeContext(
        string entity, string field, string? from, string? to, Guid? recordId = null)
    {
        return new TransitionContext
        {
            EntityName = entity,
            RecordId = recordId ?? Guid.NewGuid(),
            FieldName = field,
            OldValue = from,
            NewValue = to,
            UserId = "test-user"
        };
    }
}
