# ASSIGNMENT TRACEABILITY MATRIX
## DeliveryTracker — Baseline Inspection Report

*Prepared: 2026-08-23 | Baseline Commit: 2a22e54*

---

## 1. BASELINE BUILD & TEST RESULTS

| Check | Result | Notes |
| :--- | :--- | :--- |
| `dotnet build` (API) | ✅ Compiles clean | Build "failed" only because running server locks the output `.exe` — no actual compile errors |
| `dotnet build` (Tests) | ✅ Compiles clean | Same locked-exe issue; DLL output is valid |
| `dotnet test` (55 tests) | ✅ **55/55 PASSED** | Failed: 0, Passed: 55, Skipped: 0 |
| `npm run build` (Frontend) | ✅ **Exit 0** | TypeScript compiled, Vite bundled 261 kB JS, 3.27 kB CSS |

### Test Suite Breakdown
| Test Class | Tests | Coverage |
| :--- | :--- | :--- |
| `PricingServiceTests` | 5 | B2C/B2B x IntraZone/InterZone x Prepaid/COD, volumetric weight dominance |
| `AgentAssignmentServiceTests` | ~10 | Same-zone preference, Haversine ranking, excludeAgentId, no-agents error |
| `OrderStatusServiceTests` | ~15 | Valid/invalid transitions, agent ownership, Failed -> DeliveryAttempt, admin allowed |
| `DeliveryRecoveryServiceTests` | ~15 | Full reschedule flow, agent release, notification creation, unauthorized |
| `AuthServiceTests` | ~8 | Register, login, duplicate email, wrong password |
| `OrderServiceTests` | ~2 | Order creation, GetOrders scoping |
| `UnitTest1.cs` | 0 | Placeholder — no tests |

---

## 2. EXISTING IMPLEMENTATION INVENTORY

### Backend — Fully Implemented

| Component | File(s) | Status |
| :--- | :--- | :--- |
| User registration (Customer only) | `AuthController`, `AuthService` | COMPLETE |
| JWT login (all roles) | `AuthController`, `AuthService` | COMPLETE |
| JWT claims: sub, email, role | `AuthService.GenerateJwtToken` | COMPLETE |
| Password hashing (PasswordHasher) | `AuthService` | COMPLETE |
| Pricing engine (/api/orders/calculate-price) | `PricingController`, `PricingService` | COMPLETE |
| Volumetric weight: L*W*H/5000 | `PricingService` | COMPLETE |
| Chargeable weight: max(actual, volumetric) | `PricingService` | COMPLETE |
| IntraZone vs InterZone rate lookup | `PricingService`, `RateCard` entity | COMPLETE |
| COD surcharge (flat amount) | `PricingService`, `RateCard` entity | COMPLETE |
| B2C / B2B pricing tiers | `RateCard` entity, seed data | COMPLETE |
| Order creation | `OrdersController`, `OrderService` | COMPLETE |
| Unique tracking number (LM-YYYYMMDD-XXXXXX) | `OrderService` | COMPLETE |
| Initial OrderStatusHistory on create | `OrderService` | COMPLETE |
| Order list (scoped: customer/agent/admin) | `OrdersController`, `OrderService` | COMPLETE |
| Order detail (privacy check) | `OrdersController`, `OrderService` | COMPLETE |
| State machine transitions (7 valid) | `OrderStatusService` | COMPLETE |
| Immutable append-only OrderStatusHistory | `OrderStatusService` | COMPLETE |
| Agent assignment via Haversine | `AgentAssignmentService` | COMPLETE |
| Same-zone preference in assignment | `AgentAssignmentService` | COMPLETE |
| Agent availability flag mutation | `AgentAssignmentService` | COMPLETE |
| Failed delivery: DeliveryAttempt record | `OrderStatusService` | COMPLETE |
| Failed delivery: customer Notification | `OrderStatusService` | COMPLETE |
| Reschedule: agent release | `DeliveryRecoveryService` | COMPLETE |
| Reschedule: exclude previous agent | `DeliveryRecoveryService` | COMPLETE |
| Reschedule: auto-reassign | `DeliveryRecoveryService` | COMPLETE |
| Reschedule: customer ownership validation | `DeliveryRecoveryService` | COMPLETE |
| Reschedule: future-date validation | `DeliveryRecoveryService` | COMPLETE |
| EF Core SQLite migrations | `Migrations/`, `DbInitializer.cs` | COMPLETE |
| Seed data (3 zones, 6 areas, 2 rate cards, 4 users, 2 agents) | `DbInitializer.cs` | COMPLETE |
| CORS policy for localhost:5173 | `Program.cs` | COMPLETE |
| Swagger / OpenAPI at /swagger | `Program.cs` | COMPLETE |
| Agent authorization: only assigned orders | `OrderStatusService` | COMPLETE |

