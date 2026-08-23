# DeliveryTracker — Rate Cards & Pricing Guide

## 1. Volumetric Weight Calculation
Air and last-mile logistics compute package weight using volumetric standardization to account for low-density packages consuming vehicle volume.

$$\text{Volumetric Weight (kg)} = \frac{\text{Length (cm)} \times \text{Width (cm)} \times \text{Height (cm)}}{5000}$$

### Examples:
- **Dense Parcel**: $10 \text{ cm} \times 10 \text{ cm} \times 10 \text{ cm}$, Actual Weight = $2.0 \text{ kg}$
  $$\text{Volumetric Weight} = \frac{1000}{5000} = 0.20 \text{ kg} \implies \text{Chargeable Weight} = \max(2.0, 0.20) = \mathbf{2.00 \text{ kg}}$$
- **Bulky Parcel**: $50 \text{ cm} \times 40 \text{ cm} \times 30 \text{ cm}$, Actual Weight = $3.0 \text{ kg}$
  $$\text{Volumetric Weight} = \frac{60000}{5000} = 12.00 \text{ kg} \implies \text{Chargeable Weight} = \max(3.0, 12.00) = \mathbf{12.00 \text{ kg}}$$

---

## 2. Rate Resolution Matrix
Shipping rates are determined dynamically by cross-referencing:
1. **Tier Category**: `B2C` (Retail Customers) vs `B2B` (Enterprise Accounts).
2. **Geographical Scope**:
   - **Intra-Zone**: Pickup Area and Drop Area reside within the **same** Zone (`pickupArea.ZoneId == dropArea.ZoneId`).
   - **Inter-Zone**: Pickup Area and Drop Area reside in **different** Zones (`pickupArea.ZoneId != dropArea.ZoneId`).

### Default Rate Cards (Admin Configurable)
| Tier | Intra-Zone Rate (₹/kg) | Inter-Zone Rate (₹/kg) | COD Surcharge (₹) | Description |
| :--- | :--- | :--- | :--- | :--- |
| **B2C** | ₹40.00 | ₹60.00 | ₹40.00 | Standard retail consumer shipping |
| **B2B** | ₹30.00 | ₹45.00 | ₹25.00 | Discounted high-volume commercial rate |

---

## 3. Total Fee Calculation Formula

$$\text{Delivery Fee} = \text{Chargeable Weight (kg)} \times \text{Rate Per Kg (₹)}$$
$$\text{Total Amount} = \text{Delivery Fee} + (\text{COD Surcharge if PaymentType} = \text{COD})$$

### Sample Calculation:
- **Order**: B2C, Colaba (Zone A) to Andheri (Zone B), COD Payment
- **Dimensions**: $25 \times 15 \times 10 \text{ cm}$, Actual Weight = $3.5 \text{ kg}$
  1. $\text{Volumetric Weight} = \frac{25 \times 15 \times 10}{5000} = 0.75 \text{ kg}$
  2. $\text{Chargeable Weight} = \max(3.5, 0.75) = 3.50 \text{ kg}$
  3. Zone A $\neq$ Zone B $\implies$ Inter-Zone B2C Rate = ₹60.00/kg
  4. $\text{Delivery Fee} = 3.50 \times 60.00 = \text{₹210.00}$
  5. $\text{COD Surcharge} = \text{₹40.00}$
  6. $\mathbf{\text{Total Amount}} = 210.00 + 40.00 = \mathbf{\text{₹250.00}}$
