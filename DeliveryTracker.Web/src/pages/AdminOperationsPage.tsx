import React, { useState, useEffect } from 'react';
import type { OrderSummary, Zone, Area, AdminCustomer, AdminAgent, OrderStatus, OrderType, PaymentType } from '../types';
import { apiClient } from '../api/apiClient';
import { StatusBadge } from '../components/StatusBadge';
import { AdminConfigurationManager } from '../components/AdminConfigurationManager';
import { 
  Shield, Package, Truck, AlertTriangle, CheckCircle2, Search, Filter, 
  Eye, RefreshCw, AlertCircle, Sliders, Plus, UserCheck, AlertOctagon, X
} from 'lucide-react';

interface AdminOperationsPageProps {
  onSelectOrder: (orderId: number) => void;
}

export const AdminOperationsPage: React.FC<AdminOperationsPageProps> = ({ onSelectOrder }) => {
  const [activeSection, setActiveSection] = useState<'dispatch' | 'configuration'>('dispatch');
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [zones, setZones] = useState<Zone[]>([]);
  const [areas, setAreas] = useState<Area[]>([]);
  const [customers, setCustomers] = useState<AdminCustomer[]>([]);
  const [agents, setAgents] = useState<AdminAgent[]>([]);

  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Filters State
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('ALL');
  const [zoneFilter, setZoneFilter] = useState<string>('ALL');
  const [agentFilter, setAgentFilter] = useState<string>('ALL');

  // Modals
  const [createOrderModalOpen, setCreateOrderModalOpen] = useState(false);
  const [orderForm, setOrderForm] = useState({
    customerId: 0,
    pickupAreaId: 0,
    dropAreaId: 0,
    pickupAddress: '',
    dropAddress: '',
    length: 20,
    breadth: 20,
    height: 20,
    actualWeight: 2.0,
    orderType: 'B2C' as OrderType,
    paymentType: 'Prepaid' as PaymentType,
  });

  const [assignModalOrder, setAssignModalOrder] = useState<OrderSummary | null>(null);
  const [selectedAgentId, setSelectedAgentId] = useState<number>(0);

  const [overrideModalOrder, setOverrideModalOrder] = useState<OrderSummary | null>(null);
  const [overrideForm, setOverrideForm] = useState<{ status: OrderStatus; reason: string }>({
    status: 'Delivered',
    reason: '',
  });

  const showSuccess = (msg: string) => {
    setSuccessMsg(msg);
    setTimeout(() => setSuccessMsg(null), 4000);
  };

  const fetchOperationsData = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const [ordersData, zonesData, areasData, customersData, agentsData] = await Promise.all([
        apiClient.get<OrderSummary[]>('/orders'),
        apiClient.get<Zone[]>('/zones'),
        apiClient.get<Area[]>('/areas'),
        apiClient.get<AdminCustomer[]>('/orders/customers'),
        apiClient.get<AdminAgent[]>('/agents'),
      ]);
      setOrders(ordersData);
      setZones(zonesData);
      setAreas(areasData);
      setCustomers(customersData);
      setAgents(agentsData);

      if (customersData.length > 0 && orderForm.customerId === 0) {
        setOrderForm((prev) => ({
          ...prev,
          customerId: customersData[0].id,
          pickupAreaId: areasData[0]?.id || 0,
          dropAreaId: areasData[1]?.id || areasData[0]?.id || 0,
        }));
      }
    } catch (err: any) {
      setError(err.message || 'Failed to fetch operations data.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchOperationsData();
  }, []);

  // --- Handlers ---
  const handleAutoAssign = async (orderId: number, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setError(null);
    try {
      const res = await apiClient.post<any>(`/orders/${orderId}/auto-assign`);
      showSuccess(`Auto-assigned to ${res.assignedAgent.name}`);
      fetchOperationsData();
    } catch (err: any) {
      setError(err.message || 'Auto-assignment failed');
    }
  };

  const handleOpenManualAssign = (order: OrderSummary, e: React.MouseEvent) => {
    e.stopPropagation();
    setAssignModalOrder(order);
    const available = agents.filter((a) => a.isAvailable);
    setSelectedAgentId(available[0]?.id || agents[0]?.id || 0);
  };

  const handleSaveManualAssign = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assignModalOrder || !selectedAgentId) return;
    setError(null);
    try {
      const res = await apiClient.post<any>(`/orders/${assignModalOrder.id}/assign`, {
        agentId: selectedAgentId,
      });
      showSuccess(res.message || 'Agent assigned successfully.');
      setAssignModalOrder(null);
      fetchOperationsData();
    } catch (err: any) {
      setError(err.message || 'Manual assignment failed.');
    }
  };

  const handleOpenStatusOverride = (order: OrderSummary, e: React.MouseEvent) => {
    e.stopPropagation();
    setOverrideModalOrder(order);
    setOverrideForm({ status: order.status, reason: '' });
  };

  const handleSaveStatusOverride = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!overrideModalOrder || !overrideForm.reason.trim()) return;
    setError(null);
    try {
      await apiClient.post(`/orders/${overrideModalOrder.id}/override-status`, overrideForm);
      showSuccess(`Order ${overrideModalOrder.trackingNumber} status overridden to ${overrideForm.status}.`);
      setOverrideModalOrder(null);
      fetchOperationsData();
    } catch (err: any) {
      setError(err.message || 'Status override failed.');
    }
  };

  const handleCreateOrderOnBehalf = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const newOrder = await apiClient.post<any>('/orders', orderForm);
      showSuccess(`Order ${newOrder.trackingNumber} created on behalf of customer!`);
      setCreateOrderModalOpen(false);
      fetchOperationsData();
    } catch (err: any) {
      setError(err.message || 'Order creation failed.');
    }
  };

  // Metrics
  const totalOrders = orders.length;
  const activeOrders = orders.filter((o) => ['Created', 'PickedUp', 'InTransit', 'OutForDelivery', 'Rescheduled'].includes(o.status)).length;
  const failedOrders = orders.filter((o) => o.status === 'Failed').length;
  const deliveredOrders = orders.filter((o) => o.status === 'Delivered').length;

  // Filtered Orders
  const filteredOrders = orders.filter((o) => {
    const matchesSearch =
      o.trackingNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
      o.pickupArea.toLowerCase().includes(searchTerm.toLowerCase()) ||
      o.dropArea.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (o.customerName && o.customerName.toLowerCase().includes(searchTerm.toLowerCase()));

    const matchesStatus = statusFilter === 'ALL' || o.status === statusFilter;

    const matchesZone =
      zoneFilter === 'ALL' ||
      o.pickupZoneId?.toString() === zoneFilter ||
      o.dropZoneId?.toString() === zoneFilter;

    const matchesAgent =
      agentFilter === 'ALL'
        ? true
        : agentFilter === 'UNASSIGNED'
        ? !o.assignedAgentId
        : o.assignedAgentId?.toString() === agentFilter;

    return matchesSearch && matchesStatus && matchesZone && matchesAgent;
  });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Top Banner with Navigation */}
      <div className="card" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h3 style={{ fontSize: '1rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Shield size={18} color="var(--brand-primary)" /> Operations Management Console
          </h3>
          <p style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
            System-wide order monitoring, manual/auto agent dispatch, privileged override, and system configuration.
          </p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <div style={{ display: 'flex', gap: '0.25rem', backgroundColor: 'var(--bg-app)', padding: '0.25rem', borderRadius: 'var(--radius-md)', border: '1px solid var(--border-subtle)' }}>
            <button
              onClick={() => setActiveSection('dispatch')}
              style={{
                padding: '0.35rem 0.75rem',
                fontSize: '0.8125rem',
                fontWeight: 600,
                borderRadius: 'var(--radius-sm)',
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '0.35rem',
                backgroundColor: activeSection === 'dispatch' ? 'var(--brand-primary)' : 'transparent',
                color: activeSection === 'dispatch' ? '#0f172a' : 'var(--text-secondary)',
                transition: 'all 0.15s ease',
              }}
            >
              <Truck size={14} /> Dispatch & Orders
            </button>

            <button
              onClick={() => setActiveSection('configuration')}
              style={{
                padding: '0.35rem 0.75rem',
                fontSize: '0.8125rem',
                fontWeight: 600,
                borderRadius: 'var(--radius-sm)',
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '0.35rem',
                backgroundColor: activeSection === 'configuration' ? 'var(--brand-primary)' : 'transparent',
                color: activeSection === 'configuration' ? '#0f172a' : 'var(--text-secondary)',
                transition: 'all 0.15s ease',
              }}
            >
              <Sliders size={14} /> System Configuration
            </button>
          </div>

          {activeSection === 'dispatch' && (
            <>
              <button onClick={() => setCreateOrderModalOpen(true)} className="btn btn-primary btn-sm">
                <Plus size={14} /> Book for Customer
              </button>
              <button onClick={fetchOperationsData} className="btn btn-secondary btn-sm" disabled={isLoading}>
                <RefreshCw size={14} className={isLoading ? 'animate-spin' : ''} /> Refresh
              </button>
            </>
          )}
        </div>
      </div>

      {/* Configuration View */}
      {activeSection === 'configuration' ? (
        <AdminConfigurationManager />
      ) : (
        <>
          {/* Messages */}
          {error && (
            <div style={{ backgroundColor: 'rgba(244, 63, 94, 0.15)', border: '1px solid rgba(244, 63, 94, 0.3)', color: '#fb7185', padding: '0.875rem 1rem', borderRadius: 'var(--radius-md)', fontSize: '0.875rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <AlertCircle size={18} /> {error}
            </div>
          )}

          {successMsg && (
            <div style={{ backgroundColor: 'rgba(16, 185, 129, 0.15)', border: '1px solid rgba(16, 185, 129, 0.3)', color: '#34d399', padding: '0.875rem 1rem', borderRadius: 'var(--radius-md)', fontSize: '0.875rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <CheckCircle2 size={18} /> {successMsg}
            </div>
          )}

          {/* Metrics Grid */}
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

          {/* Multi-Dimensional Filter Bar */}
          <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '0.75rem' }}>
              {/* Search */}
              <div style={{ position: 'relative' }}>
                <Search size={16} style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
                <input
                  type="text"
                  placeholder="Search tracking, customer, area..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="input-control"
                  style={{ paddingLeft: '2.25rem' }}
                />
              </div>

              {/* Status Filter */}
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Filter size={15} color="var(--text-muted)" />
                <select
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value)}
                  className="input-control"
                >
                  <option value="ALL">All Statuses</option>
                  <option value="Created">Created</option>
                  <option value="PickedUp">PickedUp</option>
                  <option value="InTransit">InTransit</option>
                  <option value="OutForDelivery">OutForDelivery</option>
                  <option value="Delivered">Delivered</option>
                  <option value="Failed">Failed</option>
                  <option value="Rescheduled">Rescheduled</option>
                </select>
              </div>

              {/* Zone Filter */}
              <div>
                <select
                  value={zoneFilter}
                  onChange={(e) => setZoneFilter(e.target.value)}
                  className="input-control"
                >
                  <option value="ALL">All Zones</option>
                  {zones.map((z) => (
                    <option key={z.id} value={z.id.toString()}>{z.name}</option>
                  ))}
                </select>
              </div>

              {/* Agent Filter */}
              <div>
                <select
                  value={agentFilter}
                  onChange={(e) => setAgentFilter(e.target.value)}
                  className="input-control"
                >
                  <option value="ALL">All Agents</option>
                  <option value="UNASSIGNED">Unassigned Only</option>
                  {agents.map((a) => (
                    <option key={a.id} value={a.id.toString()}>{a.name} ({a.zoneName})</option>
                  ))}
                </select>
              </div>
            </div>
          </div>

          {/* Orders Table */}
          <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
            <div style={{ padding: '1rem 1.25rem', borderBottom: '1px solid var(--border-subtle)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3 style={{ fontSize: '0.9375rem', fontWeight: 600 }}>
                Operations Dispatch List ({filteredOrders.length})
              </h3>
            </div>

            {isLoading ? (
              <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
                <RefreshCw size={24} className="animate-spin" style={{ margin: '0 auto 0.75rem' }} />
                <p>Loading operations data...</p>
              </div>
            ) : filteredOrders.length === 0 ? (
              <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
                <Package size={36} style={{ margin: '0 auto 0.75rem', opacity: 0.5 }} />
                <p>No orders match the selected filters.</p>
              </div>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table className="table">
                  <thead>
                    <tr>
                      <th>Tracking #</th>
                      <th>Customer</th>
                      <th>Route (Pickup $\rightarrow$ Drop)</th>
                      <th>Amount</th>
                      <th>Status</th>
                      <th>Assigned Agent</th>
                      <th style={{ textAlign: 'right' }}>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredOrders.map((o) => (
                      <tr
                        key={o.id}
                        onClick={() => onSelectOrder(o.id)}
                        style={{ cursor: 'pointer' }}
                      >
                        <td style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--brand-primary)' }}>
                          {o.trackingNumber}
                        </td>
                        <td>
                          <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{o.customerName || `Customer #${o.customerId}`}</span>
                        </td>
                        <td>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8125rem' }}>
                            <span>{o.pickupArea}</span>
                            <span style={{ color: 'var(--text-muted)' }}>$\rightarrow$</span>
                            <span>{o.dropArea}</span>
                          </div>
                        </td>
                        <td style={{ fontFamily: 'var(--font-mono)', fontWeight: 600 }}>
                          ₹{o.totalAmount.toFixed(2)}
                        </td>
                        <td>
                          <StatusBadge status={o.status} />
                        </td>
                        <td>
                          {o.assignedAgentName ? (
                            <span className="badge badge-info" style={{ fontSize: '0.75rem' }}>
                              {o.assignedAgentName}
                            </span>
                          ) : (
                            <span className="badge badge-neutral" style={{ fontSize: '0.75rem' }}>
                              Unassigned
                            </span>
                          )}
                        </td>
                        <td style={{ textAlign: 'right' }} onClick={(e) => e.stopPropagation()}>
                          <div style={{ display: 'inline-flex', gap: '0.35rem' }}>
                            {/* Auto Assign */}
                            {!o.assignedAgentId && ['Created', 'Rescheduled'].includes(o.status) && (
                              <button
                                onClick={() => handleAutoAssign(o.id)}
                                className="btn btn-secondary btn-sm"
                                title="Intelligent Haversine Auto-Assign"
                              >
                                Auto Assign
                              </button>
                            )}

                            {/* Manual Assign */}
                            {['Created', 'Rescheduled'].includes(o.status) && (
                              <button
                                onClick={(e) => handleOpenManualAssign(o, e)}
                                className="btn btn-secondary btn-sm"
                                title="Manually Pick Agent"
                              >
                                <UserCheck size={13} /> Assign
                              </button>
                            )}

                            {/* Admin Status Override */}
                            <button
                              onClick={(e) => handleOpenStatusOverride(o, e)}
                              className="btn btn-secondary btn-sm"
                              style={{ color: '#f59e0b' }}
                              title="Privileged Status Override"
                            >
                              <AlertOctagon size={13} /> Override
                            </button>

                            {/* Inspect */}
                            <button
                              onClick={() => onSelectOrder(o.id)}
                              className="btn btn-secondary btn-sm"
                              title="View Full Detail"
                            >
                              <Eye size={13} /> Inspect
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
        </>
      )}

      {/* MODAL: Book Order on behalf of Customer */}
      {createOrderModalOpen && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.75)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem' }}>
          <div className="card" style={{ maxWidth: '560px', width: '100%', maxHeight: '90vh', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <h3 style={{ fontSize: '1rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Plus size={18} color="var(--brand-primary)" /> Create Order on Behalf of Customer
              </h3>
              <button onClick={() => setCreateOrderModalOpen(false)} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}><X size={18} /></button>
            </div>

            <form onSubmit={handleCreateOrderOnBehalf} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
              <div>
                <label className="label">Select Customer</label>
                <select
                  value={orderForm.customerId}
                  onChange={(e) => setOrderForm({ ...orderForm, customerId: parseInt(e.target.value) })}
                  className="input-control"
                  required
                >
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.fullName} ({c.email})
                    </option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div>
                  <label className="label">Pickup Area</label>
                  <select
                    value={orderForm.pickupAreaId}
                    onChange={(e) => setOrderForm({ ...orderForm, pickupAreaId: parseInt(e.target.value) })}
                    className="input-control"
                    required
                  >
                    {areas.map((a) => (
                      <option key={a.id} value={a.id}>{a.name} ({a.zone?.name || `Zone ${a.zoneId}`})</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="label">Drop Area</label>
                  <select
                    value={orderForm.dropAreaId}
                    onChange={(e) => setOrderForm({ ...orderForm, dropAreaId: parseInt(e.target.value) })}
                    className="input-control"
                    required
                  >
                    {areas.map((a) => (
                      <option key={a.id} value={a.id}>{a.name} ({a.zone?.name || `Zone ${a.zoneId}`})</option>
                    ))}
                  </select>
                </div>
              </div>

              <div>
                <label className="label">Pickup Address</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. 101 Marine Drive"
                  value={orderForm.pickupAddress}
                  onChange={(e) => setOrderForm({ ...orderForm, pickupAddress: e.target.value })}
                  className="input-control"
                />
              </div>

              <div>
                <label className="label">Drop Address</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. 404 Lokhandwala Complex"
                  value={orderForm.dropAddress}
                  onChange={(e) => setOrderForm({ ...orderForm, dropAddress: e.target.value })}
                  className="input-control"
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '0.5rem' }}>
                <div>
                  <label className="label">Length (cm)</label>
                  <input
                    type="number"
                    min="1"
                    required
                    value={orderForm.length}
                    onChange={(e) => setOrderForm({ ...orderForm, length: parseFloat(e.target.value) || 0 })}
                    className="input-control"
                  />
                </div>
                <div>
                  <label className="label">Breadth (cm)</label>
                  <input
                    type="number"
                    min="1"
                    required
                    value={orderForm.breadth}
                    onChange={(e) => setOrderForm({ ...orderForm, breadth: parseFloat(e.target.value) || 0 })}
                    className="input-control"
                  />
                </div>
                <div>
                  <label className="label">Height (cm)</label>
                  <input
                    type="number"
                    min="1"
                    required
                    value={orderForm.height}
                    onChange={(e) => setOrderForm({ ...orderForm, height: parseFloat(e.target.value) || 0 })}
                    className="input-control"
                  />
                </div>
                <div>
                  <label className="label">Weight (kg)</label>
                  <input
                    type="number"
                    step="0.1"
                    min="0.1"
                    required
                    value={orderForm.actualWeight}
                    onChange={(e) => setOrderForm({ ...orderForm, actualWeight: parseFloat(e.target.value) || 0 })}
                    className="input-control"
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div>
                  <label className="label">Order Type</label>
                  <select
                    value={orderForm.orderType}
                    onChange={(e) => setOrderForm({ ...orderForm, orderType: e.target.value as OrderType })}
                    className="input-control"
                  >
                    <option value="B2C">B2C (Retail)</option>
                    <option value="B2B">B2B (Commercial)</option>
                  </select>
                </div>

                <div>
                  <label className="label">Payment Type</label>
                  <select
                    value={orderForm.paymentType}
                    onChange={(e) => setOrderForm({ ...orderForm, paymentType: e.target.value as PaymentType })}
                    className="input-control"
                  >
                    <option value="Prepaid">Prepaid</option>
                    <option value="COD">Cash on Delivery (COD)</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
                <button type="button" onClick={() => setCreateOrderModalOpen(false)} className="btn btn-secondary">Cancel</button>
                <button type="submit" className="btn btn-primary">Book Order</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: Manual Agent Assignment */}
      {assignModalOrder && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.75)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem' }}>
          <div className="card" style={{ maxWidth: '440px', width: '100%', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <h3 style={{ fontSize: '1rem', fontWeight: 600 }}>Manually Assign Delivery Agent</h3>
              <button onClick={() => setAssignModalOrder(null)} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}><X size={18} /></button>
            </div>

            <p style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)' }}>
              Assigning order <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--brand-primary)', fontWeight: 600 }}>{assignModalOrder.trackingNumber}</span> ({assignModalOrder.pickupArea} $\rightarrow$ {assignModalOrder.dropArea}).
            </p>

            <form onSubmit={handleSaveManualAssign} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
              <div>
                <label className="label">Select Agent</label>
                <select
                  value={selectedAgentId}
                  onChange={(e) => setSelectedAgentId(parseInt(e.target.value))}
                  className="input-control"
                  required
                >
                  {agents.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name} — {a.zoneName} {a.isAvailable ? '(Available)' : '(Busy / Assigned)'}
                    </option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
                <button type="button" onClick={() => setAssignModalOrder(null)} className="btn btn-secondary">Cancel</button>
                <button type="submit" className="btn btn-primary">Confirm Assignment</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: Admin Privileged Status Override */}
      {overrideModalOrder && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.75)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem' }}>
          <div className="card" style={{ maxWidth: '480px', width: '100%', display: 'flex', flexDirection: 'column', gap: '1rem', border: '1px solid rgba(245, 158, 11, 0.4)' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <h3 style={{ fontSize: '1rem', fontWeight: 600, color: '#f59e0b', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <AlertOctagon size={18} /> Privileged Status Override
              </h3>
              <button onClick={() => setOverrideModalOrder(null)} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}><X size={18} /></button>
            </div>

            <div style={{ backgroundColor: 'rgba(245, 158, 11, 0.12)', border: '1px solid rgba(245, 158, 11, 0.25)', color: '#fbbf24', padding: '0.75rem', borderRadius: 'var(--radius-md)', fontSize: '0.8125rem' }}>
              <strong>Admin Privilege:</strong> You are overriding order <span style={{ fontFamily: 'var(--font-mono)' }}>{overrideModalOrder.trackingNumber}</span> from <strong>{overrideModalOrder.status}</strong>. This override will be immutably recorded in the audit trail with your Administrator credentials.
            </div>

            <form onSubmit={handleSaveStatusOverride} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
              <div>
                <label className="label">Target Status</label>
                <select
                  value={overrideForm.status}
                  onChange={(e) => setOverrideForm({ ...overrideForm, status: e.target.value as OrderStatus })}
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
                  placeholder="Explain why this privileged status override is necessary..."
                  value={overrideForm.reason}
                  onChange={(e) => setOverrideForm({ ...overrideForm, reason: e.target.value })}
                  className="input-control"
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
                <button type="button" onClick={() => setOverrideModalOrder(null)} className="btn btn-secondary">Cancel</button>
                <button type="submit" className="btn btn-primary" style={{ backgroundColor: '#d97706', borderColor: '#d97706' }}>Execute Override</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