### Frontend — Fully Implemented

| Component | File(s) | Status |
| :--- | :--- | :--- |
| Login page | `LoginPage.tsx` | COMPLETE |
| Registration page | `RegisterPage.tsx` | COMPLETE |
| JWT token storage + AuthContext | `AuthContext.tsx` | COMPLETE |
| Role-based navigation / tab routing | `App.tsx`, `Layout.tsx` | COMPLETE |
| Customer: 4-step booking form | `CustomerCreateOrderPage.tsx` | COMPLETE |
| Customer: calculate-price call + breakdown card | `CustomerCreateOrderPage.tsx` | COMPLETE |
| Customer: order confirmation -> redirect to detail | `CustomerCreateOrderPage.tsx` | COMPLETE |
| Customer/Agent/Admin: orders list with search+filter | `OrdersListPage.tsx` | COMPLETE |
| Order detail page: tracking, route, package, pricing | `OrderDetailPage.tsx` | COMPLETE |
| Vertical tracking timeline (immutable audit) | `TrackingTimeline.tsx` | COMPLETE |
| Reschedule modal (customer, Failed orders only) | `RescheduleModal.tsx` | COMPLETE |
| Status update modal (agent/admin) | `AgentStatusModal.tsx` | COMPLETE |
| Admin: operations console with KPI cards | `AdminOperationsPage.tsx` | COMPLETE |
| Admin: auto-assign button per order row | `AdminOperationsPage.tsx` | COMPLETE |
| StatusBadge component | `StatusBadge.tsx` | COMPLETE |
| PriceBreakdownCard component | `PriceBreakdownCard.tsx` | COMPLETE |

---

## 3. GAPS AND PARTIAL IMPLEMENTATIONS

### 3.1 Critical Gaps

| ID | Area | Gap Description | Severity |
| :--- | :--- | :--- | :--- |
| GAP-001 | Backend Order entity | Order has no RescheduledDate field. Reschedule date is stored only on DeliveryAttempt.RescheduledDate, not on the Order itself. GET /api/orders/{id} cannot return when an order was rescheduled for. | HIGH |
| GAP-002 | Notification entity | Notification entity uses only RecipientEmail + Message. Missing: UserId FK, Title, IsRead flag. README ER diagram documents these fields but they don't exist in the entity. | MEDIUM |
| GAP-003 | Frontend notifications | Backend creates Notification records on failure + reschedule, but there is no frontend page or component to display them to the customer. | MEDIUM |
| GAP-004 | Frontend Rescheduled state | canAgentUpdateStatus excludes Failed correctly, but Rescheduled orders with a reassigned agent need manual verification that the agent can see and advance them. | LOW |
| GAP-005 | Area entity coordinates | Area entity has no Latitude/Longitude fields. Haversine uses hardcoded zone centroids instead of per-area coordinates. ARCHITECTURE.md shows Areas with coordinates but they don't exist. | LOW / BY DESIGN |

### 3.2 Documentation Gaps

