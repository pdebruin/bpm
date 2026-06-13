# BPM — Business Process Management Engine

## Vision

A standalone, metadata-driven process orchestration engine that adds durable
workflows, service invocation, and event-driven communication to any
application. Like XRM ships an empty canvas for data, BPM ships an **empty
canvas for processes** — users compose process definitions from built-in
activity types and configure routing rules at runtime without code changes or
redeployment. New activity types require a developer; new processes do not.

Designed to complement data platforms like XRM — but usable independently with
any data source. Ships as a NuGet package that hosts can wire into their
ASP.NET Core application.

## Guiding Principles

- **Metadata-driven processes** — process definitions are data, not code. The
  engine interprets them at runtime. Adding a step or changing a condition never
  requires a deploy. Running instances pin to a definition version.
- **Separate concern** — BPM handles *process* (what happens, in what order,
  with what conditions and timeouts). The data platform handles *state* (what
  things exist and what their current values are).
- **Dapr-first** — built on Dapr's workflow, pub/sub, service invocation, and
  bindings APIs. Reuse Dapr's built-in capabilities (retries, timers, state
  stores) rather than building custom infrastructure. Push back on features that
  duplicate what Dapr already provides.
- **Loosely coupled to data sources** — BPM references entities by name, not by
  compile-time type. Works with XRM entities, EF Core models, or external
  systems — the host provides an adapter.
- **Host-owned topology** — the BPM library defines the engine and built-in
  activity types. The host project (e.g., ERP) decides which Dapr components to
  use, what events to publish, and which services to call.
- **Progressive adoption** — each stage is independently useful. A host can use
  workflows without pub/sub, or pub/sub without service invocation.
- **Composable** — processes can trigger other processes, reference shared
  activities, and compose into larger flows.
- **Testable in isolation** — process logic can be unit-tested without Dapr
  running, using in-memory fakes for all infrastructure interfaces.

---

## Tech Stack

| Layer          | Choice                                |
|----------------|---------------------------------------|
| Runtime        | .NET 10 (ASP.NET Core)                |
| Orchestration  | Dapr Workflow (.NET SDK)              |
| Messaging      | Dapr Pub/Sub                          |
| Invocation     | Dapr Service Invocation               |
| Persistence    | Dapr State Store (host-configured)    |
| Tests          | xUnit + in-memory Dapr test doubles   |

---

## Stages

Implementation is staged. Each stage is independently valuable and builds on
the previous one.

### Stage 1 — Side effects on state transitions

Trigger actions when a field value changes (e.g., send notification, create a
follow-up record). This bridges XRM's lifecycle hooks to BPM's action system.

### Stage 2 — Durable workflows

Multi-step processes that span hours or days: approval chains, SLA timers,
escalation paths, retry policies. Survives application restarts.

### Stage 3 — External service invocation

Resilient calls to external systems (ERP APIs, payment providers, document
generation) with retries, circuit breakers, and timeout handling.

### Stage 4 — Event-driven pub/sub

Decouple producers from consumers. "When order ships, notify warehouse AND
update finance." Fan-out, filtering, dead-letter handling.

---

## Core Concepts

### 1. Process Definition (Design Time)

A **Process Definition** describes a business process: its trigger, steps,
conditions, and outcomes.

Properties:
- Name, display name, description
- Trigger type: `StateChange`, `Schedule`, `Manual`, `Event`
- Trigger configuration (which entity/field, cron expression, event topic, etc.)
- Steps (ordered list of activities)
- Timeout / SLA duration (optional)
- Enabled flag

### 2. Activity (Design Time)

An **Activity** is a single unit of work within a process. Activities are
composable and typed.

Activity types:
- `SendNotification` — email, webhook, or in-app notification
- `CreateRecord` — create a record in the data platform
- `UpdateField` — set a field value on a record
- `CallService` — invoke an external service via Dapr
- `Wait` — pause for a duration or until a condition is met
- `Approval` — pause until a user approves/rejects
- `Condition` — branch based on field values or expressions
- `PublishEvent` — emit an event to a pub/sub topic

