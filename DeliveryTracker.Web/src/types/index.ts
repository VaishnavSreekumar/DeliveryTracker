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
  customerName?: string;
  pickupArea: string;
  pickupZone?: string;
  pickupZoneId?: number;
  dropArea: string;
  dropZone?: string;
  dropZoneId?: number;
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

export interface AppNotification {
  id: number;
  userId: number;
  orderId: number;
  orderTrackingNumber: string;
  title: string;
  message: string;
  recipientEmail: string;
  isRead: boolean;
  sentAt: string;
}

export interface RateCard {
  id: number;
  orderType: OrderType;
  intraZoneRatePerKg: number;
  interZoneRatePerKg: number;
  codSurcharge: number;
}

export interface AdminCustomer {
  id: number;
  fullName: string;
  email: string;
}

export interface AdminAgent {
  id: number;
  userId: number;
  name: string;
  email: string;
  zoneId: number;
  zoneName: string;
  isAvailable: boolean;
  latitude: number;
  longitude: number;
}

