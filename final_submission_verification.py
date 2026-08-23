import json
import urllib.request
import sqlite3
import datetime

API_BASE = "http://localhost:5055/api"
DB_PATH = r"C:\Users\vaish\.gemini\antigravity-ide\scratch\DeliveryTracker\DeliveryTracker.API\delivery.db"

def req(url, method="GET", data=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    body = json.dumps(data).encode("utf-8") if data is not None else None
    request = urllib.request.Request(url, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request) as resp:
            content = resp.read().decode("utf-8")
            return resp.status, json.loads(content) if content else {}
    except urllib.error.HTTPError as e:
        err_msg = e.read().decode("utf-8")
        try:
            return e.code, json.loads(err_msg)
        except:
            return e.code, err_msg
    except Exception as e:
        return 0, str(e)

print("="*75)
print("FINAL D4 SOURCE-OF-TRUTH & REGRESSION VERIFICATION")
print("="*75)

# Reset agents
conn = sqlite3.connect(DB_PATH)
cur = conn.cursor()
cur.execute("UPDATE Agents SET IsAvailable = 1")
cur.execute("UPDATE RateCards SET IntraZoneRatePerKg = 40.0, InterZoneRatePerKg = 60.0, CODSurcharge = 40.0 WHERE OrderType = 'B2C' OR OrderType = 0")
cur.execute("UPDATE RateCards SET IntraZoneRatePerKg = 30.0, InterZoneRatePerKg = 50.0, CODSurcharge = 30.0 WHERE OrderType = 'B2B' OR OrderType = 1")
conn.commit()
conn.close()

# -------------------------------------------------------------
# 1. AUTHENTICATION & TOKENS
# -------------------------------------------------------------
status, admin_auth = req(f"{API_BASE}/auth/login", "POST", {"email": "admin@delivery.com", "password": "Admin@123"})
assert status == 200, f"Admin login failed: {admin_auth}"
admin_token = admin_auth["token"]

status, custA_auth = req(f"{API_BASE}/auth/login", "POST", {"email": "customer@delivery.com", "password": "Customer@123"})
assert status == 200, f"Customer A login failed: {custA_auth}"
custA_token = custA_auth["token"]
custA_id = custA_auth["user"]["id"]

status, agent1_auth = req(f"{API_BASE}/auth/login", "POST", {"email": "agent1@delivery.com", "password": "Agent@123"})
assert status == 200, f"Agent 1 login failed: {agent1_auth}"
agent1_token = agent1_auth["token"]
agent1_user_id = agent1_auth["user"]["id"]

status, agent2_auth = req(f"{API_BASE}/auth/login", "POST", {"email": "agent2@delivery.com", "password": "Agent@123"})
assert status == 200, f"Agent 2 login failed: {agent2_auth}"
agent2_token = agent2_auth["token"]
agent2_user_id = agent2_auth["user"]["id"]

print("[GATE 1] Authentication & JWT generation: PASSED")

# -------------------------------------------------------------
# 2. PRICING REGRESSION (Cases A-F + Dynamic RateCard update)
# -------------------------------------------------------------
# Case A: Actual > Volumetric (10x10x10 cm, 2.0 kg -> Vol 0.2, Chargeable 2.0)
status, priceA = req(f"{API_BASE}/orders/calculate-price", "POST", {
    "pickupAreaId": 1, "dropAreaId": 1, "length": 10, "breadth": 10, "height": 10, "actualWeight": 2.0, "orderType": "B2C", "paymentType": "Prepaid"
}, custA_token)
assert status == 200 and priceA["volumetricWeight"] == 0.2 and priceA["chargeableWeight"] == 2.0 and priceA["totalAmount"] == 80.0
print("  - Case A (Actual > Volumetric): OK (Vol: 0.20kg, Chargeable: 2.00kg, Total: Rs. 80.00)")

# Case B: Volumetric > Actual (50x40x30 cm, 3.0 kg -> Vol 12.0, Chargeable 12.0)
status, priceB = req(f"{API_BASE}/orders/calculate-price", "POST", {
    "pickupAreaId": 1, "dropAreaId": 1, "length": 50, "breadth": 40, "height": 30, "actualWeight": 3.0, "orderType": "B2C", "paymentType": "Prepaid"
}, custA_token)
assert status == 200 and priceB["volumetricWeight"] == 12.0 and priceB["chargeableWeight"] == 12.0 and priceB["totalAmount"] == 480.0
print("  - Case B (Volumetric > Actual): OK (Vol: 12.00kg, Chargeable: 12.00kg, Total: Rs. 480.00)")

# Case C: B2C Intra Prepaid (Colaba -> Dadar, Intra)
status, priceC = req(f"{API_BASE}/orders/calculate-price", "POST", {
    "pickupAreaId": 1, "dropAreaId": 2, "length": 10, "breadth": 10, "height": 10, "actualWeight": 2.0, "orderType": "B2C", "paymentType": "Prepaid"
}, custA_token)
assert status == 200 and priceC["ratePerKg"] == 40.0 and priceC["codSurcharge"] == 0.0 and priceC["totalAmount"] == 80.0
print("  - Case C (B2C Intra Prepaid): OK (Rate: Rs. 40/kg, Total: Rs. 80.00)")

# Case D: B2C Inter COD (Colaba (1) -> Andheri (3), Inter)
status, priceD = req(f"{API_BASE}/orders/calculate-price", "POST", {
    "pickupAreaId": 1, "dropAreaId": 3, "length": 25, "breadth": 15, "height": 10, "actualWeight": 3.5, "orderType": "B2C", "paymentType": "COD"
}, custA_token)
assert status == 200 and priceD["ratePerKg"] == 60.0 and priceD["codSurcharge"] == 40.0 and priceD["totalAmount"] == 250.0
print("  - Case D (B2C Inter COD): OK (Rate: Rs. 60/kg, COD: Rs. 40, Total: Rs. 250.00)")

# Case E: B2B Intra Prepaid (Colaba -> Dadar, Rate 30.0)
status, priceE = req(f"{API_BASE}/orders/calculate-price", "POST", {
    "pickupAreaId": 1, "dropAreaId": 2, "length": 20, "breadth": 20, "height": 20, "actualWeight": 5.0, "orderType": "B2B", "paymentType": "Prepaid"
}, custA_token)
assert status == 200 and priceE["ratePerKg"] == 30.0 and priceE["codSurcharge"] == 0.0 and priceE["totalAmount"] == 150.0
print("  - Case E (B2B Intra Prepaid): OK (Rate: Rs. 30/kg, Total: Rs. 150.00)")

# Case F: B2B Inter COD (Colaba -> Andheri, Rate 50.0, COD 30.0)
status, priceF = req(f"{API_BASE}/orders/calculate-price", "POST", {
    "pickupAreaId": 1, "dropAreaId": 3, "length": 30, "breadth": 20, "height": 15, "actualWeight": 5.0, "orderType": "B2B", "paymentType": "COD"
}, custA_token)
assert status == 200 and priceF["ratePerKg"] == 50.0 and priceF["codSurcharge"] == 30.0 and priceF["totalAmount"] == 280.0
print("  - Case F (B2B Inter COD): OK (Rate: Rs. 50/kg, COD: Rs. 30, Total: Rs. 280.00)")

# Dynamic RateCard update test: Admin updates B2C Intra rate to 45.0
status, update_rc = req(f"{API_BASE}/ratecards/1", "PUT", {
    "orderType": "B2C", "intraZoneRatePerKg": 45.0, "interZoneRatePerKg": 60.0, "codSurcharge": 40.0
}, admin_token)
assert status == 200

status, price_dyn = req(f"{API_BASE}/orders/calculate-price", "POST", {
    "pickupAreaId": 1, "dropAreaId": 2, "length": 10, "breadth": 10, "height": 10, "actualWeight": 2.0, "orderType": "B2C", "paymentType": "Prepaid"
}, custA_token)
assert status == 200 and price_dyn["ratePerKg"] == 45.0 and price_dyn["totalAmount"] == 90.0
print("  - Dynamic Pricing Test: Updated rate immediately reflected (Rs. 45/kg -> Rs. 90.00)")

# Restore original rate card
req(f"{API_BASE}/ratecards/1", "PUT", {
    "orderType": "B2C", "intraZoneRatePerKg": 40.0, "interZoneRatePerKg": 60.0, "codSurcharge": 40.0
}, admin_token)
print("[GATE 2] Pricing Engine & Dynamic Rate Resolution: PASSED")

# -------------------------------------------------------------
# 3. COMPLETE E2E LIFECYCLE (Auto-Assign & Failure Recovery)
# -------------------------------------------------------------
# 1. Customer creates order
status, order = req(f"{API_BASE}/orders", "POST", {
    "customerId": custA_id,
    "pickupAreaId": 1,
    "dropAreaId": 3,
    "pickupAddress": "Gate 1, South Pier",
    "dropAddress": "Plot 42, West IT Park",
    "length": 25, "breadth": 15, "height": 10, "actualWeight": 3.5,
    "orderType": "B2C", "paymentType": "COD"
}, custA_token)
assert status in (200, 201)
order_id = order["id"]
tracking_num = order["trackingNumber"]
print(f"\n[GATE 3] E2E Lifecycle for Order #{order_id} ({tracking_num}):")
print(f"  [1] Created: Rs. {order['totalAmount']} (Chargeable: {order['chargeableWeight']}kg, Status: {order['status']})")

# 2. Admin triggers Auto-Assignment (Agent 1 in Zone A selected)
status, auto_res = req(f"{API_BASE}/orders/{order_id}/auto-assign", "POST", token=admin_token)
assert status == 200
print(f"  [2] Auto-Assigned: Agent '{auto_res['assignedAgent']['name']}' ({auto_res['assignedAgent']['zoneName']})")

# 3. Agent 1 transitions: PickedUp -> InTransit -> OutForDelivery -> Failed
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "PickedUp", "actorId": agent1_user_id, "notes": "Picked up from South Pier"}, agent1_token)
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "InTransit", "actorId": agent1_user_id, "notes": "In transit to West zone"}, agent1_token)
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "OutForDelivery", "actorId": agent1_user_id, "notes": "Out for delivery attempt 1"}, agent1_token)
status, fail_res = req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "Failed", "actorId": agent1_user_id, "notes": "Customer security gate locked"}, agent1_token)
assert status == 200 and fail_res["currentStatus"] == "Failed"
print("  [3] Agent 1 advanced PickedUp -> InTransit -> OutForDelivery -> Failed")