### 3. Process Instance (Runtime)

A **Process Instance** is a running occurrence of a process definition, tied to
a specific trigger event (e.g., "Record X transitioned to status Y").

Properties:
- Reference to process definition
- Trigger context (record ID, entity, field, old/new value)
- Current step
- Status: `Running`, `WaitingForInput`, `Completed`, `Failed`, `Cancelled`
- Started at, completed at
- Error details (if failed)

### 4. Work Queue (Runtime)

A **Work Queue** surfaces pending human tasks (approvals, reviews, manual steps)
to users and teams. It is the primary way users interact with running processes.

Properties:
- Queue name (e.g., "Approvals", "Order Review")
- Assigned to: user, team/role, or unassigned (claimable)
- Work item: reference to process instance + current activity
- Priority (derived from SLA or explicit)
- Due date (from timer/SLA)
- Status: `Pending`, `Claimed`, `Completed`, `Escalated`

### 5. Event (Runtime)

An **Event** is a message published to a topic. Other processes or external
subscribers can react to it.

Properties:
- Topic name
- Payload (JSON)
- Source (process instance, user, external system)
- Timestamp

---

## Functional Requirements

### FR-1: Stage 1 — Transition Actions

| ID     | Requirement |
|--------|-------------|
| FR-1.1 | Define actions triggered by a state transition on a specific entity/field |
| FR-1.2 | Support multiple actions per transition (executed in order) |
| FR-1.3 | Actions receive trigger context: entity, record ID, field, old value, new value, user |
| FR-1.4 | Built-in action types: SendNotification, CreateRecord, UpdateField |
| FR-1.5 | Actions that fail do not block the original state transition (fire-and-forget by default) |
| FR-1.6 | Optional "blocking" mode: transition waits for action success |
| FR-1.7 | Action execution is logged with outcome (success/failure/skipped) |

### FR-2: Stage 2 — Durable Workflows

| ID     | Requirement |
|--------|-------------|
| FR-2.1 | Define processes as metadata (API/UI): steps, conditions, routing — no code deploy |
| FR-2.2 | Process definitions are versioned; running instances pin to an immutable snapshot of the definition they started on |
| FR-2.3 | Workflows survive application restarts (Dapr durable execution) |
| FR-2.4 | Support timers: wait for a duration, SLA deadlines with escalation |
| FR-2.5 | Support human-in-the-loop: pause workflow and create work queue item for approval/input |
| FR-2.6 | Workflow steps have configurable retry policies (count, backoff) — reuse Dapr retry |
| FR-2.7 | Query running workflow instances: list, filter by status, inspect current step |
| FR-2.8 | Cancel a running workflow instance |
| FR-2.9 | Workflow history: log of completed steps with timestamps and outcomes |
| FR-2.10 | Processes can trigger sub-processes (composition) |

### FR-2B: Work Queues

| ID      | Requirement |
|---------|-------------|
| FR-2B.1 | Human tasks (approvals, reviews, manual steps) appear in a work queue |
| FR-2B.2 | Work items are assignable to individual users or teams/roles |
| FR-2B.3 | Unassigned work items can be claimed by team members (pull model) |
| FR-2B.4 | Work items show: process name, step, related record, priority, due date |
| FR-2B.5 | Completing/rejecting a work item resumes the waiting workflow |
| FR-2B.6 | Escalation: overdue work items can auto-reassign or notify a supervisor |
| FR-2B.7 | API to list, claim, complete, and reject work items |

### FR-2C: Process Overview & Bottleneck Visibility

| ID      | Requirement |
|---------|-------------|
| FR-2C.1 | Dashboard showing all running process instances grouped by definition |
| FR-2C.2 | Per-definition: count by status (running, waiting, completed, failed) |
| FR-2C.3 | Highlight bottlenecks: steps where instances are accumulating or SLA is at risk |
| FR-2C.4 | Average and P95 duration per step (identify slow steps) |
| FR-2C.5 | Filter by date range, entity, assignee |
| FR-2C.6 | Drill down from overview to individual process instance detail |

