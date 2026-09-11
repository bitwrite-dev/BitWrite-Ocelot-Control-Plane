# BitWrite Ocelot Control Plane - Task Prioritization

## Overview
This document provides a prioritized list of tasks and subtasks based on DDD architecture dependencies.

---

## Completed Tasks

| Issue | Title | PR | Status |
|-------|-------|----|----|
| #244 | [ValueObjects] Identity & Configuration Value Objects | #286 | ✅ Done |
| #225 | [Epic] Shared Kernel - Value Objects & Domain Primitives | #286 | ✅ Done |
| #265 | [Subtask] Define Domain Services | #287 | ✅ Done |
| #242 | [DomainService] Configuration Builder | #287 | ✅ Done |
| #246 | [DomainService] RouteConflictDetector | #287 | ✅ Done |
| #247 | [DomainService] ConfigurationConsistencyValidator | #287 | ✅ Done |
| #248 | [DomainService] OcelotCapabilityResolver | #287 | ✅ Done |
| #249 | [DomainService] SnapshotIntegrityVerifier | #287 | ✅ Done |
| #264 | [Subtask] Define Aggregate Roots & Boundaries | #288 | ✅ Done |
| #228 | [Aggregate] Gateway | #288 | ✅ Done |
| #229 | [Aggregate] Route | #288 | ✅ Done |
| #230 | [Aggregate] Service | #288 | ✅ Done |
| #231 | [Aggregate] GlobalConfiguration | #288 | ✅ Done |
| #232 | [Aggregate] Snapshot | #288 | ✅ Done |
| #233 | [Aggregate] Publication | #288 | ✅ Done |
| #234 | [Aggregate] Plugin | #288 | ✅ Done |
| #235 | [Aggregate] RuntimeInstance | #288 | ✅ Done |
| #267 | [Subtask] Define Domain Events & Integration Events | #289 | ✅ Done |
| #241 | [Domain] Domain Events & Integration Events | #289 | ✅ Done |
| #279 | [Subtask] CreateSnapshot UseCase - Detailed Implementation | #290 | ✅ Done |
| #236 | [UseCase] Snapshot Creation Flow | #290 | ✅ Done |

---

## Phase 1: Domain Events (High Priority)
> Foundation for Use Cases

### **COMPLETED** ✅

| # | Item | Status |
|---|------|--------|
| 1 | Domain Event base class/interface | ✅ Done |
| 2 | All domain events defined with payload | ✅ Done |
| 3 | Aggregate base class with event collection | ✅ Done |
| 4 | Application Service event dispatcher | ✅ Done |
| 5 | Outbox table/entity in Infrastructure | ✅ Done (InMemory) |
| 6 | Outbox publisher (background job) | ✅ Done |
| 6 | Redis Pub/Sub publisher for notifications | ✅ Done (structure) |
| 7 | Event serialization with versioning | ✅ Done |
| 8 | Idempotency handling in consumers | ⏳ Pending |
| 9 | Unit tests for event dispatching | ✅ Done |

---

## Phase 2: Application Layer - Use Cases (High Priority)
> Core business logic, depends on Domain Events

| # | Issue | Subtask | Title | Branch | Status |
|---|-------|---------|-------|--------|--------|
| 1 | #236 | #279 | [UseCase] Snapshot Creation Flow | - | ✅ Done |
| 2 | #238 | #280 | [UseCase] Snapshot Publication Flow | - | ⏳ Ready |
| 3 | #240 | #280 | [UseCase] Rollback | - | ⏳ Ready |

---

## Phase 3: Infrastructure Layer (Medium Priority)
> Data persistence, depends on Use Cases

| # | Issue | Subtask | Title | Branch | Status |
|---|-------|---------|-------|--------|--------|
| 1 | #243 | #282 | [Infrastructure] Redis Repository Implementations | - | ⏳ Ready |
| 2 | #281 | - | [Subtask] RuntimeAdapter - Gateway Config Synchronization | - | ⏳ Ready |

---

## Phase 4: API & UI (Lower Priority)
> Upper layers, depends on Infrastructure

| # | Issue | Subtask | Title | Branch | Status |
|---|-------|---------|-------|--------|--------|
| 1 | #283 | - | [Subtask] Management API Controllers | - | ⏳ Ready |
| 2 | #284 | - | [Subtask] Dashboard UI | - | ⏳ Ready |
| 3 | #285 | - | [Subtask] Testing Implementation | - | ⏳ Ready |

---

## Phase 5: Runtime Operations (Lower Priority)
> Gateway synchronization, depends on Use Cases

| # | Issue | Subtask | Title | Branch | Status |
|---|-------|---------|-------|--------|--------|
| 1 | #239 | #281 | [UseCase] Runtime Synchronization | - | ⏳ Ready |

---

## Notes

### Dependency Chain
```
Value Objects → Domain Services → Aggregates → Domain Events → Use Cases → Infrastructure → API/UI
```

### Architecture Layers
1. **Domain Layer**: Value Objects, Domain Services, Aggregates, Domain Events
2. **Application Layer**: Use Cases (Orchestration)
3. **Infrastructure Layer**: Redis Repositories, External Integrations
4. **Presentation Layer**: Management API, Dashboard UI

### Branch Strategy
- Branch name: `feature/{issue-number}-{short-description}`
- Example: `feature/267-domain-events-integration`
- One branch per subtask
- PR to `develop` branch

### Next Steps (Current Priority)
1. **Phase 2**: Implement **#280 PublishSnapshot & RollbackSnapshot UseCases** (parent #257)
2. **Phase 2**: Implement #238 Snapshot Publication Flow
3. **Phase 2**: Implement #240 Rollback
4. **Phase 3**: Implement Infrastructure (#243, #282, #281)
5. **Phase 4**: Implement API & UI (#283, #284, #285)
6. **Phase 5**: Implement Runtime Operations (#239, #281)

---

*Last Updated: $(date)*