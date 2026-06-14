# BPM Stage 1 — Architecture

## Overview

Stage 1 provides transition actions: side effects that fire when a field value
changes on a record. No Dapr dependency — this stage is pure .NET.

## Components

```
┌─────────────────────────────────────────────────────────────┐
│ Host (e.g., CaseMgmt)                                       │
│                                                             │
│  IRecordLifecycleHandler (XRM hook)                         │
│       │                                                     │
│       ▼                                                     │
│  TransitionActionDispatcher (BPM)                           │
│       │                                                     │
│       ├── ITransitionActionStore → metadata definitions     │
│       │                                                     │
│       ├── IActivity[] → coded activity implementations      │
│       │     ├── SendNotificationActivity                    │
│       │     ├── CreateRecordActivity                        │
│       │     └── UpdateFieldActivity                         │
│       │                                                     │
│       ├── IRecordProvider → data platform adapter           │
│       │                                                     │
│       └── IActionLog → execution logging                    │
└─────────────────────────────────────────────────────────────┘
```

## Flow

1. XRM saves a record and detects a field change
2. XRM's `OnUpdatedAsync` lifecycle hook fires
3. Host's handler extracts old/new values and builds a `TransitionContext`
4. Host calls `TransitionActionDispatcher.DispatchAsync(context)`
5. Dispatcher queries `ITransitionActionStore` for matching definitions
6. For each match: executes steps in order via `IActivity` implementations
7. Results are logged via `IActionLog`
8. If a blocking action fails, dispatch stops

## Host responsibilities

The host must provide implementations for:

| Interface | Purpose |
|-----------|---------|
| `ITransitionActionStore` | Where process definitions live (DB, JSON file, in-memory) |
| `IRecordProvider` | How to create/update records in the data platform |
| `IActionLog` | Where to log execution results |
| `IRecordLifecycleHandler` | XRM hook that triggers BPM dispatch |

## Execution modes

- **Fire-and-forget** (default): action failures are logged as warnings; the
  original save succeeds.
- **Blocking**: action must succeed or the dispatcher returns a failure result.
  The host's lifecycle handler can throw to abort the save.

## Template resolution

Activity configs support `{{placeholder}}` syntax:
- `{{EntityName}}`, `{{RecordId}}`, `{{FieldName}}`
- `{{OldValue}}`, `{{NewValue}}`

## Testing

All interfaces have in-memory test doubles in `Bpm.Tests/TestDoubles.cs`.
No external dependencies required.
