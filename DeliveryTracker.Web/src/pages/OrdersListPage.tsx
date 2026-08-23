import React, { useState, useEffect } from 'react';
import type { OrderSummary } from '../types';
import { useAuth } from '../context/AuthContext';
import { apiClient } from '../api/apiClient';
import { StatusBadge } from '../components/StatusBadge';
import { Search, Filter, Eye, RefreshCw, AlertCircle, ShieldAlert } from 'lucide-react';

interface OrdersListPageProps {
  onSelectOrder: (orderId: number) => void;
  onCreateOrderClick?: () => void;
}

export const OrdersListPage: React.FC<OrdersListPageProps> = ({ onSelectOrder, onCreateOrderClick }) => {
  const { user } = useAuth();
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Search & Filter state
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('ALL');

  const fetchOrders = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await apiClient.get<OrderSummary[]>('/orders');
      setOrders(data);
    } catch (err: any) {
      setError(err.message || 'Failed to fetch orders.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchOrders();
  }, []);

  const handleAutoAssign = async (orderId: number, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await apiClient.post(`/orders/${orderId}/auto-assign`);
      fetchOrders();
    } catch (err: any) {
      alert(err.message || 'Auto-assign failed');
    }
  };

  // Real data filtering
  const filteredOrders = orders.filter((o) => {
    const matchesSearch =
      o.trackingNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
      o.pickupArea.toLowerCase().includes(searchTerm.toLowerCase()) ||
      o.dropArea.toLowerCase().includes(searchTerm.toLowerCase());

    const matchesStatus = statusFilter === 'ALL' || o.status === statusFilter;

    return matchesSearch && matchesStatus;
  });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {/* Search & Filter Header Bar */}
      <div className="card" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flex: 1, minWidth: '280px' }}>
          <div style={{ position: 'relative', flex: 1 }}>
            <Search size={16} color="var(--text-muted)" style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)' }} />
            <input
              type="text"
              className="input-field"
              style={{ paddingLeft: '2.25rem' }}
              placeholder="Search tracking number or area..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Filter size={16} color="var(--text-muted)" />
            <select
              className="input-field"
              style={{ width: '160px' }}
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
            >
              <option value="ALL">All Statuses</option>
              <option value="Created">Created</option>
              <option value="PickedUp">Picked Up</option>
              <option value="InTransit">In Transit</option>
              <option value="OutForDelivery">Out For Delivery</option>
              <option value="Delivered">Delivered</option>
              <option value="Failed">Failed</option>
              <option value="Rescheduled">Rescheduled</option>
            </select>
          </div>
        </div>

        <button onClick={fetchOrders} className="btn btn-secondary btn-sm">
          <RefreshCw size={14} /> Refresh List
        </button>
      </div>

      {error && (
        <div style={{ backgroundColor: 'rgba(244, 63, 94, 0.15)', border: '1px solid rgba(244, 63, 94, 0.3)', color: '#fb7185', padding: '0.875rem 1rem', borderRadius: 'var(--radius-md)', fontSize: '0.875rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <AlertCircle size={18} /> {error}
        </div>
      )}

      {/* Main Operational Orders Table */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        {isLoading ? (
          <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-muted)' }}>Loading orders...</div>
        ) : filteredOrders.length === 0 ? (
          <div style={{ padding: '3rem', textAlign: 'center' }}>
            <ShieldAlert size={36} color="var(--text-muted)" style={{ marginBottom: '0.5rem' }} />
            <h4 style={{ fontSize: '1rem', fontWeight: 600, color: 'var(--text-primary)' }}>No Deliveries Found</h4>
            <p style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)', marginTop: '4px' }}>
              {searchTerm || statusFilter !== 'ALL'
                ? 'No orders match your active search or status filter.'
                : user?.role === 'Customer'
                ? 'You have not created any shipping deliveries yet.'
                : 'No deliveries currently assigned.'}
            </p>
            {user?.role === 'Customer' && onCreateOrderClick && (
              <button onClick={onCreateOrderClick} className="btn btn-primary" style={{ marginTop: '1rem' }}>
                Create First Delivery
              </button>
            )}
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
              <thead>
                <tr style={{ backgroundColor: 'rgba(15, 23, 42, 0.6)', borderBottom: '1px solid var(--border-subtle)', color: 'var(--text-secondary)', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                  <th style={{ padding: '0.875rem 1.25rem' }}>Tracking Number</th>
                  <th style={{ padding: '0.875rem 1.25rem' }}>Route</th>
                  <th style={{ padding: '0.875rem 1.25rem' }}>Assigned Agent</th>
                  <th style={{ padding: '0.875rem 1.25rem' }}>Status</th>
                  <th style={{ padding: '0.875rem 1.25rem' }}>Amount</th>
                  <th style={{ padding: '0.875rem 1.25rem' }}>Created</th>
                  <th style={{ padding: '0.875rem 1.25rem', textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredOrders.map((o) => (
                  <tr
                    key={o.id}
                    onClick={() => onSelectOrder(o.id)}
                    style={{
                      borderBottom: '1px solid var(--border-subtle)',
                      cursor: 'pointer',
                      transition: 'background-color 0.15s ease',
                    }}
                    onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'var(--bg-surface-hover)')}
                    onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                  >
                    <td style={{ padding: '0.875rem 1.25rem' }}>
                      <span className="tracking-mono" style={{ color: 'var(--brand-primary)' }}>
                        {o.trackingNumber}
                      </span>
                    </td>
                    <td style={{ padding: '0.875rem 1.25rem', color: 'var(--text-primary)' }}>
                      {o.pickupArea} &rarr; {o.dropArea}
                    </td>
                    <td style={{ padding: '0.875rem 1.25rem' }}>
                      {o.assignedAgentName ? (
                        <span style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{o.assignedAgentName}</span>
                      ) : (
                        <span style={{ color: 'var(--status-outfordelivery-fg)', fontSize: '0.8125rem' }}>
                          Awaiting Assignment
                        </span>
                      )}
                    </td>
                    <td style={{ padding: '0.875rem 1.25rem' }}>
                      <StatusBadge status={o.status} size="sm" />
                    </td>
                    <td style={{ padding: '0.875rem 1.25rem', fontFamily: 'var(--font-mono)', fontWeight: 600 }}>
                      ₹{o.totalAmount.toFixed(2)}
                    </td>
                    <td style={{ padding: '0.875rem 1.25rem', color: 'var(--text-muted)', fontSize: '0.8125rem' }}>
                      {new Date(o.createdAt).toLocaleDateString()}
                    </td>
                    <td style={{ padding: '0.875rem 1.25rem', textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: '0.5rem', alignItems: 'center' }}>
                        {user?.role === 'Admin' && !o.assignedAgentId && (
                          <button
                            onClick={(e) => handleAutoAssign(o.id, e)}
                            className="btn btn-secondary btn-sm"
                            title="Auto Assign Agent"
                          >
                            Auto Assign
                          </button>
                        )}
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            onSelectOrder(o.id);
                          }}
                          className="btn btn-secondary btn-sm"
                        >
                          <Eye size={14} /> View
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