| ID | Area | Gap Description |
| :--- | :--- | :--- |
| DOC-001 | ARCHITECTURE.md RateCard | Shows stale fields (IsIntraZone, BaseRatePerKg, MinFee, CodSurchargePercent) — does not match actual entity (IntraZoneRatePerKg, InterZoneRatePerKg, CODSurcharge). |
| DOC-002 | ARCHITECTURE.md Agents | Shows Name, Phone, CurrentAreaId fields that do not exist on the actual Agent entity. |
| DOC-003 | ARCHITECTURE.md DeliveryAttempts | Shows IsSuccessful boolean field that does not exist in the actual DeliveryAttempt entity. |
| DOC-004 | ARCHITECTURE.md Orders | Shows RescheduledDate on Order — this field does not exist (see GAP-001). |

### 3.3 Test Coverage Gaps

| ID | Area | Gap Description |
| :--- | :--- | :--- |
| TEST-001 | UnitTest1.cs | Empty placeholder file — 0 tests. Not harmful but unprofessional. |
| TEST-002 | OrderServiceTests | Only ~2 tests. CreateOrderAsync and GetOrdersAsync are minimally covered. |
| TEST-003 | RescheduledDate coverage | No test can assert Order.RescheduledDate from GET response because the field doesn't exist yet. |

---

## 4. PER-REQUIREMENT TRACEABILITY

### REQ-01 — User Registration
- **Requirement**: Customer self-registers with name/email/password. Returns JWT.
- **Implementation**: POST /api/auth/register -> AuthService.RegisterAsync. Role forced to Customer.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None (AuthServiceTests covers)
- **Manual verify**: POST /api/auth/register -> 200 with token + role=Customer

---

### REQ-02 — User Login
- **Requirement**: All roles can login with email+password and receive JWT.
- **Implementation**: POST /api/auth/login -> AuthService.LoginAsync.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None (AuthServiceTests covers)
- **Manual verify**: Login with all 4 seeded demo accounts

---

### REQ-03 — Price Calculation
- **Requirement**: System calculates price from dimensions, weight, zone, order type, payment type. Database-driven rate cards.
- **Implementation**: POST /api/orders/calculate-price -> PricingService.CalculatePriceAsync. Reads RateCards table.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None (PricingServiceTests: 5 scenarios)
- **Manual verify**: Colaba->Andheri, B2C, COD, 4 kg = 240+40 = 280

---

### REQ-04 — Order Creation
- **Requirement**: Customer creates order. Backend recalculates price. Generates tracking number. Records Created status.
- **Implementation**: POST /api/orders -> OrderService.CreateOrderAsync. Calls PricingService. Tracking = LM-YYYYMMDD-XXXXXX.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None
- **Manual verify**: Create order, verify tracking format, verify Created in response

---

### REQ-05 — Order Listing (Scoped)
- **Requirement**: Customer sees own orders. Agent sees assigned orders. Admin sees all.
- **Implementation**: GET /api/orders -> OrdersController.GetOrders. JWT claims used for scoping.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None
- **Manual verify**: Login as agent1, GET /api/orders -> only their assigned order

---

### REQ-06 — Order Detail (Privacy)
- **Requirement**: Customers only view own orders. Agents only view assigned orders.
- **Implementation**: GET /api/orders/{id} with ownership check. Returns 403 for unauthorized.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None
- **Manual verify**: Login as customer, GET another customer's order -> 403

---

### REQ-07 — Agent Auto-Assignment
- **Requirement**: Admin triggers assignment. Algorithm: IsAvailable filter, same-zone preference, Haversine distance tie-break.
- **Implementation**: POST /api/orders/{id}/auto-assign -> AgentAssignmentService.AutoAssignAgentAsync.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None (AgentAssignmentServiceTests covers)
- **Manual verify**: Auto-assign Zone A order -> Raj Agent (Zone A) selected

---

### REQ-08 — Delivery Status Progression
- **Requirement**: Agent/Admin advances: Created->PickedUp->InTransit->OutForDelivery->Delivered. Each appends immutable history.
- **Implementation**: PATCH /api/orders/{id}/status -> OrderStatusService. State machine via switch expression.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None (OrderStatusServiceTests covers)
- **Manual verify**: Login as agent, step through all statuses, verify all history events

---