### FR-3: Stage 3 — Service Invocation

| ID     | Requirement |
|--------|-------------|
| FR-3.1 | Invoke internal Dapr services via service invocation; invoke external HTTP APIs via Dapr bindings or resilient HTTP client |
| FR-3.2 | Configurable retry policy per service call (count, interval, backoff) |
| FR-3.3 | Circuit breaker: stop calling a failing service after threshold |
| FR-3.4 | Timeout per invocation (default + per-call override) |
| FR-3.5 | Response mapping: extract values from service response into workflow context |
| FR-3.6 | Service registry: named services with base URL, auth config, health endpoint |

### FR-4: Stage 4 — Pub/Sub

| ID     | Requirement |
|--------|-------------|
| FR-4.1 | Publish events to named topics from workflows or transition actions |
| FR-4.2 | Subscribe to topics and trigger workflows or actions on event receipt |
| FR-4.3 | Event filtering: subscribe to a subset of events on a topic (by payload fields) |
| FR-4.4 | Dead-letter handling: events that fail processing N times move to DLQ |
| FR-4.5 | At-least-once delivery guarantee |
| FR-4.6 | Event schema: all events carry source, timestamp, correlation ID, payload |

---

## Non-Functional Requirements

| ID    | Requirement |
|-------|-------------|
| NFR-1 | No runtime dependency on a specific data platform — works with XRM, EF Core models, or external APIs |
| NFR-2 | BPM depends only on Dapr APIs; the host must provide required Dapr components for enabled stages (e.g., state store for Stage 2, pub/sub component for Stage 4) |
| NFR-3 | Process definitions are metadata (JSON/API), not compiled code — versionable without deploy |
| NFR-4 | Dapr state store for durable workflow execution; a separate queryable read store (e.g., SQLite, Postgres) for instance queries, history, work queues, and metrics — host provides the implementation |
| NFR-5 | All abstractions have in-memory test doubles for unit testing without Dapr |
| NFR-6 | Structured logging with correlation IDs across workflow steps |
| NFR-7 | Failure semantics per operation: blocking actions fail the transition; fire-and-forget actions use a durable outbox for crash safety; pub/sub publish retries before failing |

---

## Integration with XRM

BPM integrates with XRM through its lifecycle hook system:

```
XRM lifecycle hook (OnFieldChanged)
  → BPM TransitionActionDispatcher
    → evaluates registered process definitions
      → starts workflow / executes actions
```

The integration is a thin adapter — not a hard dependency. A host without XRM
can trigger BPM processes through the API or event system directly.

---

## Out of Scope (for now)

- Visual workflow designer (UI)
- BPMN import/export
- Multi-tenancy
- Built-in monitoring dashboard (FR-2C provides API and read model; visualization is the host's responsibility)
- Parallel / branching step execution
- Stage 5+ (multiple services, Aspire orchestration)

---

## Design Decisions

1. **Dapr over custom infrastructure** — no reason to build a scheduler, message
   bus, or retry system when Dapr provides all of these with pluggable backends.
2. **Metadata-driven, not code-driven** — process definitions are data
   (like XRM entity definitions), interpreted at runtime by a generic Dapr
   workflow engine. This avoids the versioning problem where code changes break
   running instances, and lets users compose processes without a developer.
3. **Fire-and-forget by default** — transition actions should not slow down the
   user's save operation unless explicitly configured as blocking.
4. **Correlation by record** — every process instance ties back to a source
   record, enabling "show me all processes for this order" queries.
5. **Work queues as first-class concept** — human tasks are not an afterthought.
   Processes that need human input surface work items in queues, making open work
   visible and measurable.
6. **Loose coupling to data sources** — BPM references entities/records by name
   and ID, never by compile-time type. The host provides an `IRecordProvider`
   adapter. XRM is one possible implementation, but not the only one.
