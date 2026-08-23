import React, { useState } from 'react';
import type { OrderStatus } from '../types';
import { apiClient } from '../api/apiClient';
import { Truck, X, AlertCircle } from 'lucide-react';

interface AgentStatusModalProps {
  orderId: number;
  trackingNumber: string;
  currentStatus: OrderStatus;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const AgentStatusModal: React.FC<AgentStatusModalProps> = ({
  orderId,
  trackingNumber,
  currentStatus,
  isOpen,
  onClose,
  onSuccess,
}) => {
  const getNextAllowedStatuses = (status: OrderStatus): OrderStatus[] => {
    switch (status) {
      case 'Created':
        return ['PickedUp'];
      case 'PickedUp':
        return ['InTransit'];
      case 'InTransit':
        return ['OutForDelivery'];
      case 'OutForDelivery':
        return ['Delivered', 'Failed'];
      case 'Rescheduled':
        return ['OutForDelivery'];
      default:
        return [];
    }
  };

  const allowedStatuses = getNextAllowedStatuses(currentStatus);
  const [targetStatus, setTargetStatus] = useState<OrderStatus>(allowedStatuses[0] || 'PickedUp');
  const [notes, setNotes] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (targetStatus === 'Failed' && !notes.trim()) {
      setError('Failure notes are required when marking an attempt as Failed.');
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await apiClient.patch(`/orders/${orderId}/status`, {
        status: targetStatus,
        notes: notes.trim() || `Status updated to ${targetStatus}`,
      });
      onSuccess();
      onClose();
    } catch (err: any) {
      setError(err.message || 'Failed to update order status');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(15, 23, 42, 0.75)',
        backdropFilter: 'blur(2px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
        padding: '1rem',
      }}
    >
      <div
        style={{
          backgroundColor: 'var(--bg-surface)',
          border: '1px solid var(--border-strong)',
          borderRadius: 'var(--radius-lg)',
          width: '100%',
          maxWidth: '480px',
          padding: '1.5rem',
          boxShadow: 'var(--shadow-md)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem', borderBottom: '1px solid var(--border-subtle)', paddingBottom: '0.75rem' }}>
          <h3 style={{ fontSize: '1rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Truck size={18} color="var(--brand-primary)" /> Update Delivery Status
          </h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}>
            <X size={18} />
          </button>
        </div>

        <p style={{ fontSize: '0.875rem', color: 'var(--text-secondary)', marginBottom: '1.25rem' }}>
          Order <strong className="tracking-mono" style={{ color: 'var(--text-primary)' }}>{trackingNumber}</strong> is currently <span style={{ fontWeight: 600, color: 'var(--brand-primary)' }}>{currentStatus}</span>.
        </p>

        {error && (
          <div style={{ backgroundColor: 'rgba(244, 63, 94, 0.15)', border: '1px solid rgba(244, 63, 94, 0.3)', color: '#fb7185', padding: '0.75rem', borderRadius: 'var(--radius-sm)', fontSize: '0.875rem', marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <AlertCircle size={16} /> {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div style={{ marginBottom: '1rem' }}>
            <label className="label">Target Next Status</label>
            <select
              className="input-field"
              value={targetStatus}
              onChange={(e) => setTargetStatus(e.target.value as OrderStatus)}
            >
              {allowedStatuses.map((st) => (
                <option key={st} value={st}>
                  {st === 'Failed' ? 'Failed Attempt' : st}
                </option>
              ))}
            </select>
          </div>

          <div style={{ marginBottom: '1.5rem' }}>
            <label className="label">
              Status Notes {targetStatus === 'Failed' && <span style={{ color: '#fb7185' }}>* (Required)</span>}
            </label>
            <textarea
              className="input-field"
              rows={3}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder={targetStatus === 'Failed' ? 'e.g. Customer unavailable at address' : 'Optional update notes'}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
            <button type="button" onClick={onClose} className="btn btn-secondary" disabled={isSubmitting}>
              Cancel
            </button>
            <button
              type="submit"
              className={`btn ${targetStatus === 'Failed' ? 'btn-danger' : 'btn-primary'}`}
              disabled={isSubmitting}
            >
              {isSubmitting ? 'Updating...' : `Set Status to ${targetStatus}`}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
