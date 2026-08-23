# DeliveryTracker — REST API Reference Documentation

Base URL: `http://localhost:5055/api` (or deployed URL)  
Authentication: HTTP Bearer Token (`Authorization: Bearer <JWT>`)

---

## 1. Authentication Endpoints

### `POST /api/auth/register`
Registers a new customer account and generates a JWT.
- **Access**: Public
- **Request Body**:
  ```json
  {
    "fullName": "Customer Name",
    "email": "customer@example.com",
    "password": "Password@123"
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "token": "eyJhbGciOi...",
    "user": {
      "id": 2,
      "fullName": "Customer Name",
      "email": "customer@example.com",
      "role": "Customer"
    }
  }
  ```

### `POST /api/auth/login`
Authenticates a user and returns a signed JWT with role claims.
- **Access**: Public
- **Request Body**:
  ```json
  {
    "email": "customer@delivery.com",
    "password": "Customer@123"
  }
  ```
- **Response (200 OK)**: Same as register.

---

## 2. Order & Pricing Endpoints

### `POST /api/orders/calculate-price`
Calculates volumetric weight, chargeable weight, rate tier, and COD surcharge.
- **Access**: Authenticated (`Customer`, `Admin`)
- **Request Body**:
  ```json
  {
    "pickupAreaId": 1,
    "dropAreaId": 2,
    "length": 25.0,
    "breadth": 15.0,
    "height": 10.0,
    "actualWeight": 3.5,
    "orderType": "B2C",
    "paymentType": "COD"
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "actualWeight": 3.5,
    "volumetricWeight": 0.75,
    "chargeableWeight": 3.5,
    "ratePerKg": 60.00,
    "deliveryFee": 210.00,
    "codSurcharge": 40.00,
    "totalAmount": 250.00,
    "isInterZone": true
  }
  ```

### `POST /api/orders`
Creates a delivery order.
- **Access**: `Customer` (for self), `Admin` (can pass any `customerId`).
- **Request Body**:
  ```json
  {
    "customerId": 2,
    "pickupAreaId": 1,
    "dropAreaId": 2,
    "pickupAddress": "100 South Terminal",
    "dropAddress": "200 West Warehouse",
    "length": 25.0,
    "breadth": 15.0,
    "height": 10.0,
    "actualWeight": 3.5,
    "orderType": "B2C",
    "paymentType": "Prepaid"
  }
  ```
- **Response (201 Created)**: Returns full `OrderResponse` with unique `trackingNumber` (e.g. `LM-20260823-A1B2C3`).

### `GET /api/orders`
Retrieves orders based on authenticated role and query parameters.
- **Access**: Authenticated
- **Query Parameters**:
  - `status`: Filter by status (`Created`, `PickedUp`, `InTransit`, `OutForDelivery`, `Delivered`, `Failed`, `Rescheduled`)
  - `zoneId`: Filter by zone ID
  - `agentId`: Filter by assigned agent ID
  - `search`: Filter by tracking number, customer name, area, or address
- **Role Scoping**:
  - `Customer`: Automatically scoped to own orders (`CustomerId = sub`).
  - `Agent`: Automatically scoped to assigned deliveries (`AssignedAgentId = agent.Id`).
  - `Admin`: Global visibility across all orders.

### `GET /api/orders/{id}`
Retrieves complete order detail including customer info, dimensions, weights, rates, and immutable status history.
- **Access**: Order Owner (`Customer`), Assigned `Agent`, or `Admin`. Unauthorized callers receive **403 Forbidden**.

### `PATCH /api/orders/{id}/status`
Transitions order status according to the finite state machine.
- **Access**: Assigned `Agent`, `Admin`.
- **Request Body**:
  ```json
  {
    "status": "OutForDelivery",
    "actorId": 101,
    "notes": "Loaded for transit"
  }
  ```

### `POST /api/orders/{id}/reschedule`
Reschedules a failed order and triggers replacement agent auto-assignment.
- **Access**: Order Owner (`Customer`).
- **Request Body**:
  ```json
  {
    "customerId": 2,
    "rescheduledDate": "2026-08-25T10:00:00Z",
    "notes": "Please deliver after 2 PM"
  }
  ```

### `POST /api/orders/{id}/assign`
Manually assigns a specific agent to an order.
- **Access**: `Admin` only.
- **Request Body**: `{"agentId": 1}`

### `POST /api/orders/{id}/auto-assign`
Triggers same-zone prioritized Haversine auto-assignment.
- **Access**: `Admin` only.

### `POST /api/orders/{id}/override-status`
Privileged status override with mandatory audit reason.
- **Access**: `Admin` only.
- **Request Body**:
  ```json
  {
    "status": "Delivered",
    "reason": "Customer collected package directly at central depot"
  }
  ```

---

## 3. Configuration & Infrastructure Endpoints

### `GET /api/zones`, `POST /api/zones`, `PUT /api/zones/{id}`, `DELETE /api/zones/{id}`
CRUD operations for geographic delivery zones.
- **Access**: `GET` (Public), `POST`/`PUT`/`DELETE` (`Admin` only).

### `GET /api/areas`, `POST /api/areas`, `PUT /api/areas/{id}`, `DELETE /api/areas/{id}`
CRUD operations for areas and zone reassignments.
- **Access**: `GET` (Public), `POST`/`PUT`/`DELETE` (`Admin` only).

### `GET /api/ratecards`, `PUT /api/ratecards/{id}`
Lookup and dynamic configuration of B2B/B2C intra/inter-zone rates and COD surcharges.
- **Access**: `GET` (Public), `PUT` (`Admin` only).

### `GET /api/agents`
Retrieves all delivery agents with current availability, coordinates, and assigned zones.
- **Access**: `Admin` only.

---

## 4. Multi-Channel Notifications Endpoints

### `GET /api/notifications`
Retrieves in-app notifications for authenticated customer or agent.
- **Access**: Authenticated.

### `PATCH /api/notifications/{id}/read`
Marks an in-app notification as read.
- **Access**: Notification Owner (`Customer`/`Agent`) or `Admin`.

### `GET /api/notifications/order/{orderId}/communications`
Retrieves all multi-channel communication logs (In-App, Email, SMS) for an order.
- **Access**: `Admin` only.
