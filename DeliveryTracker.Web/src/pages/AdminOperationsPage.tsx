import React, { useState, useEffect } from 'react';
import type { OrderSummary } from '../types';
import { apiClient } from '../api/apiClient';
import { StatusBadge } from '../components/StatusBadge';
import { Shield, Package, Truck, AlertTriangle, CheckCircle2, Search, Filter, Eye, RefreshCw, AlertCircle } from 'lucide-react';

interface AdminOperationsPageProps {
  onSelectOrder: (orderId: number) => void;
}

export const AdminOperationsPage: React.FC<AdminOperationsPageProps> = ({ onSelectOrder }) => {
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('ALL');

  const fetchOrders = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await apiClient.get<OrderSummary[]>('/orders');
      setOrders(data);
    } catch (err: any) {
      setError(err.message || 'Failed to fetch operations data.');
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
      alert(err.message || 'Auto-assignment failed');
    }
  };

  // Derive metrics strictly from actual backend order data
  const totalOrders = orders.length;
  const activeOrders = orders.filter((o) => ['Created', 'PickedUp', 'InTransit', 'OutForDelivery', 'Rescheduled'].includes(o.status)).length;
  const failedOrders = orders.filter((o) => o.status === 'Failed').length;
  const deliveredOrders = orders.filter((o) => o.status === 'Delivered').length;

  const filteredOrders = orders.filter((o) => {
    const matchesSearch =
      o.trackingNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
      o.pickupArea.toLowerCase().includes(searchTerm.toLowerCase()) ||
      o.dropArea.toLowerCase().includes(searchTerm.toLowerCase());

    const matchesStatus = statusFilter === 'ALL' || o.status === statusFilter;

    return matchesSearch && matchesStatus;
  });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Intro Banner */}
      <div className="card" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h3 style={{ fontSize: '1rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Shield size={18} color="var(--brand-primary)" /> Operations Management Console
          </h3>
          <p style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
            System-wide order monitoring, agent assignment dispatching, and recovery oversight.
          </p>
        </div>
        <button onClick={fetchOrders} className="btn btn-secondary btn-sm">
          <RefreshCw size={14} /> Refresh Console
        </button>
      </div>

      {error && (
        <div style={{ backgroundColor: 'rgba(244, 63, 94, 0.15)', border: '1px solid rgba(244, 63, 94, 0.3)', color: '#fb7185', padding: '0.875rem 1rem', borderRadius: 'var(--radius-md)', fontSize: '0.875rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <AlertCircle size={18} /> {error}
        </div>
      )}

      {/* Actual Data Metrics Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
        <div className="card" style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <div style={{ padding: '0.75rem', borderRadius: 'var(--radius-md)', backgroundColor: 'rgba(148, 163, 184, 0.15)', color: '#cbd5e1' }}>
            <Package size={22} />
          </div>
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>Total Orders</span>
            <div style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>{totalOrders}</div>
          </div>
        </div>

        <div className="card" style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <div style={{ padding: '0.75rem', borderRadius: 'var(--radius-md)', backgroundColor: 'rgba(56, 189, 248, 0.15)', color: '#38bdf8' }}>
            <Truck size={22} />
          </div>
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>Active Deliveries</span>
            <div style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--brand-primary)', fontFamily: 'var(--font-mono)' }}>{activeOrders}</div>
          </div>
        </div>

        <div className="card" style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <div style={{ padding: '0.75rem', borderRadius: 'var(--radius-md)', backgroundColor: 'rgba(244, 63, 94, 0.15)', color: '#fb7185' }}>
            <AlertTriangle size={22} />
          </div>
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>Failed Attempts</span>
            <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#fb7185', fontFamily: 'var(--font-mono)' }}>{failedOrders}</div>
          </div>
        </div>

        <div className="card" style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <div style={{ padding: '0.75rem', borderRadius: 'var(--radius-md)', backgroundColor: 'rgba(16, 185, 129, 0.15)', color: '#34d399' }}>
            <CheckCircle2 size={22} />
          </div>
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase', fontWeight: 600 }}>Delivered</span>
            <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#34d399', fontFamily: 'var(--font-mono)' }}>{deliveredOrders}</div>
          </div>
        </div>
      </div>

      {/* Global Orders Operational Table */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        <div style={{ padding: '1rem 1.25rem', borderBottom: '1px solid var(--border-subtle)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flex: 1, minWidth: '280px' }}>
            <div style={{ position: 'relative', flex: 1 }}>
              <Search size={16} color="var(--text-muted)" style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)' }} />
              <input
                type="text"
                className="input-field"
                style={{ paddingLeft: '2.25rem' }}
                placeholder="Search tracking or area..."
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
        </div>

        {isLoading ? (
          <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-muted)' }}>Loading operations data...</div>
        ) : filteredOrders.length === 0 ? (
          <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-muted)' }}>No operations orders found.</div>
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
                        {!o.assignedAgentId && (
                          <button
                            onClick={(e) => handleAutoAssign(o.id, e)}
                            className="btn btn-secondary btn-sm"
                            title="Trigger Auto Assign Algorithm"
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
                          <Eye size={14} /> Inspect
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
