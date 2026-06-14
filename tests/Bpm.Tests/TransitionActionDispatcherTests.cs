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
    private readonly TransitionActionDispatcher _dispatcher;
    private readonly FakeActivity _sendNotification = new("SendNotification");
    private readonly FakeActivity _createRecord = new("CreateRecord");
    private readonly FakeActivity _updateField = new("UpdateField");
    private readonly FakeActivity _linkRecord = new("LinkRecord");

    public TransitionActionDispatcherTests()
    {
        IActivity[] activities = [_sendNotification, _createRecord, _updateField, _linkRecord];

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
        Assert.Single(_sendNotification.Calls);
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
            FromValue = null,
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
                new ActionStep { ActivityType = "CreateRecord", Config = new() { ["entity"] = "Activity" } },
                new ActionStep { ActivityType = "LinkRecord", Config = new() { ["relationship"] = "CaseActivities" } },
            }
        });

        var context = MakeContext("Case", "Status", "In Progress", "Resolved");
        var results = await _dispatcher.DispatchAsync(context);

        Assert.Single(results);
        Assert.Equal(2, results[0].StepResults.Count);
        Assert.All(results[0].StepResults, r => Assert.Equal(ActionOutcome.Success, r.Outcome));
        Assert.Single(_createRecord.Calls);
        Assert.Single(_linkRecord.Calls);
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

        Assert.Single(results);
        Assert.False(results[0].Success);
    }

    [Fact]
    public async Task StepContext_FlowsBetweenSteps()
    {
        // Use a custom activity that writes to StepContext
        var writerActivity = new StepContextWriterActivity("Writer");
        var readerActivity = new StepContextReaderActivity("Reader");

        var dispatcher = new TransitionActionDispatcher(
            _store, _log, new IActivity[] { writerActivity, readerActivity },
            NullLogger<TransitionActionDispatcher>.Instance);

        _store.Add(new TransitionActionDefinition
        {
            Name = "Context test",
            EntityName = "Case",
            FieldName = "Status",
            Steps = new()
            {
                new ActionStep { ActivityType = "Writer", Config = new() },
                new ActionStep { ActivityType = "Reader", Config = new() },
            }
        });

        var context = MakeContext("Case", "Status", "New", "Triaged");
        await dispatcher.DispatchAsync(context);

        Assert.Equal("written-value", readerActivity.ReadValue);
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

// Helper activities for StepContext test
file class StepContextWriterActivity : IActivity
{
    public string TypeName { get; }
    public StepContextWriterActivity(string name) => TypeName = name;

    public Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config, TransitionContext context, StepContext stepContext, CancellationToken ct)
    {
        stepContext.Set("TestKey", "written-value");
        return Task.FromResult(ActionOutcome.Success);
    }
}

file class StepContextReaderActivity : IActivity
{
    public string TypeName { get; }
    public string? ReadValue { get; private set; }
    public StepContextReaderActivity(string name) => TypeName = name;

    public Task<ActionOutcome> ExecuteAsync(
        Dictionary<string, string> config, TransitionContext context, StepContext stepContext, CancellationToken ct)
    {
        ReadValue = stepContext.Get("TestKey");
        return Task.FromResult(ActionOutcome.Success);
    }
}
