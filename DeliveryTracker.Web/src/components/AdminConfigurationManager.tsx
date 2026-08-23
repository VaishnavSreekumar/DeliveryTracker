import React, { useState, useEffect } from 'react';
import { apiClient } from '../api/apiClient';
import type { Zone, Area, RateCard } from '../types';
import { MapPin, Layers, IndianRupee, Plus, Edit2, Trash2, Check, X, AlertCircle, RefreshCw } from 'lucide-react';

export const AdminConfigurationManager: React.FC = () => {
  const [activeSubTab, setActiveSubTab] = useState<'ratecards' | 'zones' | 'areas'>('ratecards');
  
  // Data States
  const [rateCards, setRateCards] = useState<RateCard[]>([]);
  const [zones, setZones] = useState<Zone[]>([]);
  const [areas, setAreas] = useState<Area[]>([]);
  
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Modals / Edit States
  const [editingRateCard, setEditingRateCard] = useState<RateCard | null>(null);
  const [rateCardForm, setRateCardForm] = useState({ intraZoneRatePerKg: 0, interZoneRatePerKg: 0, codSurcharge: 0 });

  const [zoneModalOpen, setZoneModalOpen] = useState(false);
  const [editingZone, setEditingZone] = useState<Zone | null>(null);
  const [zoneForm, setZoneForm] = useState({ name: '', code: '' });

  const [areaModalOpen, setAreaModalOpen] = useState(false);
  const [editingArea, setEditingArea] = useState<Area | null>(null);
  const [areaForm, setAreaForm] = useState({ name: '', code: '', zoneId: 0 });

  const [areaSearch, setAreaSearch] = useState('');

  const loadData = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const [rcData, zData, aData] = await Promise.all([
        apiClient.get<RateCard[]>('/ratecards'),
        apiClient.get<Zone[]>('/zones'),
        apiClient.get<Area[]>('/areas'),
      ]);
      setRateCards(rcData);
      setZones(zData);
      setAreas(aData);
    } catch (err: any) {
      setError(err.message || 'Failed to load configuration data.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const showSuccess = (msg: string) => {
    setSuccessMsg(msg);
    setTimeout(() => setSuccessMsg(null), 4000);
  };

  // --- Rate Card Handlers ---
  const handleOpenEditRateCard = (rc: RateCard) => {
    setEditingRateCard(rc);
    setRateCardForm({
      intraZoneRatePerKg: rc.intraZoneRatePerKg,
      interZoneRatePerKg: rc.interZoneRatePerKg,
      codSurcharge: rc.codSurcharge,
    });
  };

  const handleSaveRateCard = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingRateCard) return;
    setError(null);
    try {
      const updated = await apiClient.put<RateCard>(`/ratecards/${editingRateCard.id}`, rateCardForm);
      setRateCards((prev) => prev.map((rc) => (rc.id === updated.id ? updated : rc)));
      setEditingRateCard(null);
      showSuccess(`Rate Card for ${editingRateCard.orderType} updated successfully.`);
    } catch (err: any) {
      setError(err.message || 'Failed to update Rate Card.');
    }
  };

  // --- Zone Handlers ---
  const handleOpenCreateZone = () => {
    setEditingZone(null);
    setZoneForm({ name: '', code: '' });
    setZoneModalOpen(true);
  };

  const handleOpenEditZone = (z: Zone) => {
    setEditingZone(z);
    setZoneForm({ name: z.name, code: z.code });
    setZoneModalOpen(true);
  };

  const handleSaveZone = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      if (editingZone) {
        const updated = await apiClient.put<Zone>(`/zones/${editingZone.id}`, zoneForm);
        setZones((prev) => prev.map((z) => (z.id === updated.id ? updated : z)));
        showSuccess(`Zone '${updated.name}' updated successfully.`);
      } else {
        const created = await apiClient.post<Zone>('/zones', zoneForm);
        setZones((prev) => [...prev, created]);
        showSuccess(`Zone '${created.name}' created successfully.`);
      }
      setZoneModalOpen(false);
      loadData();
    } catch (err: any) {
      setError(err.message || 'Failed to save Zone.');
    }
  };

  const handleDeleteZone = async (z: Zone) => {
    if (!window.confirm(`Are you sure you want to delete zone '${z.name}'?`)) return;
    setError(null);
    try {
      await apiClient.delete(`/zones/${z.id}`);
      setZones((prev) => prev.filter((item) => item.id !== z.id));
      showSuccess(`Zone '${z.name}' deleted.`);
      loadData();
    } catch (err: any) {
      setError(err.message || 'Failed to delete Zone.');
    }
  };

  // --- Area Handlers ---
  const handleOpenCreateArea = () => {
    setEditingArea(null);
    setAreaForm({ name: '', code: '', zoneId: zones[0]?.id || 0 });
    setAreaModalOpen(true);
  };

  const handleOpenEditArea = (a: Area) => {
    setEditingArea(a);
    setAreaForm({ name: a.name, code: a.code, zoneId: a.zoneId });
    setAreaModalOpen(true);
  };

  const handleSaveArea = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      if (editingArea) {
        const updated = await apiClient.put<Area>(`/areas/${editingArea.id}`, areaForm);
        setAreas((prev) => prev.map((a) => (a.id === updated.id ? updated : a)));
        showSuccess(`Area '${updated.name}' updated.`);
      } else {
        const created = await apiClient.post<Area>('/areas', areaForm);
        setAreas((prev) => [...prev, created]);
        showSuccess(`Area '${created.name}' created.`);
      }
      setAreaModalOpen(false);
      loadData();
    } catch (err: any) {
      setError(err.message || 'Failed to save Area.');
    }
  };

  const handleDeleteArea = async (a: Area) => {
    if (!window.confirm(`Are you sure you want to delete area '${a.name}'?`)) return;
    setError(null);
    try {
      await apiClient.delete(`/areas/${a.id}`);
      setAreas((prev) => prev.filter((item) => item.id !== a.id));
      showSuccess(`Area '${a.name}' deleted.`);
      loadData();
    } catch (err: any) {
      setError(err.message || 'Failed to delete Area.');
    }
  };

  const filteredAreas = areas.filter(
    (a) =>
      a.name.toLowerCase().includes(areaSearch.toLowerCase()) ||
      a.code.toLowerCase().includes(areaSearch.toLowerCase()) ||
      a.zone?.name.toLowerCase().includes(areaSearch.toLowerCase())
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {/* Configuration Header & Tab Selector */}
      <div className="card" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <div style={{ display: 'flex', gap: '0.35rem', backgroundColor: 'var(--bg-app)', padding: '0.25rem', borderRadius: 'var(--radius-md)', border: '1px solid var(--border-subtle)' }}>
            <button
              type="button"
              onClick={() => setActiveSubTab('ratecards')}
              style={{
                padding: '0.4rem 0.85rem',
                fontSize: '0.8125rem',
                fontWeight: 600,
                borderRadius: 'var(--radius-sm)',
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '0.35rem',
                backgroundColor: activeSubTab === 'ratecards' ? 'var(--brand-primary)' : 'transparent',
                color: activeSubTab === 'ratecards' ? '#0f172a' : 'var(--text-secondary)',
                transition: 'all 0.15s ease',
              }}
            >
              <IndianRupee size={14} /> Rate Cards
            </button>

            <button
              type="button"
              onClick={() => setActiveSubTab('zones')}
              style={{
                padding: '0.4rem 0.85rem',
                fontSize: '0.8125rem',
                fontWeight: 600,
                borderRadius: 'var(--radius-sm)',
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '0.35rem',
                backgroundColor: activeSubTab === 'zones' ? 'var(--brand-primary)' : 'transparent',
                color: activeSubTab === 'zones' ? '#0f172a' : 'var(--text-secondary)',
                transition: 'all 0.15s ease',
              }}
            >
              <Layers size={14} /> Zones ({zones.length})
            </button>

            <button
              type="button"
              onClick={() => setActiveSubTab('areas')}
              style={{
                padding: '0.4rem 0.85rem',
                fontSize: '0.8125rem',
                fontWeight: 600,
                borderRadius: 'var(--radius-sm)',
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '0.35rem',
                backgroundColor: activeSubTab === 'areas' ? 'var(--brand-primary)' : 'transparent',
                color: activeSubTab === 'areas' ? '#0f172a' : 'var(--text-secondary)',
                transition: 'all 0.15s ease',
              }}
            >
              <MapPin size={14} /> Areas ({areas.length})
            </button>
          </div>
        </div>

        <button onClick={loadData} className="btn btn-secondary btn-sm" disabled={isLoading}>
          <RefreshCw size={13} className={isLoading ? 'animate-spin' : ''} /> Refresh Data
        </button>
      </div>

      {/* Messages */}
      {error && (
        <div style={{ backgroundColor: 'rgba(244, 63, 94, 0.15)', border: '1px solid rgba(244, 63, 94, 0.3)', color: '#fb7185', padding: '0.75rem 1rem', borderRadius: 'var(--radius-md)', fontSize: '0.8125rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <AlertCircle size={16} /> {error}
        </div>
      )}
      {successMsg && (
        <div style={{ backgroundColor: 'rgba(16, 185, 129, 0.15)', border: '1px solid rgba(16, 185, 129, 0.3)', color: '#34d399', padding: '0.75rem 1rem', borderRadius: 'var(--radius-md)', fontSize: '0.8125rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <Check size={16} /> {successMsg}
        </div>
      )}

      {/* 1. RATE CARDS SUB-TAB */}
      {activeSubTab === 'ratecards' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.25rem' }}>
          {rateCards.map((rc) => (
            <div key={rc.id} className="card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem', border: '1px solid var(--border-subtle)' }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span className="badge badge-info" style={{ fontSize: '0.75rem', fontWeight: 700, padding: '0.2rem 0.6rem' }}>
                  {rc.orderType} TIER
                </span>
                <button onClick={() => handleOpenEditRateCard(rc)} className="btn btn-secondary btn-sm" style={{ padding: '0.25rem 0.6rem', fontSize: '0.75rem' }}>
                  <Edit2 size={13} /> Edit Rates
                </button>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div style={{ padding: '0.75rem', borderRadius: 'var(--radius-md)', backgroundColor: 'var(--bg-app)' }}>
                  <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', textTransform: 'uppercase', display: 'block' }}>Intra-Zone Rate</span>
                  <span style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--brand-primary)', fontFamily: 'var(--font-mono)' }}>₹{rc.intraZoneRatePerKg.toFixed(2)}</span>
                  <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', display: 'block' }}>per chargeable kg</span>
                </div>

                <div style={{ padding: '0.75rem', borderRadius: 'var(--radius-md)', backgroundColor: 'var(--bg-app)' }}>
                  <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', textTransform: 'uppercase', display: 'block' }}>Inter-Zone Rate</span>
                  <span style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--brand-primary)', fontFamily: 'var(--font-mono)' }}>₹{rc.interZoneRatePerKg.toFixed(2)}</span>
                  <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', display: 'block' }}>per chargeable kg</span>
                </div>
              </div>

              <div style={{ padding: '0.75rem', borderRadius: 'var(--radius-md)', backgroundColor: 'var(--bg-app)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>Flat COD Surcharge:</span>
                <span style={{ fontSize: '1rem', fontWeight: 700, color: '#f59e0b', fontFamily: 'var(--font-mono)' }}>₹{rc.codSurcharge.toFixed(2)}</span>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* 2. ZONES SUB-TAB */}
      {activeSubTab === 'zones' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <button onClick={handleOpenCreateZone} className="btn btn-primary btn-sm">
              <Plus size={14} /> Add Delivery Zone
            </button>
          </div>

          <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Zone Code</th>
                  <th>Zone Name</th>
                  <th>Assigned Areas</th>
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {zones.map((z) => {
                  const zoneAreas = areas.filter((a) => a.zoneId === z.id);
                  return (
                    <tr key={z.id}>
                      <td style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--brand-primary)' }}>{z.code}</td>
                      <td style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{z.name}</td>
                      <td>
                        <span className="badge badge-neutral" style={{ fontSize: '0.7rem' }}>
                          {zoneAreas.length} Areas
                        </span>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <div style={{ display: 'inline-flex', gap: '0.35rem' }}>
                          <button onClick={() => handleOpenEditZone(z)} className="btn btn-secondary btn-sm" style={{ padding: '0.25rem 0.5rem' }} title="Edit Zone">
                            <Edit2 size={13} />
                          </button>
                          <button onClick={() => handleDeleteZone(z)} className="btn btn-secondary btn-sm" style={{ padding: '0.25rem 0.5rem', color: '#fb7185' }} title="Delete Zone">
                            <Trash2 size={13} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* 3. AREAS SUB-TAB */}
      {activeSubTab === 'areas' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '1rem', flexWrap: 'wrap' }}>
            <input
              type="text"
              placeholder="Search areas or zones..."
              value={areaSearch}
              onChange={(e) => setAreaSearch(e.target.value)}
              className="input-control"
              style={{ maxWidth: '300px' }}
            />
            <button onClick={handleOpenCreateArea} className="btn btn-primary btn-sm">
              <Plus size={14} /> Add Delivery Area
            </button>
          </div>

          <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Area Code</th>
                  <th>Area Name</th>
                  <th>Assigned Zone</th>
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredAreas.map((a) => (
                  <tr key={a.id}>
                    <td style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--brand-primary)' }}>{a.code}</td>
                    <td style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{a.name}</td>
                    <td>
                      <span className="badge badge-info" style={{ fontSize: '0.7rem' }}>
                        {a.zone?.name || `Zone ID ${a.zoneId}`}
                      </span>
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: '0.35rem' }}>
                        <button onClick={() => handleOpenEditArea(a)} className="btn btn-secondary btn-sm" style={{ padding: '0.25rem 0.5rem' }} title="Edit / Reassign Zone">
                          <Edit2 size={13} />
                        </button>
                        <button onClick={() => handleDeleteArea(a)} className="btn btn-secondary btn-sm" style={{ padding: '0.25rem 0.5rem', color: '#fb7185' }} title="Delete Area">
                          <Trash2 size={13} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* MODAL: Edit Rate Card */}
      {editingRateCard && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0, 0, 0, 0.75)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem' }}>
          <div className="card" style={{ maxWidth: '420px', width: '100%', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <h3 style={{ fontSize: '1rem', fontWeight: 600 }}>Configure {editingRateCard.orderType} Rates</h3>
              <button onClick={() => setEditingRateCard(null)} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}><X size={18} /></button>
            </div>

            <form onSubmit={handleSaveRateCard} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
              <div>
                <label className="label">Intra-Zone Rate (₹/kg)</label>
                <input
                  type="number"
                  step="0.5"
                  min="1"
                  required
                  value={rateCardForm.intraZoneRatePerKg}
                  onChange={(e) => setRateCardForm({ ...rateCardForm, intraZoneRatePerKg: parseFloat(e.target.value) || 0 })}
                  className="input-control"
                />
              </div>

              <div>
                <label className="label">Inter-Zone Rate (₹/kg)</label>
                <input
                  type="number"
                  step="0.5"
                  min="1"
                  required
                  value={rateCardForm.interZoneRatePerKg}
                  onChange={(e) => setRateCardForm({ ...rateCardForm, interZoneRatePerKg: parseFloat(e.target.value) || 0 })}
                  className="input-control"
                />
              </div>

              <div>
                <label className="label">COD Surcharge (₹)</label>
                <input
                  type="number"
                  step="0.5"
                  min="0"
                  required
                  value={rateCardForm.codSurcharge}
                  onChange={(e) => setRateCardForm({ ...rateCardForm, codSurcharge: parseFloat(e.target.value) || 0 })}
                  className="input-control"
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
                <button type="button" onClick={() => setEditingRateCard(null)} className="btn btn-secondary">Cancel</button>
                <button type="submit" className="btn btn-primary">Save Rates</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: Zone Create / Edit */}
      {zoneModalOpen && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0, 0, 0, 0.75)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem' }}>
          <div className="card" style={{ maxWidth: '400px', width: '100%', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <h3 style={{ fontSize: '1rem', fontWeight: 600 }}>{editingZone ? 'Edit Zone' : 'Create Delivery Zone'}</h3>
              <button onClick={() => setZoneModalOpen(false)} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}><X size={18} /></button>
            </div>

            <form onSubmit={handleSaveZone} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
              <div>
                <label className="label">Zone Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Navi Mumbai"
                  value={zoneForm.name}
                  onChange={(e) => setZoneForm({ ...zoneForm, name: e.target.value })}
                  className="input-control"
                />
              </div>

              <div>
                <label className="label">Zone Code (Unique)</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. ZONE_NAVI_MUMBAI"
                  value={zoneForm.code}
                  onChange={(e) => setZoneForm({ ...zoneForm, code: e.target.value.toUpperCase() })}
                  className="input-control"
                  style={{ fontFamily: 'var(--font-mono)' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
                <button type="button" onClick={() => setZoneModalOpen(false)} className="btn btn-secondary">Cancel</button>
                <button type="submit" className="btn btn-primary">{editingZone ? 'Update Zone' : 'Create Zone'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: Area Create / Edit (With Zone Assignment) */}
      {areaModalOpen && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0, 0, 0, 0.75)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem' }}>
          <div className="card" style={{ maxWidth: '420px', width: '100%', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <h3 style={{ fontSize: '1rem', fontWeight: 600 }}>{editingArea ? 'Edit / Reassign Area' : 'Create Delivery Area'}</h3>
              <button onClick={() => setAreaModalOpen(false)} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}><X size={18} /></button>
            </div>

            <form onSubmit={handleSaveArea} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
              <div>
                <label className="label">Area Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Vashi"
                  value={areaForm.name}
                  onChange={(e) => setAreaForm({ ...areaForm, name: e.target.value })}
                  className="input-control"
                />
              </div>

              <div>
                <label className="label">Area Code (Unique)</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. VASHI"
                  value={areaForm.code}
                  onChange={(e) => setAreaForm({ ...areaForm, code: e.target.value.toUpperCase() })}
                  className="input-control"
                  style={{ fontFamily: 'var(--font-mono)' }}
                />
              </div>

              <div>
                <label className="label">Assign to Delivery Zone</label>
                <select
                  value={areaForm.zoneId}
                  onChange={(e) => setAreaForm({ ...areaForm, zoneId: parseInt(e.target.value) })}
                  className="input-control"
                  required
                >
                  {zones.map((z) => (
                    <option key={z.id} value={z.id}>
                      {z.name} ({z.code})
                    </option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
                <button type="button" onClick={() => setAreaModalOpen(false)} className="btn btn-secondary">Cancel</button>
                <button type="submit" className="btn btn-primary">{editingArea ? 'Save Area' : 'Create Area'}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