# 4. Customer Reschedules
future_date = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=2)).isoformat()
status, resched_res = req(f"{API_BASE}/orders/{order_id}/reschedule", "POST", {
    "customerId": custA_id,
    "rescheduledDate": future_date,
    "notes": "Please deliver after 3 PM"
}, custA_token)
assert status == 200
print(f"  [4] Rescheduled: New Agent '{resched_res['newAgent']['name']}' auto-assigned (Previous Agent 1 released)")

# 5. Replacement Agent 2 delivers: OutForDelivery -> Delivered
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "OutForDelivery", "actorId": agent2_user_id, "notes": "Out for delivery attempt 2"}, agent2_token)
status, deliv_res = req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "Delivered", "actorId": agent2_user_id, "notes": "Delivered to customer"}, agent2_token)
assert status == 200 and deliv_res["currentStatus"] == "Delivered"
print("  [5] Delivered: Completed by Agent 2")

# 6. Verify 8-step immutable audit history
status, full_order = req(f"{API_BASE}/orders/{order_id}", "GET", token=custA_token)
assert status == 200
history = full_order["statusHistory"]
assert len(history) == 8, f"Expected 8 history records, got {len(history)}"
expected_sequence = ["Created", "PickedUp", "InTransit", "OutForDelivery", "Failed", "Rescheduled", "OutForDelivery", "Delivered"]
actual_sequence = [h["status"] for h in history]
assert actual_sequence == expected_sequence, f"Sequence mismatch: {actual_sequence} != {expected_sequence}"
print(f"  [6] Immutable Audit Trail: 8 sequential events verified -> {' -> '.join(actual_sequence)}")
print("[GATE 3] Complete E2E Lifecycle & Audit Trail: PASSED")

