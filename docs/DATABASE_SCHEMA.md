# DeliveryTracker — Database Schema & Data Dictionary

Database Engines:
- **Local Development**: **SQLite 3** (`Data Source=delivery.db`)
- **Production Deployment**: **PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL` on Render Free PostgreSQL)
ORM: **Entity Framework Core 10** with strictly applied code-first migrations (`__EFMigrationsHistory`) and automated master data seeding.

---

## Entity Relationship Overview

```text
       ┌───────────┐                 ┌─────────────┐
       │   Users   │───(1:N)────────▶│   Orders    │◀───(1:N)───┐
       └─────┬─────┘                 └──────┬──────┘            │
             │                              │                   │
             │ (1:1)                        ├───(1:N)───┐       │
             ▼                              ▼           ▼       │
       ┌───────────┐                 ┌───────────────┐ ┌────────────────┐
       │  Agents   │                 │ OrderStatus   │ │DeliveryAttempts│
       └─────┬─────┘                 │   Histories   │ └────────────────┘
             │ (N:1)                 └───────────────┘
             ▼                              ▲
       ┌───────────┐                        │
       │   Zones   │◀───(1:N)───┐            │
       └─────┬─────┘            │            │
             │ (1:N)            │            │
             ▼                  │            │
       ┌───────────┐            │            │
       │   Areas   │────────────┘            │
       └───────────┘                         │
                                             │
       ┌───────────────┐                     │
       │ Notifications │─────────────────────┘
       └───────────────┘

       ┌───────────┐
       │ RateCards │
       └───────────┘
```

---

## Tables & Data Dictionary

### 1. `Users`
Stores account profiles and credentials for all system roles.
| Column | Type | Nullable | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INTEGER` | No | PK, Auto-increment | Unique user identifier |
| `FullName` | `TEXT` | No | Max 100 chars | User's full name |
| `Email` | `TEXT` | No | Unique index | Login email address |
| `PasswordHash` | `TEXT` | No | — | PBKDF2 with HMAC-SHA256 password hash |
| `Role` | `INTEGER` | No | Enum: 0=Admin, 1=Customer, 2=Agent | System authorization role |
| `CreatedAt` | `TEXT (ISO)` | No | Default `UtcNow` | Account creation timestamp |

### 2. `Zones`
Geographical delivery zones for pricing and dispatching.
| Column | Type | Nullable | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INTEGER` | No | PK, Auto-increment | Unique zone ID |
| `Name` | `TEXT` | No | Max 100 chars | Descriptive name (e.g., South Mumbai) |
| `Code` | `TEXT` | No | Unique index | Unique zone code (e.g., `ZONE_A`) |

### 3. `Areas`
Specific neighborhoods belonging to a parent Zone.
| Column | Type | Nullable | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INTEGER` | No | PK, Auto-increment | Unique area ID |
| `Name` | `TEXT` | No | Max 100 chars | Neighborhood name (e.g., Colaba) |
| `Code` | `TEXT` | No | Unique index | Area code (e.g., `COLABA`) |
| `ZoneId` | `INTEGER` | No | FK $\rightarrow$ `Zones.Id` | Parent zone foreign key |

### 4. `Agents`
Delivery personnel profiles with real-time availability and stationing location.
| Column | Type | Nullable | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INTEGER` | No | PK, Auto-increment | Unique agent profile ID |
| `UserId` | `INTEGER` | No | FK $\rightarrow$ `Users.Id`, Unique | Associated user account |
| `ZoneId` | `INTEGER` | No | FK $\rightarrow$ `Zones.Id` | Assigned stationing zone |
| `IsAvailable` | `INTEGER (bool)`| No | Default 1 | Current dispatch availability |
| `Latitude` | `REAL` | No | GPS Latitude | Current stationing latitude |
| `Longitude` | `REAL` | No | GPS Longitude | Current stationing longitude |

### 5. `RateCards`
Configurable pricing matrices for B2B and B2C tiers.
| Column | Type | Nullable | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INTEGER` | No | PK, Auto-increment | Unique rate card ID |
| `OrderType` | `INTEGER` | No | Enum: 0=B2C, 1=B2B | Customer category tier |
| `IntraZoneRatePerKg` | `TEXT (decimal)` | No | Currency rate | Price/kg for same-zone deliveries |
| `InterZoneRatePerKg` | `TEXT (decimal)` | No | Currency rate | Price/kg for cross-zone deliveries |
| `CODSurcharge` | `TEXT (decimal)` | No | Flat fee | Surcharge for cash-on-delivery |

