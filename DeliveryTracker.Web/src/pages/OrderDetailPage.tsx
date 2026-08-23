import React, { useState, useEffect } from 'react';
import type { Order, AppNotification } from '../types';
import { useAuth } from '../context/AuthContext';
import { apiClient } from '../api/apiClient';
import { StatusBadge } from '../components/StatusBadge';
import { TrackingTimeline } from '../components/TrackingTimeline';
import { RescheduleModal } from '../components/RescheduleModal';
import { AgentStatusModal } from '../components/AgentStatusModal';
import { ArrowLeft, Copy, MapPin, Truck, Scale, DollarSign, Calendar, AlertTriangle, Check, RefreshCw, Mail, MessageSquare, Bell } from 'lucide-react';

interface OrderDetailPageProps {
  orderId: number;
  onBack: () => void;
}

export const OrderDetailPage: React.FC<OrderDetailPageProps> = ({ orderId, onBack }) => {
  const { user } = useAuth();
  const [order, setOrder] = useState<Order | null>(null);
  const [communications, setCommunications] = useState<AppNotification[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  // Modals state
  const [isRescheduleOpen, setIsRescheduleOpen] = useState(false);
  const [isStatusUpdateOpen, setIsStatusUpdateOpen] = useState(false);
  const [isOverrideOpen, setIsOverrideOpen] = useState(false);
  const [overrideForm, setOverrideForm] = useState<{ status: string; reason: string }>({
    status: 'Delivered',
    reason: '',
  });
  const [overrideError, setOverrideError] = useState<string | null>(null);

  const fetchOrder = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await apiClient.get<Order>(`/orders/${orderId}`);
      setOrder(data);

      if (user?.role === 'Admin') {
        try {
          const comms = await apiClient.get<AppNotification[]>(`/notifications/order/${orderId}/communications`);
          setCommunications(comms);
        } catch {
          // ignore comm log failure
        }
      }
    } catch (err: any) {
      setError(err.message || `Failed to fetch order #${orderId}`);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchOrder();
  }, [orderId]);

  const handleCopyTracking = () => {
    if (order) {
      navigator.clipboard.writeText(order.trackingNumber);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleExecuteOverride = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!order || !overrideForm.reason.trim()) return;
    setOverrideError(null);
    try {
      await apiClient.post(`/orders/${order.id}/override-status`, overrideForm);
      setIsOverrideOpen(false);
      fetchOrder();
    } catch (err: any) {
      setOverrideError(err.message || 'Status override failed.');
    }
  };

  if (isLoading) {
    return <div style={{ color: 'var(--text-muted)', padding: '2rem', textAlign: 'center' }}>Loading order details...</div>;
  }

  if (error || !order) {
    return (
      <div style={{ padding: '2rem' }}>
        <button onClick={onBack} className="btn btn-secondary" style={{ marginBottom: '1rem' }}>
          <ArrowLeft size={16} /> Back to Orders
        </button>
        <div style={{ backgroundColor: 'rgba(244, 63, 94, 0.15)', border: '1px solid rgba(244, 63, 94, 0.3)', color: '#fb7185', padding: '1rem', borderRadius: 'var(--radius-md)' }}>
          <AlertTriangle size={18} /> {error || 'Order not found.'}
        </div>
      </div>
    );
  }

  const isCustomer = user?.role === 'Customer';
  const isAgent = user?.role === 'Agent';
  const isAdmin = user?.role === 'Admin';

  const canReschedule = isCustomer && order.status === 'Failed';
  const canAgentUpdateStatus =
    isAgent &&
    order.status !== 'Delivered' &&
    order.status !== 'Failed';

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Navigation Top Action */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <button onClick={onBack} className="btn btn-secondary">
          <ArrowLeft size={16} /> Back to Orders List
        </button>

        <div style={{ display: 'flex', gap: '0.5rem' }}>
          {isAdmin && (
            <button
              onClick={() => {
                setOverrideForm({ status: order.status, reason: '' });
                setIsOverrideOpen(true);
              }}
              className="btn btn-secondary"
              style={{ color: '#f59e0b', borderColor: 'rgba(245, 158, 11, 0.4)' }}
            >
              <AlertTriangle size={15} /> Admin Override Status
            </button>
          )}

          <button onClick={fetchOrder} className="btn btn-secondary btn-sm">
            <RefreshCw size={14} /> Refresh Order
          </button>
        </div>
      </div>

      {/* Primary Header Card */}
      <div className="card">
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem', borderBottom: '1px solid var(--border-subtle)', paddingBottom: '1rem', marginBottom: '1rem' }}>
          <div>
            <div style={{ fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-muted)', fontWeight: 600 }}>
              Tracking Number
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '2px' }}>
              <span className="tracking-mono" style={{ fontSize: '1.25rem', color: 'var(--brand-primary)' }}>
                {order.trackingNumber}
              </span>
              <button
                onClick={handleCopyTracking}
                title="Copy Tracking Number"
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', display: 'flex', alignItems: 'center' }}
              >
                {copied ? <Check size={16} color="var(--status-delivered-fg)" /> : <Copy size={16} />}
              </button>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
            <StatusBadge status={order.status} size="lg" />

            {canReschedule && (
              <button onClick={() => setIsRescheduleOpen(true)} className="btn btn-primary">
                <Calendar size={16} /> Reschedule Delivery
              </button>
            )}

            {canAgentUpdateStatus && (
              <button onClick={() => setIsStatusUpdateOpen(true)} className="btn btn-primary">
                <Truck size={16} /> Update Delivery Status
              </button>
            )}
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem', fontSize: '0.8125rem', color: 'var(--text-secondary)' }}>
          <div>
            <span style={{ color: 'var(--text-muted)', display: 'block', fontSize: '0.7rem', textTransform: 'uppercase' }}>Customer</span>
            <strong style={{ color: 'var(--text-primary)' }}>{order.customerName || `Customer #${order.customerId}`}</strong>
          </div>
          <div>
            <span style={{ color: 'var(--text-muted)', display: 'block', fontSize: '0.7rem', textTransform: 'uppercase' }}>Assigned Agent</span>
            <strong style={{ color: order.assignedAgentName ? 'var(--brand-primary)' : 'var(--status-outfordelivery-fg)' }}>
              {order.assignedAgentName ? `${order.assignedAgentName} (ID #${order.assignedAgentId})` : 'Awaiting Assignment'}
            </strong>
          </div>
          <div>
            <span style={{ color: 'var(--text-muted)', display: 'block', fontSize: '0.7rem', textTransform: 'uppercase' }}>Created Date</span>
            <strong style={{ color: 'var(--text-primary)' }}>{new Date(order.createdAt).toLocaleString()}</strong>
          </div>
          <div>
            <span style={{ color: 'var(--text-muted)', display: 'block', fontSize: '0.7rem', textTransform: 'uppercase' }}>Last Updated</span>
            <strong style={{ color: 'var(--text-primary)' }}>{new Date(order.updatedAt).toLocaleString()}</strong>
          </div>
          {order.rescheduledDate && (
            <div>
              <span style={{ color: 'var(--text-muted)', display: 'block', fontSize: '0.7rem', textTransform: 'uppercase' }}>Rescheduled For</span>
              <strong style={{ color: '#fbbf24' }}>{new Date(order.rescheduledDate).toLocaleDateString(undefined, { weekday: 'short', year: 'numeric', month: 'short', day: 'numeric' })}</strong>
            </div>
          )}
        </div>
      </div>

      {/* 2-Column Main Layout: Details vs Vertical Timeline */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: '1.5rem' }}>
        {/* Left Column: Route, Package, Pricing */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          {/* Route Card */}
          <div className="card">
            <h4 style={{ fontSize: '0.875rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <MapPin size={16} color="var(--brand-primary)" /> Delivery Route & Addresses
            </h4>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              <div style={{ borderLeft: '3px solid var(--brand-primary)', paddingLeft: '0.75rem' }}>
                <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>Pickup Location</span>
                <div style={{ fontSize: '0.875rem', fontWeight: 600, color: 'var(--text-primary)', marginTop: '2px' }}>
                  {order.pickupArea} ({order.pickupZone})
                </div>
                <div style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
                  {order.pickupAddress}
                </div>
              </div>

              <div style={{ borderLeft: '3px solid var(--status-outfordelivery-fg)', paddingLeft: '0.75rem' }}>
                <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>Drop Destination</span>
                <div style={{ fontSize: '0.875rem', fontWeight: 600, color: 'var(--text-primary)', marginTop: '2px' }}>
                  {order.dropArea} ({order.dropZone})
                </div>
                <div style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
                  {order.dropAddress}
                </div>
              </div>
            </div>
          </div>

          {/* Package & Pricing Summary Card */}
          <div className="card">
            <h4 style={{ fontSize: '0.875rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <Scale size={16} color="var(--brand-primary)" /> Package & Pricing Breakdown
            </h4>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', fontSize: '0.8125rem', marginBottom: '1rem' }}>
              <div>
                <span style={{ color: 'var(--text-muted)', display: 'block', fontSize: '0.7rem' }}>Dimensions</span>
                <strong>{order.lengthCm} &times; {order.widthCm} &times; {order.heightCm} cm</strong>
              </div>
              <div>
                <span style={{ color: 'var(--text-muted)', display: 'block', fontSize: '0.7rem' }}>Actual Weight</span>
                <strong>{order.actualWeight.toFixed(2)} kg</strong>
              </div>
              <div>
                <span style={{ color: 'var(--text-muted)', display: 'block', fontSize: '0.7rem' }}>Volumetric Weight</span>
                <strong>{order.volumetricWeight.toFixed(2)} kg</strong>
              </div>
              <div>
                <span style={{ color: 'var(--text-muted)', display: 'block', fontSize: '0.7rem' }}>Chargeable Weight</span>
                <strong style={{ color: 'var(--brand-primary)' }}>{order.chargeableWeight.toFixed(2)} kg</strong>
              </div>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', fontSize: '0.875rem', borderTop: '1px solid var(--border-subtle)', paddingTop: '0.75rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)' }}>
                <span>Rate Tier / Payment</span>
                <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{order.orderType} | {order.paymentType}</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)' }}>
                <span>Base Delivery Fee</span>
                <span>₹{order.deliveryFee.toFixed(2)}</span>
              </div>
              {order.codSurcharge > 0 && (
                <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--status-outfordelivery-fg)' }}>
                  <span>COD Surcharge</span>
                  <span>+ ₹{order.codSurcharge.toFixed(2)}</span>
                </div>
              )}
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', paddingTop: '0.5rem', borderTop: '1px solid var(--border-subtle)' }}>
                <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>Total Order Amount</span>
                <span style={{ fontSize: '1.25rem', fontWeight: 700, fontFamily: 'var(--font-mono)' }}>₹{order.totalAmount.toFixed(2)}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Right Column: Signature Vertical Tracking Timeline */}
        <div className="card">
          <h4 style={{ fontSize: '0.875rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '1.25rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <DollarSign size={16} color="var(--brand-primary)" /> Immutable Tracking Audit Trail
          </h4>

          <TrackingTimeline history={order.statusHistory || []} />
        </div>
      </div>

      {/* Admin Multi-Channel Communication Audit Trail */}
      {isAdmin && (
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem', borderBottom: '1px solid var(--border-subtle)', paddingBottom: '0.75rem' }}>
            <h4 style={{ fontSize: '0.875rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--brand-primary)', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <Mail size={16} /> Multi-Channel Communication Activity Log ({communications.length})
            </h4>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>In-App • Email • SMS Dispatch Tracking</span>
          </div>

          {communications.length === 0 ? (
            <div style={{ color: 'var(--text-muted)', fontSize: '0.8125rem', padding: '1rem 0', textAlign: 'center' }}>
              No communication dispatches recorded for this order yet.
            </div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--border-subtle)', textAlign: 'left', color: 'var(--text-muted)' }}>
                    <th style={{ padding: '0.5rem' }}>Channel</th>
                    <th style={{ padding: '0.5rem' }}>Event</th>
                    <th style={{ padding: '0.5rem' }}>Recipient</th>
                    <th style={{ padding: '0.5rem' }}>Status</th>
                    <th style={{ padding: '0.5rem' }}>Message Preview</th>
                    <th style={{ padding: '0.5rem' }}>Timestamp</th>
                  </tr>
                </thead>
                <tbody>
                  {communications.map((c) => (
                    <tr key={c.id} style={{ borderBottom: '1px solid var(--border-subtle)' }}>
                      <td style={{ padding: '0.625rem 0.5rem', fontWeight: 600 }}>
                        <span style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '0.375rem',
                          padding: '0.2rem 0.5rem',
                          borderRadius: '4px',
                          fontSize: '0.75rem',
                          backgroundColor: c.channel === 'Email' ? 'rgba(59, 130, 246, 0.15)' : c.channel === 'Sms' ? 'rgba(16, 185, 129, 0.15)' : 'rgba(139, 92, 246, 0.15)',
                          color: c.channel === 'Email' ? '#60a5fa' : c.channel === 'Sms' ? '#34d399' : '#a78bfa'
                        }}>
                          {c.channel === 'Email' && <Mail size={12} />}
                          {c.channel === 'Sms' && <MessageSquare size={12} />}
                          {c.channel === 'InApp' && <Bell size={12} />}
                          {c.channel || 'InApp'}
                        </span>
                      </td>
                      <td style={{ padding: '0.625rem 0.5rem', fontFamily: 'var(--font-mono)', fontSize: '0.75rem' }}>
                        {c.eventType || 'General'}
                      </td>
                      <td style={{ padding: '0.625rem 0.5rem', color: 'var(--text-secondary)' }}>
                        {c.channel === 'Sms' ? (c.recipientPhone || 'Customer Phone') : c.recipientEmail}
                      </td>
                      <td style={{ padding: '0.625rem 0.5rem' }}>
                        <span style={{
                          padding: '0.15rem 0.4rem',
                          borderRadius: '3px',
                          fontSize: '0.7rem',
                          fontWeight: 600,
                          backgroundColor: c.deliveryStatus === 'Failed' ? 'rgba(244, 63, 94, 0.2)' : 'rgba(34, 197, 94, 0.2)',
                          color: c.deliveryStatus === 'Failed' ? '#fb7185' : '#4ade80'
                        }}>
                          {c.deliveryStatus || 'Sent'}
                        </span>
                      </td>
                      <td style={{ padding: '0.625rem 0.5rem', maxWidth: '280px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', color: 'var(--text-secondary)' }} title={c.message}>
                        {c.title}: {c.message}
                      </td>
                      <td style={{ padding: '0.625rem 0.5rem', color: 'var(--text-muted)', fontSize: '0.75rem' }}>
                        {new Date(c.sentAt).toLocaleString()}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Modals */}
      {canReschedule && (
        <RescheduleModal
          orderId={order.id}
          trackingNumber={order.trackingNumber}
          isOpen={isRescheduleOpen}
          onClose={() => setIsRescheduleOpen(false)}
          onSuccess={fetchOrder}
        />
      )}

      {canAgentUpdateStatus && (
        <AgentStatusModal
          orderId={order.id}
          trackingNumber={order.trackingNumber}
          currentStatus={order.status}
          isOpen={isStatusUpdateOpen}
          onClose={() => setIsStatusUpdateOpen(false)}
          onSuccess={fetchOrder}
        />
      )}

      {/* Admin Privileged Status Override Modal */}
      {isOverrideOpen && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.75)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem' }}>
          <div className="card" style={{ maxWidth: '480px', width: '100%', display: 'flex', flexDirection: 'column', gap: '1rem', border: '1px solid rgba(245, 158, 11, 0.4)' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <h3 style={{ fontSize: '1rem', fontWeight: 600, color: '#f59e0b', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <AlertTriangle size={18} /> Privileged Admin Status Override
              </h3>
              <button onClick={() => setIsOverrideOpen(false)} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}>&times;</button>
            </div>

            <div style={{ backgroundColor: 'rgba(245, 158, 11, 0.12)', border: '1px solid rgba(245, 158, 11, 0.25)', color: '#fbbf24', padding: '0.75rem', borderRadius: 'var(--radius-md)', fontSize: '0.8125rem' }}>
              <strong>Admin Override:</strong> Directly changes order state from <strong>{order.status}</strong>. An immutable audit record with your Admin user ID and reason will be appended.
            </div>

            {overrideError && (
              <div style={{ backgroundColor: 'rgba(244, 63, 94, 0.15)', color: '#fb7185', padding: '0.5rem 0.75rem', borderRadius: 'var(--radius-md)', fontSize: '0.8125rem' }}>
                {overrideError}
              </div>
            )}

            <form onSubmit={handleExecuteOverride} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
              <div>
                <label className="label">Target Status</label>
                <select
                  value={overrideForm.status}
                  onChange={(e) => setOverrideForm({ ...overrideForm, status: e.target.value })}
                  className="input-control"
                  required
                >
                  <option value="Created">Created</option>
                  <option value="PickedUp">PickedUp</option>
                  <option value="InTransit">InTransit</option>
                  <option value="OutForDelivery">OutForDelivery</option>
                  <option value="Delivered">Delivered</option>
                  <option value="Failed">Failed</option>
                  <option value="Rescheduled">Rescheduled</option>
                </select>
              </div>

              <div>
                <label className="label">Mandatory Override Reason</label>
                <textarea
                  required
                  rows={3}
                  placeholder="State the reason for this administrative status change..."
                  value={overrideForm.reason}
                  onChange={(e) => setOverrideForm({ ...overrideForm, reason: e.target.value })}
                  className="input-control"
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
                <button type="button" onClick={() => setIsOverrideOpen(false)} className="btn btn-secondary">Cancel</button>
                <button type="submit" className="btn btn-primary" style={{ backgroundColor: '#d97706', borderColor: '#d97706' }}>Execute Override</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
