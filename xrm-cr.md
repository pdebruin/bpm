# XRM Change Requests

BPM Stage 1 integration may require changes to XRM. Document them here.

| # | Feature | Impact | Blocking? | Status |
|---|---------|--------|-----------|--------|
| CR-001 | Expose field-level old/new values in lifecycle hook context | The `OnUpdatedAsync` hook already provides `oldDataJson` and the full record — sufficient for BPM to detect field changes. | No | ✅ Already supported |

## Assessment

**No XRM changes needed for Stage 1.** The existing `IRecordLifecycleHandler.OnUpdatedAsync(Record record, string oldDataJson, EntityDefinition entity)` provides everything BPM needs:

- Entity name via `entity.Name`
- Record ID via `record.Id`
- Old field values via `oldDataJson`
- New field values via `record.DataJson`

The host (casemgmt) will implement a lifecycle handler that:
1. Parses old/new JSON to detect changed fields
2. Builds a `TransitionContext` for each changed field
3. Calls `TransitionActionDispatcher.DispatchAsync(context)`