# -------------------------------------------------------------
# 4. SECURITY & AUTHORIZATION BOUNDARIES
# -------------------------------------------------------------
# 1. No token -> 401
status, _ = req(f"{API_BASE}/orders", "GET")
assert status == 401, f"Expected 401 without token, got {status}"

# 2. Customer cannot access Admin endpoints (403)
status, _ = req(f"{API_BASE}/ratecards/1", "PUT", {"orderType": "B2C", "intraZoneRatePerKg": 40.0, "interZoneRatePerKg": 60.0, "codSurcharge": 40.0}, custA_token)
assert status == 403, f"Expected 403 for Customer modifying RateCard, got {status}"

status, _ = req(f"{API_BASE}/orders/{order_id}/assign", "POST", {"agentId": 2}, custA_token)
assert status == 403, f"Expected 403 for Customer assigning agent, got {status}"

status, _ = req(f"{API_BASE}/orders/{order_id}/override-status", "POST", {"status": "Delivered", "reason": "unauthorized"}, custA_token)
assert status == 403, f"Expected 403 for Customer status override, got {status}"

# 3. Agent cannot access Admin endpoints (403)
status, _ = req(f"{API_BASE}/orders/{order_id}/assign", "POST", {"agentId": 2}, agent1_token)
assert status == 403, f"Expected 403 for Agent assigning agent, got {status}"

status, _ = req(f"{API_BASE}/orders/{order_id}/override-status", "POST", {"status": "Delivered", "reason": "unauthorized"}, agent1_token)
assert status == 403, f"Expected 403 for Agent status override, got {status}"

print("[GATE 4] Security & Role-Based Authorization Boundaries: PASSED")

# -------------------------------------------------------------
# 5. MULTI-CHANNEL NOTIFICATIONS & DATABASE INTEGRITY
# -------------------------------------------------------------
status, comms = req(f"{API_BASE}/notifications/order/{order_id}/communications", "GET", token=admin_token)
assert status == 200 and len(comms) >= 10, f"Expected communications logs, got {len(comms)}"
channels = set(c["channel"] for c in comms)
assert "InApp" in channels and "Email" in channels and "Sms" in channels
print(f"[GATE 5] Multi-Channel Communication Logs: {len(comms)} records stored across In-App, Email, SMS")

conn = sqlite3.connect(DB_PATH)
cur = conn.cursor()
cur.execute("PRAGMA foreign_key_check")
fk_violations = cur.fetchall()
conn.close()
assert len(fk_violations) == 0, f"Foreign key violations found: {fk_violations}"
print(f"[GATE 6] SQLite Database Integrity: 0 foreign key violations")

print("\n" + "="*75)
print(">>> ALL FINAL D4 SOURCE-OF-TRUTH VERIFICATION GATES PASSED! <<<")
print("="*75)
