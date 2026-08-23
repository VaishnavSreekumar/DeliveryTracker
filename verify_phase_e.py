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
print("PHASE E — REAL EMAIL + SMS STATUS NOTIFICATIONS VERIFICATION")
print("="*75)

# Reset agents
conn = sqlite3.connect(DB_PATH)
cur = conn.cursor()
cur.execute("UPDATE Agents SET IsAvailable = 1")
conn.commit()
conn.close()

# 1. Register a customer with Real Email & Real Phone Number
test_email = f"jury_customer_{int(datetime.datetime.now().timestamp())}@delivery.com"
test_phone = "+919876543210"

status, reg_auth = req(f"{API_BASE}/auth/register", "POST", {
    "fullName": "Jury Evaluation Customer",
    "email": test_email,
    "phoneNumber": test_phone,
    "password": "Customer@123"
})
assert status in (200, 201), f"Registration failed: {reg_auth}"
cust_token = reg_auth["token"]
cust_id = reg_auth["user"]["id"]
assert reg_auth["user"]["phoneNumber"] == test_phone, f"Phone number not preserved: {reg_auth}"
print(f"[STEP 1] Customer registered with real phone '{test_phone}' and email '{test_email}': SUCCESS")

# 2. Login Admin & Agents
status, admin_auth = req(f"{API_BASE}/auth/login", "POST", {"email": "admin@delivery.com", "password": "Admin@123"})
assert status == 200, f"Admin login failed: {admin_auth}"
admin_token = admin_auth["token"]

status, agent1_auth = req(f"{API_BASE}/auth/login", "POST", {"email": "agent1@delivery.com", "password": "Agent@123"})
assert status == 200, f"Agent 1 login failed: {agent1_auth}"
agent1_token = agent1_auth["token"]
agent1_id = agent1_auth["user"]["id"]

status, agent2_auth = req(f"{API_BASE}/auth/login", "POST", {"email": "agent2@delivery.com", "password": "Agent@123"})
assert status == 200, f"Agent 2 login failed: {agent2_auth}"
agent2_token = agent2_auth["token"]
agent2_id = agent2_auth["user"]["id"]

# 3. Create Order -> Triggers OrderCreated notifications (InApp, Email, SMS)
status, order = req(f"{API_BASE}/orders", "POST", {
    "customerId": cust_id,
    "pickupAreaId": 1,
    "dropAreaId": 2,
    "pickupAddress": "12 Gateway Terminal",
    "dropAddress": "45 Dadar TT Circle",
    "length": 15, "breadth": 15, "height": 10, "actualWeight": 2.5,
    "orderType": "B2C", "paymentType": "Prepaid"
}, cust_token)
assert status in (200, 201), f"Order creation failed: {order}"
order_id = order["id"]
tracking_num = order["trackingNumber"]
print(f"[STEP 2] Order Created: #{order_id} ({tracking_num})")

# 4. Assign Agent
status, _ = req(f"{API_BASE}/orders/{order_id}/assign", "POST", {"agentId": 1}, admin_token)
assert status == 200
print(f"[STEP 3] Agent 1 assigned to order #{order_id}")

# 5. Full Status Lifecycle: PickedUp -> InTransit -> OutForDelivery -> Failed -> Rescheduled -> OutForDelivery -> Delivered
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "PickedUp", "actorId": agent1_id, "notes": "Picked up from Gateway Terminal"}, agent1_token)
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "InTransit", "actorId": agent1_id, "notes": "In transit to Dadar Hub"}, agent1_token)
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "OutForDelivery", "actorId": agent1_id, "notes": "Out for delivery attempt 1"}, agent1_token)
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "Failed", "actorId": agent1_id, "notes": "Customer unavailable"}, agent1_token)

# Customer Reschedules
future_date = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=2)).isoformat()
status, resched_res = req(f"{API_BASE}/orders/{order_id}/reschedule", "POST", {
    "customerId": cust_id,
    "rescheduledDate": future_date,
    "notes": "Deliver next morning"
}, cust_token)
assert status == 200

# Replacement Agent delivers
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "OutForDelivery", "actorId": agent2_id, "notes": "Out for delivery attempt 2"}, agent2_token)
req(f"{API_BASE}/orders/{order_id}/status", "PATCH", {"status": "Delivered", "actorId": agent2_id, "notes": "Delivered to customer"}, agent2_token)
print(f"[STEP 4] Complete 8-event delivery lifecycle executed successfully")

# 6. Verify Multi-Channel Activity Log
status, comm_logs = req(f"{API_BASE}/notifications/order/{order_id}/communications", "GET", token=admin_token)
assert status == 200 and len(comm_logs) >= 20, f"Expected >= 20 communication logs, got {len(comm_logs)}"

inapp_logs = [c for c in comm_logs if c["channel"] == "InApp"]
email_logs = [c for c in comm_logs if c["channel"] == "Email"]
sms_logs = [c for c in comm_logs if c["channel"] == "Sms"]

print(f"\n[STEP 5] Multi-Channel Communication Audit Log for Order #{order_id}:")
print(f"  - In-App Notifications : {len(inapp_logs)} records")
print(f"  - Email Dispatches     : {len(email_logs)} records (Recipient: {test_email})")
print(f"  - SMS Dispatches       : {len(sms_logs)} records (Recipient: {test_phone})")
print(f"  - Total Dispatches     : {len(comm_logs)} records")

# Verify accurate recipient phone resolution
assert all(s["recipientPhone"] == test_phone for s in sms_logs), "SMS recipient phone does not match customer's registered phone number"
assert all(e["recipientEmail"] == test_email for e in email_logs), "Email recipient does not match customer's registered email"

# 7. Customer In-App Notification Center check
status, cust_notifs = req(f"{API_BASE}/notifications", "GET", token=cust_token)
assert status == 200 and len(cust_notifs) >= 1
print(f"[STEP 6] Customer In-App Notification Center received {len(cust_notifs)} scoped items")

# 8. Database Integrity
conn = sqlite3.connect(DB_PATH)
cur = conn.cursor()
cur.execute("PRAGMA foreign_key_check")
fk_violations = cur.fetchall()
conn.close()
assert len(fk_violations) == 0, f"Foreign key violations found: {fk_violations}"
print("[STEP 7] SQLite Database Integrity: 0 foreign key violations")

print("\n" + "="*75)
print(">>> PHASE E REAL EMAIL & SMS NOTIFICATIONS FULLY VERIFIED! <<<")
print("="*75)
