export type UserRole = 'Admin' | 'Customer' | 'Agent';

export type OrderStatus = 
  | 'Created'
  | 'PickedUp'
  | 'InTransit'
  | 'OutForDelivery'
  | 'Delivered'
  | 'Failed'
  | 'Rescheduled';

export type OrderType = 'B2C' | 'B2B';
export type PaymentType = 'Prepaid' | 'COD';

export interface User {
  id: number;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface Zone {
  id: number;
  name: string;
  code: string;
}

export interface Area {
  id: number;
  name: string;
  code: string;
  zoneId: number;
  zone?: Zone;
}

export interface OrderStatusHistory {
  id: number;
  status: OrderStatus;
  actorId: number;
  actorRole: UserRole;
  notes?: string;
  timestamp: string;
}

export interface Order {
  id: number;
  trackingNumber: string;
  customerId: number;
  customerName?: string;
  pickupArea: string;
  pickupZone: string;
  dropArea: string;
  dropZone: string;
  pickupAddress: string;
  dropAddress: string;
  lengthCm: number;
  widthCm: number;
  heightCm: number;
  actualWeight: number;
  volumetricWeight: number;
  chargeableWeight: number;
  orderType: OrderType;
  paymentType: PaymentType;
  ratePerKg: number;
  deliveryFee: number;
  codSurcharge: number;
  totalAmount: number;
  status: OrderStatus;
  assignedAgentId?: number;
  assignedAgentName?: string;
  createdAt: string;
  updatedAt: string;
  rescheduledDate?: string;
  statusHistory: OrderStatusHistory[];
}

export interface OrderSummary {
  id: number;
  trackingNumber: string;
  customerId: number;
  pickupArea: string;
  dropArea: string;
  totalAmount: number;
  status: OrderStatus;
  assignedAgentId?: number;
  assignedAgentName?: string;
  createdAt: string;
}

export interface PriceCalculationResult {
  pickupZone: string;
  dropZone: string;
  actualWeight: number;
  volumetricWeight: number;
  chargeableWeight: number;
  ratePerKg: number;
  deliveryFee: number;
  codSurcharge: number;
  totalAmount: number;
}
