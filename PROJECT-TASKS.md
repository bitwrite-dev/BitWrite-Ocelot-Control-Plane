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
| #280 | [Subtask] PublishSnapshot & RollbackSnapshot UseCases | #291 | ✅ Done |
| #238 | [UseCase] Snapshot Publication Flow | #292 | ✅ Done |
| #243 | [Infrastructure] Redis Repository Implementations | #293 | ✅ Done |
| #281 | [Subtask] RuntimeAdapter - Gateway Config Synchronization | #294 | ✅ Done |
| #283 | [Subtask] Management API Controllers | #295 | ✅ Done |

---

## Phase 1: Domain Events (High Priority)
> Foundation for Use Cases

### **COMPLETED** ✅

| # | Item | Status |
|---|------|--------|
| 1 | Domain Event base class/interface | ✅ Done |
| 2 | All domain events defined with payload | ⚠️ Partial (#339 - Missing License/Gateway/Route/Service/Plugin events) |
| 3 | Aggregate base class with event collection | ✅ Done |
| 4 | Application Service event dispatcher | ✅ Done |
| 5 | Outbox table/entity in Infrastructure | ✅ Done (InMemory) |
| 6 | Outbox publisher (background job) | ❌ Stub (#318) |
| 7 | Redis Pub/Sub publisher for notifications | ❌ Not implemented (#318) |
| 8 | Event serialization with versioning | ⚠️ Partial - Deserialize broken (#319) |
| 9 | Idempotency handling in consumers | ⏳ Pending |
| 10 | Unit tests for event dispatching | ✅ Done |

---

## Phase 2: Application Layer - Use Cases (High Priority)
> Core business logic, depends on Domain Events

| # | Issue | Subtask | Title | Branch | Status | Priority |
|---|-------|---------|-------|--------|--------|----------|
| 1 | #236 | #279 | [UseCase] Snapshot Creation Flow | - | ✅ Done | — |
| 2 | #238 | - | [UseCase] Snapshot Publication Flow | - | ✅ Done | — |
| 3 | #240 | #280 | [UseCase] Rollback | - | ✅ Done (via #280) | — |
| 4 | — | #308 | [Task] Gateway UseCases Implementation | — | ⏳ **Ready** | **High** |
| 5 | — | #309 | [Task] Route UseCases Implementation (11) | — | ⏳ **Ready** | **High** |
| 6 | — | #310 | [Task] Service UseCases Implementation | — | ⏳ **Ready** | **High** |
| 7 | — | #311 | [Task] GlobalConfiguration UseCases Implementation | — | ⏳ **Ready** | **High** |
| 8 | — | #312 | [Task] Snapshot Query UseCases Implementation (7) | — | ⏳ **Ready** | **High** |
| 9 | — | #313 | [Task] Publication Query UseCases Implementation (3) | — | ⏳ **Ready** | **High** |
| 10 | — | #314 | [Task] Plugin UseCases Implementation | — | ⏳ **Ready** | **High** |
| 11 | — | #315 | [Task] Runtime UseCases Implementation | — | ⏳ **Ready** | **High** |
| 12 | — | #304 | [Task] License UseCases Implementation | — | ⏳ **Ready** (dep #302-303) | **High** |
| 13 | — | #307 | [Task] Audit Query UseCases Implementation | — | ⏳ **Ready** (dep #305-306) | **High** |

---

## Phase 3: Infrastructure Layer (Medium Priority)
> Data persistence, depends on Use Cases

| # | Issue | Subtask | Title | Branch | Status | Priority |
|---|-------|---------|-------|--------|--------|----------|
| 1 | #243 | #282 | [Infrastructure] Redis Repository Implementations | - | ✅ Done | — |
| 2 | #281 | - | [Subtask] RuntimeAdapter - Gateway Config Synchronization | - | ✅ Done | — |
| 3 | — | #316 | [Task] Fix PublicationRepository Stubs | — | ⏳ **Ready** | **High** |
| 4 | — | #317 | [Task] Fix RuntimeInstanceRepository & PluginRepository Stubs | — | ⏳ **Ready** | **High** |
| 5 | — | #318 | [Task] Implement OutboxPublisher - Real Redis Pub/Sub | feature/318-outbox-publisher-redis | 🔄 **In Progress** | **Critical** |
| 6 | — | #319 | [Task] Fix JsonEventSerializer.Deserialize | — | ⏳ **Ready** | **Critical** |
| 7 | — | #320 | [Task] Implement RedisOutboxRepository | — | ⏳ **Ready** | **High** |
| 8 | — | #321 | [Task] Implement IOcelotConfigApplier | — | ⏳ **Ready** | **Critical** |
| 9 | — | #322 | [Task] Add Missing Repository Interfaces to Application Layer | — | ⏳ **Ready** | **High** |
| 10 | — | #303 | [Task] License Repository Implementation | — | ⏳ **Ready** (dep #302) | **High** |
| 11 | — | #306 | [Task] AuditLog Repository Implementation | — | ⏳ **Ready** (dep #305) | **High** |

---

## Phase 4: API & UI (Lower Priority)
> Upper layers, depends on Infrastructure

| # | Issue | Subtask | Title | Branch | Status | Priority |
|---|-------|---------|-------|--------|--------|----------|
| 1 | #283 | - | [Subtask] Management API Controllers | - | ✅ Scaffolding Done | — |
| 2 | — | #323 | [Task] DI Registration - Register All Handlers, Repositories, Domain Services | feature/323-di-registration | ✅ **Done** | **Critical** |
| 3 | — | #324 | [Task] Wire GatewaysController to UseCases | — | ⏳ **Ready** (dep #308, #323) | **High** |
| 4 | — | #325 | [Task] Wire RoutesController to UseCases | — | ⏳ **Ready** (dep #309, #323) | **High** |
| 5 | — | #326 | [Task] Wire ServicesController to UseCases | — | ⏳ **Ready** (dep #310, #323) | **High** |
| 6 | — | #327 | [Task] Wire GlobalConfigurationController to UseCases | — | ⏳ **Ready** (dep #311, #323) | **High** |
| 7 | — | #328 | [Task] Wire SnapshotsController to UseCases | — | ⏳ **Ready** (dep #312, #323) | **High** |
| 8 | — | #329 | [Task] Wire PublicationsController to UseCases | — | ⏳ **Ready** (dep #313, #323) | **High** |
| 9 | — | #330 | [Task] Wire PluginsController to UseCases | — | ⏳ **Ready** (dep #314, #322, #323) | **High** |
| 10 | — | #331 | [Task] Wire RuntimeController to UseCases | — | ⏳ **Ready** (dep #315, #321, #322, #323) | **High** |
| 11 | — | #332 | [Task] Wire LicensesController to UseCases | — | ⏳ **Ready** (dep #296, #323) | **High** |
| 12 | — | #333 | [Task] Wire AuditController to UseCases | — | ⏳ **Ready** (dep #297, #323) | **High** |
| 13 | #284 | - | [Subtask] Dashboard UI - 12 Pages | — | ⏳ **Next** | Medium |
| 14 | #285 | - | [Subtask] Testing Implementation - 5 Test Layers | — | ⏳ **Ready** | High |

---

## Phase 5: Testing Implementation (High Priority)
> Quality assurance, can start in parallel

| # | Issue | Subtask | Title | Branch | Status | Priority |
|---|-------|---------|-------|--------|--------|----------|
| 1 | #227 | #263 | [Epic] Testing Strategy & CI/CD | — | 📋 Open | **High** |
| 2 | — | #334 | [Task] Application Layer Tests - UseCase Handlers | — | ⏳ **Ready** | **High** |
| 3 | — | #335 | [Task] Infrastructure Layer Tests | — | ⏳ **Ready** | **High** |
| 4 | — | #336 | [Task] API/Controller Tests | — | ⏳ **Ready** | **High** |
| 5 | — | #337 | [Task] Architecture Tests (NetArchTest) | — | ⏳ **Ready** | Medium |
| 6 | — | #338 | [Task] CI/CD Pipeline - GitHub Actions | — | ⏳ **Ready** | **High** |
| 7 | #285 | - | [Subtask] Testing Implementation - 5 Test Layers | — | ⏳ **Ready** | **High** |
| 8 | #263 | - | [Task] Testing Implementation - All Layers | — | ⏳ **Ready** | **High** |

---

## Phase 6: Missing Bounded Contexts (Parallelizable)

### Licensing Context (Epic #296)
| # | Issue | Title | Branch | Status | Priority |
|---|-------|-------|--------|--------|----------|
| 1 | #296 | [Epic] Licensing Context - License Aggregate & Management | — | 📋 Open | **High** |
| 2 | #302 | [Task] License Aggregate Implementation | — | ⏳ **Ready** | **High** |
| 3 | #303 | [Task] License Repository Implementation | — | ⏳ **Ready** | **High** |
| 4 | #304 | [Task] License UseCases Implementation | — | ⏳ **Ready** | **High** |
| 5 | #332 | [Task] Wire LicensesController to UseCases | — | ⏳ **Ready** (dep #296) | **High** |

### Audit Context (Epic #297)
| # | Issue | Title | Branch | Status | Priority |
|---|-------|-------|--------|--------|----------|
| 1 | #297 | [Epic] Audit Context - AuditLog Aggregate & Query | — | 📋 Open | **High** |
| 2 | #305 | [Task] AuditLog Entity Implementation | — | ⏳ **Ready** | **High** |
| 3 | #306 | [Task] AuditLog Repository Implementation | — | ⏳ **Ready** | **High** |
| 4 | #307 | [Task] Audit Query UseCases Implementation | — | ⏳ **Ready** | **High** |
| 5 | #333 | [Task] Wire AuditController to UseCases | — | ⏳ **Ready** (dep #297) | **High** |

### Plugin Platform Context (Epic #217)
| # | Issue | Title | Branch | Status | Priority |
|---|-------|-------|--------|--------|----------|
| 1 | #217 | [Epic] Plugin Platform Context | — | 📋 Open | Medium (v1.1) |

---

## Phase 7: Domain Completion

| # | Issue | Title | Branch | Status | Priority |
|---|-------|-------|--------|--------|----------|
| 1 | #339 | [Task] Add Missing Domain Events | — | ⏳ **Ready** | **High** |

---

## Phase 8: SDK (Future - v1.1)

| # | Issue | Title | Branch | Status | Priority |
|---|-------|-------|--------|--------|----------|
| 1 | #340 | [Task] Implement SDK Project | — | ⏳ **Ready** | Low |

---

## Notes

### Dependency Chain
```
Value Objects → Domain Services → Aggregates → Domain Events → Use Cases → Infrastructure → API/UI
                    ↑                                                                  │
                    └────────────────────────── (DI Registration #323) ─────────────────┘
```

### Architecture Layers
1. **Domain Layer**: Value Objects, Domain Services, Aggregates, Domain Events
2. **Application Layer**: Use Cases (Orchestration)
3. **Infrastructure Layer**: Redis Repositories, External Integrations, Outbox, Adapters
4. **Presentation Layer**: Management API, Dashboard UI

### Branch Strategy
- Branch name: `feature/{issue-number}-{short-description}`
- Example: `feature/323-di-registration`
- One branch per subtask
- PR to `develop` branch

### Prioritized Implementation Order (Critical Path)

#### 🔴 Phase 1: Critical Infrastructure (Week 1-2)
| Order | Issue | Title | Unlocks |
|---|---|---|---|
| 1 | **#323** | DI Registration | All controller wiring |
| 2 | **#322** | Missing Repo Interfaces | Plugin/Runtime/License/Audit UseCases |
| 3 | **#318** | OutboxPublisher (Real Redis Pub/Sub) | Event-driven foundation |
| 4 | **#319** | JsonEventSerializer.Deserialize | OutboxPublisher |
| 5 | **#320** | RedisOutboxRepository | Durable outbox |
| 6 | **#321** | IOcelotConfigApplier | Runtime reconciliation |
| 7 | **#339** | Missing Domain Events | All new UseCases |

#### 🟠 Phase 2: Core UseCases (Week 2-4)
| Order | Issue | Title | Endpoints |
|---|---|---|---|
| 8 | **#308** | Gateway UseCases | 5 |
| 9 | **#309** | Route UseCases | 11 |
| 10 | **#310** | Service UseCases | 6 |
| 11 | **#311** | GlobalConfiguration UseCases | 2 |
| 12 | **#312** | Snapshot Query UseCases | 7 |
| 13 | **#313** | Publication Query UseCases | 3 |
| 14 | **#314** | Plugin UseCases | 6 |
| 15 | **#315** | Runtime UseCases | 4 |

#### 🟡 Phase 3: Bounded Contexts (Week 3-4, Parallel)
| Order | Issue | Title |
|---|---|---|
| 16 | **#302** | License Aggregate |
| 17 | **#303** | License Repository |
| 18 | **#304** | License UseCases |
| 19 | **#305** | AuditLog Entity |
| 20 | **#306** | AuditLog Repository |
| 21 | **#307** | Audit Query UseCases |

#### 🟢 Phase 4: Infrastructure Fixes (Week 3)
| Order | Issue | Title |
|---|---|---|
| 22 | **#316** | Fix PublicationRepository Stubs |
| 23 | **#317** | Fix RuntimeInstance/Plugin Repository Stubs |

#### 🔵 Phase 5: Controller Wiring (Week 4-6, After Phase 1-2)
| Order | Issue | Title | Endpoints |
|---|---|---|---|
| 24 | **#324** | Wire GatewaysController | 5 |
| 25 | **#325** | Wire RoutesController | 11 |
| 26 | **#326** | Wire ServicesController | 6 |
| 27 | **#327** | Wire GlobalConfigurationController | 2 |
| 28 | **#328** | Wire SnapshotsController | 7 |
| 29 | **#329** | Wire PublicationsController | 3 |
| 30 | **#330** | Wire PluginsController | 6 |
| 31 | **#331** | Wire RuntimeController | 4 |
| 32 | **#332** | Wire LicensesController | 5 |
| 33 | **#333** | Wire AuditController | 1 |

#### 🟣 Phase 6: Testing & Quality (Week 4-6, Parallel)
| Order | Issue | Title |
|---|---|---|
| 34 | **#334** | Application Layer Tests |
| 35 | **#335** | Infrastructure Layer Tests |
| 36 | **#336** | API/Controller Tests |
| 37 | **#337** | Architecture Tests |
| 38 | **#338** | CI/CD Pipeline |

#### ⚪ Phase 7: Future (v1.1)
| Order | Issue | Title |
|---|---|---|
| 39 | **#340** | Implement SDK Project |

---

## Open Issues Summary (54 Total)

| Status | Count | Issues |
|---|---|---|
| 📋 Epic (Open) | 6 | #296, #297, #298, #300, #301, #217 |
| ⏳ Task/Subtask (Ready) | 39 | #302-315, #316-323, #324-339, #262-263, #284-285 |
| ⏳ Task/Subtask (Dep on Epic) | 9 | #332 (dep #296), #333 (dep #297), plus 7 controller wiring deps |

---

*Last Updated: 2026-09-13*