### 6. `Orders`
Central order entity capturing dimensional, pricing, and routing metadata.
| Column | Type | Nullable | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INTEGER` | No | PK, Auto-increment | Order unique identifier |
| `TrackingNumber` | `TEXT` | No | Unique index | Tracking code (`LM-YYYYMMDD-XXXXXX`) |
| `CustomerId` | `INTEGER` | No | FK $\rightarrow$ `Users.Id` | Booking customer ID |
| `PickupAreaId` | `INTEGER` | No | FK $\rightarrow$ `Areas.Id` | Origin area ID |
| `DropAreaId` | `INTEGER` | No | FK $\rightarrow$ `Areas.Id` | Destination area ID |
| `PickupAddress` | `TEXT` | No | — | Origin street address |
| `DropAddress` | `TEXT` | No | — | Destination street address |
| `LengthCm` | `REAL` | No | Package dimension | Length in centimeters |
| `WidthCm` | `REAL` | No | Package dimension | Width in centimeters |
| `HeightCm` | `REAL` | No | Package dimension | Height in centimeters |
| `ActualWeightKg`| `TEXT (decimal)` | No | Physical weight | Measured weight in kg |
| `VolumetricWeightKg`| `TEXT (decimal)` | No | Calculated weight | $\frac{L \times W \times H}{5000}$ in kg |
| `ChargeableWeightKg`| `TEXT (decimal)` | No | Effective weight | $\max(\text{Actual}, \text{Volumetric})$ |
| `OrderType` | `INTEGER` | No | Enum: 0=B2C, 1=B2B | Order tier category |
| `PaymentType` | `INTEGER` | No | Enum: 0=Prepaid, 1=COD | Payment arrangement |
| `RatePerKg` | `TEXT (decimal)` | No | Currency rate | Resolved unit shipping rate |
| `DeliveryFee` | `TEXT (decimal)` | No | Base shipping cost | $\text{ChargeableWeight} \times \text{RatePerKg}$ |
| `CODSurcharge`| `TEXT (decimal)` | No | Surcharge fee | COD handling surcharge |
| `TotalAmount` | `TEXT (decimal)` | No | Total order cost | $\text{DeliveryFee} + \text{CODSurcharge}$ |
| `Status` | `INTEGER` | No | Enum | Current delivery lifecycle state |
| `AssignedAgentId`| `INTEGER` | Yes | FK $\rightarrow$ `Agents.Id` | Currently assigned agent |
| `RescheduledDate`| `TEXT (ISO)` | Yes | Future date | Requested delivery reschedule date |
| `CreatedAt` | `TEXT (ISO)` | No | Default `UtcNow` | Order placement timestamp |
| `UpdatedAt` | `TEXT (ISO)` | No | Default `UtcNow` | Last state modification timestamp |

### 7. `OrderStatusHistories`
Append-only immutable audit trail recording every state progression.
| Column | Type | Nullable | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INTEGER` | No | PK, Auto-increment | Unique history record ID |
| `OrderId` | `INTEGER` | No | FK $\rightarrow$ `Orders.Id` | Target order identifier |
| `Status` | `INTEGER` | No | Enum | New status attained |
| `ActorId` | `INTEGER` | No | FK $\rightarrow$ `Users.Id` | User ID triggering state change |
| `ActorRole` | `INTEGER` | No | Enum | Role of actor (`Admin`/`Customer`/`Agent`)|
| `Notes` | `TEXT` | Yes | Audit justification | Reason, notes, or override remark |
| `Timestamp` | `TEXT (ISO)` | No | Default `UtcNow` | Exact UTC transition timestamp |

### 8. `DeliveryAttempts`
Tracks unsuccessful delivery attempts with failure justifications.
| Column | Type | Nullable | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INTEGER` | No | PK, Auto-increment | Unique attempt ID |
| `OrderId` | `INTEGER` | No | FK $\rightarrow$ `Orders.Id` | Target order identifier |
| `AgentId` | `INTEGER` | No | FK $\rightarrow$ `Agents.Id` | Agent conducting the attempt |
| `AttemptNumber`| `INTEGER` | No | Sequence count | 1st, 2nd, 3rd attempt count |
| `FailureReason`| `TEXT` | No | Justification | Reason delivery could not be completed |
| `AttemptedAt` | `TEXT (ISO)` | No | Default `UtcNow` | Exact UTC timestamp of attempt |

### 9. `Notifications`
Multi-channel dispatch logs for customer and operational notifications.
| Column | Type | Nullable | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `Id` | `INTEGER` | No | PK, Auto-increment | Unique notification ID |
| `UserId` | `INTEGER` | No | FK $\rightarrow$ `Users.Id` | Recipient customer/user ID |
| `OrderId` | `INTEGER` | No | FK $\rightarrow$ `Orders.Id` | Related order identifier |
| `Title` | `TEXT` | No | Max 150 chars | Notification title header |
| `Message` | `TEXT` | No | Max 500 chars | Body content / dispatch details |
| `RecipientEmail`| `TEXT` | No | Email address | Destination email |
| `RecipientPhone`| `TEXT` | Yes | Phone number | Destination SMS phone |
| `IsRead` | `INTEGER (bool)`| No | Default 0 | In-app read status |
| `Channel` | `INTEGER` | No | Enum: 0=InApp, 1=Email, 2=Sms | Communication channel |
| `EventType` | `TEXT` | No | Event key | Lifecycle event trigger identifier |
| `DeliveryStatus`| `INTEGER` | No | Enum: 0=Sent, 1=Simulated, 2=Failed | Dispatch delivery status |
| `ErrorMessage` | `TEXT` | Yes | Fault detail | Exception or error message if failed |
| `SentAt` | `TEXT (ISO)` | No | Default `UtcNow` | Dispatch timestamp |
