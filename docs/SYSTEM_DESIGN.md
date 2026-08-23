# DeliveryTracker — System Design Document

## 1. Architectural Overview
DeliveryTracker is a production-grade last-mile delivery management platform engineered with a decoupled client-server architecture:
- **Presentation Layer**: A single-page application built with React, TypeScript, and modern CSS custom properties, providing specialized operational consoles for Customers, Delivery Agents, and System Administrators.
- **Application & API Layer**: An ASP.NET Core RESTful Web API enforcing strict role-based access control (RBAC), volumetric pricing, state machine progression, and intelligent dispatching.
- **Persistence Layer**: Entity Framework Core configured with SQLite, featuring database-level foreign key enforcement and an append-only audit trail.
- **Communication Subsystem**: An asynchronous, fault-isolated multi-channel notification engine (In-App, Email, SMS) with safe simulation fallbacks for development.

```text
[React Client] <──(JWT Bearer)──> [ASP.NET Core API] <──(EF Core)──> [SQLite DB]
                                          │
                                          ├──> [In-App Notifications]
                                          ├──> [SMTP Email Engine]
                                          └──> [Twilio SMS Engine]
```

## 2. Dynamic Volumetric Pricing Engine
Delivery fees are dynamically computed at quote generation and persisted immutably on order creation.
1. **Volumetric Weight Calculation**:
   $$\text{Volumetric Weight (kg)} = \frac{\text{Length (cm)} \times \text{Width (cm)} \times \text{Height (cm)}}{5000}$$
2. **Chargeable Weight**:
   $$\text{Chargeable Weight} = \max(\text{Actual Weight}, \text{Volumetric Weight})$$
3. **Database-Driven Rate Resolution**:
   - Zone classification is evaluated by comparing pickup and drop area zones (`IntraZone` vs `InterZone`).
   - The applicable `RateCard` is resolved by `OrderType` (`B2C` vs `B2B`).
4. **Final Fee Calculation**:
   $$\text{Total Amount} = (\text{Chargeable Weight} \times \text{Rate Per Kg}) + \text{COD Surcharge (if applicable)}$$

## 3. Intelligent Agent Assignment Algorithm
Order dispatching balances geographical proximity and stationing zones without hardcoded rules:
1. **Candidate Filtering**: Queries agents where `IsAvailable == true`, optionally excluding previously failed agents.
2. **Same-Zone Priority**: Evaluates candidate agent's assigned zone against the pickup area's zone (`agent.ZoneId == pickupArea.ZoneId`). Same-zone agents receive top evaluation priority.
3. **Haversine Distance Tie-Breaking**: For candidates in equal zone brackets, great-circle distance is calculated using the Haversine formula:
   $$d = 2R \arcsin\left(\sqrt{\sin^2\left(\frac{\Delta \phi}{2}\right) + \cos(\phi_1)\cos(\phi_2)\sin^2\left(\frac{\Delta \lambda}{2}\right)}\right)$$
4. **Atomic Reservation**: The highest-ranked agent is assigned, their status set to `IsAvailable = false`, and an audit event appended in a single database transaction.

## 4. State Machine & Failure Recovery Architecture
Order progression is strictly managed via a linear finite state machine:
$$\text{Created} \longrightarrow \text{PickedUp} \longrightarrow \text{InTransit} \longrightarrow \text{OutForDelivery} \longrightarrow \{\text{Delivered} \mid \text{Failed}\}$$

- **Agent Authority**: Delivery agents can only advance orders assigned to their user ID through valid sequential transitions.
- **Privileged Admin Override**: Administrators possess an isolated, audited override capability to transition an order to any state provided a mandatory administrative reason is logged.
- **Transactional Failure Recovery**:
  1. When an agent marks an attempt `Failed`, the system records a `DeliveryAttempt`, creates a failure notification, and immediately marks the agent `IsAvailable = true`.
  2. The customer reschedules via `POST /api/orders/{id}/reschedule` specifying a future date.
  3. The system sets `Order.Status = Rescheduled`, stores `Order.RescheduledDate`, and automatically dispatches a replacement agent excluding the previous agent.
  4. The replacement agent advances $\text{Rescheduled} \longrightarrow \text{OutForDelivery} \longrightarrow \text{Delivered}$.

## 5. Security & Immutable Audit Trail
- **JWT Authentication & RBAC**: Endpoints enforce role policies (`Admin`, `Customer`, `Agent`). User identity is extracted directly from cryptographically signed claims (`sub`), preventing cross-customer data access.
- **Immutable Tracking History**: Every lifecycle change appends a record to `OrderStatusHistories` capturing `OrderId`, `Status`, `ActorId`, `ActorRole`, `Notes`, and UTC `Timestamp`. Historical entries are never updated or deleted.

## 6. Resilience & Fault Isolation
- **Multi-Channel Provider Isolation**: External email (SMTP) and SMS (Twilio) calls run within try-catch fault isolation boundaries. Network timeouts or provider outages never fail or roll back core delivery state transitions.
- **Communication Logging**: Every dispatch attempt is recorded with `Channel`, `EventType`, `DeliveryStatus` (`Sent`, `Simulated`, `Failed`), recipient, and timestamp for complete operational observability.
