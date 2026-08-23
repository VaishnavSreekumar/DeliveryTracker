# DeliveryTracker — Pricing Engine & Rate Cards Guide

## 1. Volumetric Weight Calculation
Air and last-mile logistics compute package weight using volumetric standardization to account for low-density packages consuming vehicle cargo capacity.

$$\text{Volumetric Weight (kg)} = \frac{\text{Length (cm)} \times \text{Width (cm)} \times \text{Height (cm)}}{5000}$$

### Chargeable Weight Rule
$$\text{Chargeable Weight (kg)} = \max(\text{Actual Weight}, \text{Volumetric Weight})$$

### Examples:
- **Case A: Actual Weight Dominant**:
  Dimensions: $10 \times 10 \times 10\text{ cm}$, Actual Weight = $2.0\text{ kg}$
  $$\text{Volumetric Weight} = \frac{10 \times 10 \times 10}{5000} = 0.20\text{ kg} \implies \text{Chargeable Weight} = \max(2.0, 0.20) = \mathbf{2.00\text{ kg}}$$
- **Case B: Volumetric Weight Dominant**:
  Dimensions: $50 \times 40 \times 30\text{ cm}$, Actual Weight = $3.0\text{ kg}$
  $$\text{Volumetric Weight} = \frac{50 \times 40 \times 30}{5000} = 12.00\text{ kg} \implies \text{Chargeable Weight} = \max(3.0, 12.00) = \mathbf{12.00\text{ kg}}$$

---

## 2. Rate Resolution Matrix
Shipping rates are determined dynamically from the `RateCards` database table:
1. **Tier Category**: `B2C` (Retail Consumers) vs `B2B` (Commercial / Enterprise).
2. **Geographical Scope**:
   - **Intra-Zone**: Origin Area and Destination Area reside in the **same** Zone (`pickupArea.ZoneId == dropArea.ZoneId`).
   - **Inter-Zone**: Origin Area and Destination Area reside in **different** Zones (`pickupArea.ZoneId != dropArea.ZoneId`).

### Active Seeded Rate Cards (Admin Configurable)
| Tier | Intra-Zone Rate (₹/kg) | Inter-Zone Rate (₹/kg) | COD Surcharge (₹) | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **B2C** | ₹40.00 | ₹60.00 | ₹40.00 | Standard consumer retail tier |
| **B2B** | ₹30.00 | ₹50.00 | ₹30.00 | High-volume enterprise commercial tier |

---

## 3. Total Fee Calculation Formula

$$\text{Delivery Fee} = \text{Chargeable Weight (kg)} \times \text{Rate Per Kg (₹)}$$
$$\text{Total Amount} = \text{Delivery Fee} + (\text{COD Surcharge if PaymentType} = \text{COD})$$

### Sample Verified Calculations:
1. **B2C Intra-Zone Prepaid**: $10 \times 10 \times 10\text{ cm}$ (0.2 kg vol), Actual 2.0 kg $\implies$ Chargeable 2.0 kg $\times$ ₹40.00/kg = **₹80.00**
2. **B2C Inter-Zone COD**: $25 \times 15 \times 10\text{ cm}$ (0.75 kg vol), Actual 3.5 kg $\implies$ Chargeable 3.5 kg $\times$ ₹60.00/kg (₹210.00) + COD Surcharge (₹40.00) = **₹250.00**
3. **B2B Intra-Zone Prepaid**: $20 \times 20 \times 20\text{ cm}$ (1.6 kg vol), Actual 5.0 kg $\implies$ Chargeable 5.0 kg $\times$ ₹30.00/kg = **₹150.00**
4. **B2B Inter-Zone COD**: $30 \times 20 \times 15\text{ cm}$ (1.8 kg vol), Actual 5.0 kg $\implies$ Chargeable 5.0 kg $\times$ ₹50.00/kg (₹250.00) + COD Surcharge (₹30.00) = **₹280.00**

---

## 4. Dynamic Admin Configuration
Administrators can update any RateCard rate or COD surcharge via `PUT /api/ratecards/{id}` or through the **Admin Configuration Manager** UI. All subsequent price calculations and order creation quotes dynamically reflect the updated rates immediately.
