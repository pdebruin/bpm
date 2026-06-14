# BPM Stage 1 — Architecture

## Overview

Stage 1 provides transition actions: side effects that fire when a field value
changes on a record. BPM depends on XRM — process definitions are stored as
XRM entity records, editable at runtime via the standard XRM UI and API.

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
│       ├── XrmTransitionActionStore                          │
│       │     └── reads "ProcessDefinition" XRM records       │
│       │                                                     │
│       ├── IActivity[] → coded activity implementations      │
│       │     ├── SendNotificationActivity                    │
│       │     ├── CreateRecordActivity (outputs to StepContext)│
│       │     ├── UpdateFieldActivity                         │
│       │     └── LinkRecordActivity                          │
│       │                                                     │
│       ├── StepContext → flows data between steps            │
│       │                                                     │
│       └── IActionLog → execution logging                    │
└─────────────────────────────────────────────────────────────┘
```

## Flow

1. XRM saves a record and detects a field change
2. XRM's `OnUpdatedAsync` lifecycle hook fires
3. Host's handler builds a `TransitionContext` with old/new values
4. Host calls `TransitionActionDispatcher.DispatchAsync(context)`
5. Dispatcher queries `XrmTransitionActionStore` (reads ProcessDefinition records)
6. For each match: executes steps in order via `IActivity` implementations
7. `StepContext` passes output between steps (e.g., created record ID)
8. Results are logged via `IActionLog`

## Process definitions as XRM entities

BPM ships a `BpmEntitySeeder` that creates a "ProcessDefinition" entity with:
- Name, Description, EntityName, FieldName, FromValue, ToValue
- StepsJson (JSON array of activity steps)
- Enabled, Blocking (booleans)

Users manage flow definitions the same way they manage any XRM entity:
via the Blazor UI, the REST API, or data seeding.

## Step context

Activities can output values for subsequent steps:
- `CreateRecordActivity` sets `StepContext.LastCreatedRecordId`
- `LinkRecordActivity` can reference it via `{{StepContext.LastCreatedRecordId}}`

Template syntax: `{{StepContext.Key}}` alongside existing `{{RecordId}}`, etc.

## Host responsibilities

The host must provide:

| Interface | Purpose |
|-----------|---------|
| `IActionLog` | Where to log execution results |
| `IRecordLifecycleHandler` | XRM hook that triggers BPM dispatch |

BPM provides everything else (store, activities, dispatcher, seeder).

## Testing

`ITransitionActionStore` interface remains for unit testing with `InMemoryActionStore`.
Activities are tested with fake implementations that don't need XRM running.