### REQ-09 — Failed Delivery Attempt
- **Requirement**: Agent marks OutForDelivery->Failed with notes. System records DeliveryAttempt + Notification + OrderStatusHistory.
- **Implementation**: PATCH /api/orders/{id}/status status=Failed -> OrderStatusService records DeliveryAttempt + Notification in transaction.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None
- **Manual verify**: Mark order Failed, verify DeliveryAttempts and Notifications in SQLite

---

### REQ-10 — Customer Reschedule
- **Requirement**: Customer reschedules Failed order with future date. System releases agent, excludes them, auto-assigns new agent, sets Rescheduled status.
- **Implementation**: POST /api/orders/{id}/reschedule -> DeliveryRecoveryService.RescheduleOrderAsync.
- **Status**: PARTIAL — GAP-001: RescheduledDate not stored on Order entity
- **Backend changes needed**: Add RescheduledDate to Order entity + migration; set it in DeliveryRecoveryService; expose in OrderResponse DTO
- **Frontend changes needed**: Display RescheduledDate on OrderDetailPage if present
- **Tests needed**: Test that Order.RescheduledDate is set after reschedule and returned in GET response
- **Manual verify**: Reschedule -> new agent assigned, old agent IsAvailable=true, RescheduledDate visible

---

### REQ-11 — Reassigned Agent Completes Delivery
- **Requirement**: Reassigned agent advances Rescheduled->OutForDelivery->Delivered. Full 8-event audit trail.
- **Implementation**: State machine supports (Rescheduled, OutForDelivery) = true. Agent 2 can advance.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None
- **Manual verify**: Login as Agent 2 after reassignment, advance to Delivered, count 8 history events

---

### REQ-12 — Immutable Audit Trail
- **Requirement**: Every status transition permanently recorded with actor ID, actor role, notes, timestamp.
- **Implementation**: OrderStatusHistory is append-only. Displayed in TrackingTimeline component.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None
- **Manual verify**: After full lifecycle, verify 8 rows in OrderStatusHistories for the order

---

### REQ-13 — Role-Based Authorization
- **Requirement**: Customer cannot call agent/admin endpoints. Agent cannot assign agents. Admin can do everything.
- **Implementation**: [Authorize(Roles = "...")] on all controllers. JWT role claim validated.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None
- **Manual verify**: Login as Customer, POST to /api/orders/{id}/auto-assign -> 403

---

### REQ-14 — Unit Tests
- **Requirement**: Automated unit tests covering major business logic.
- **Implementation**: 55 xUnit tests across 5 service classes using in-memory SQLite.
- **Status**: COMPLETE (minor cleanup: delete UnitTest1.cs placeholder)
- **Backend changes needed**: Delete UnitTest1.cs; optionally add OrderService tests
- **Frontend changes needed**: None
- **Tests needed**: N/A
- **Manual verify**: dotnet test -> 55 passed

---

### REQ-15 — Frontend Operations Interface
- **Requirement**: React TypeScript frontend for all three roles.
- **Implementation**: Vite + React 19 + TypeScript. 6 pages, role-based tab routing.
- **Status**: COMPLETE
- **Backend changes needed**: None
- **Frontend changes needed**: None
- **Tests needed**: None
- **Manual verify**: Login as each role, confirm correct pages and actions available

---

## 5. GAPS PRIORITY SUMMARY

| Priority | ID | Description | Files Affected |
| :--- | :--- | :--- | :--- |
| P1 — High | GAP-001 | Order entity missing RescheduledDate field | Order.cs, migration, DeliveryRecoveryService.cs, OrderResponse.cs, OrderDetailPage.tsx |
| P2 — Medium | GAP-002 | Notification missing UserId, Title, IsRead | Notification.cs, migration, AppDbContext.cs, OrderStatusService.cs, DeliveryRecoveryService.cs |
| P2 — Medium | GAP-003 | No frontend notifications view | New NotificationsPage.tsx or bell panel |
| P3 — Low | DOC-001-004 | ARCHITECTURE.md fields don't match actual entities | docs/ARCHITECTURE.md |
| P3 — Low | TEST-001 | UnitTest1.cs empty placeholder | DeliveryTracker.Tests/UnitTest1.cs |
| P3 — Low | TEST-002 | OrderServiceTests minimal coverage | DeliveryTracker.Tests/OrderServiceTests.cs |

---

## 6. PROPOSED PHASE ORDER

### Phase A — High-Priority Data Fix (GAP-001)
Add RescheduledDate to Order entity. Generate EF migration. Set it in DeliveryRecoveryService. Expose in OrderResponse DTO. Display in frontend.

Files:
- DeliveryTracker.API/Entities/Order.cs — add DateTime? RescheduledDate
- DeliveryTracker.API/Migrations/ — new migration
- DeliveryTracker.API/Services/DeliveryRecoveryService.cs — set order.RescheduledDate
- DeliveryTracker.API/DTOs/OrderResponse.cs — add RescheduledDate field
- DeliveryTracker.Web/src/types/index.ts — add rescheduledDate to Order type
- DeliveryTracker.Web/src/pages/OrderDetailPage.tsx — display RescheduledDate

### Phase B — Medium-Priority (GAP-002 + GAP-003)
Enrich Notification entity. Add notifications endpoint. Add customer-facing notification panel.

Files:
- DeliveryTracker.API/Entities/Notification.cs — add UserId, Title, IsRead
- DeliveryTracker.API/Migrations/ — new migration
- DeliveryTracker.API/Controllers/ — new NotificationsController
- DeliveryTracker.API/Services/ — update notification creation in OrderStatusService and DeliveryRecoveryService
- DeliveryTracker.Web/src/ — new NotificationsPage.tsx or notification badge component

### Phase C — Cleanup (TEST + DOC)
- Delete or fill UnitTest1.cs
- Add 3-4 additional OrderServiceTests
- Update ARCHITECTURE.md to match actual entity fields

---

## 7. FILES LIKELY TO CHANGE

| Phase | File | Change Type |
| :--- | :--- | :--- |
| A | DeliveryTracker.API/Entities/Order.cs | Add field |
| A | DeliveryTracker.API/Migrations/ | New migration file |
| A | DeliveryTracker.API/Services/DeliveryRecoveryService.cs | Set new field |
| A | DeliveryTracker.API/DTOs/OrderResponse.cs | Add field to DTO |
| A | DeliveryTracker.Web/src/types/index.ts | Add to Order type |
| A | DeliveryTracker.Web/src/pages/OrderDetailPage.tsx | Display field |
| B | DeliveryTracker.API/Entities/Notification.cs | Add 3 fields |
| B | DeliveryTracker.API/Migrations/ | New migration |
| B | DeliveryTracker.API/Services/OrderStatusService.cs | Set UserId, Title |
| B | DeliveryTracker.API/Services/DeliveryRecoveryService.cs | Set UserId, Title |
| B | DeliveryTracker.API/Controllers/ | New NotificationsController |
| B | DeliveryTracker.Web/src/ | New NotificationsPage or component |
| C | DeliveryTracker.Tests/UnitTest1.cs | Delete or fill |
| C | DeliveryTracker.Tests/OrderServiceTests.cs | Add tests |
| C | docs/ARCHITECTURE.md | Documentation corrections |

---

## 8. DO NOT TOUCH

The following are complete and correct. Do not modify them without a separate plan:

- All 6 service classes (PricingService, AgentAssignmentService, OrderStatusService, DeliveryRecoveryService, AuthService, OrderService)
- All 4 controllers (AuthController, OrdersController, PricingController, ZonesController)
- All state machine transitions in OrderStatusService.IsValidTransition
- All 55 unit tests (all pass)
- Program.cs CORS, JWT, and middleware pipeline
- DbInitializer.cs seed data
- AppDbContext.cs relationships and enum conversions
- All frontend pages except OrderDetailPage.tsx (minor addition only)
- The entire Vite build pipeline

---

*STOP. No application code was modified by this document. This is a read-only baseline inspection report.*
