import React, { useState, useEffect } from 'react';
import type { Area, OrderType, PaymentType, PriceCalculationResult, Order } from '../types';
import { apiClient } from '../api/apiClient';
import { PriceBreakdownCard } from '../components/PriceBreakdownCard';
import { MapPin, Package, CreditCard, Calculator, CheckCircle, AlertCircle } from 'lucide-react';

interface CustomerCreateOrderPageProps {
  onOrderCreated: (orderId: number) => void;
}

export const CustomerCreateOrderPage: React.FC<CustomerCreateOrderPageProps> = ({ onOrderCreated }) => {
  const [areas, setAreas] = useState<Area[]>([]);
  const [isLoadingAreas, setIsLoadingAreas] = useState(true);

  // Form State
  const [pickupAreaId, setPickupAreaId] = useState<number>(1); // Colaba
  const [dropAreaId, setDropAreaId] = useState<number>(3);   // Andheri
  const [pickupAddress, setPickupAddress] = useState('123 Colaba Causeway, Flat 4B');
  const [dropAddress, setDropAddress] = useState('456 Andheri Link Road, Hub 2');

  const [lengthCm, setLengthCm] = useState<number>(30);
  const [widthCm, setWidthCm] = useState<number>(20);
  const [heightCm, setHeightCm] = useState<number>(15);
  const [actualWeightKg, setActualWeightKg] = useState<number>(4);

  const [orderType, setOrderType] = useState<OrderType>('B2C');
  const [paymentType, setPaymentType] = useState<PaymentType>('COD');

  // Calculation & Submission state
  const [calculatedPrice, setCalculatedPrice] = useState<PriceCalculationResult | null>(null);
  const [isCalculating, setIsCalculating] = useState(false);
  const [isConfirming, setIsConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load Zones & Areas dynamically from GET /api/zones
  useEffect(() => {
    const fetchZones = async () => {
      try {
        const zonesData = await apiClient.get<any[]>('/zones');
        const allAreas: Area[] = [];
        zonesData.forEach((z) => {
          if (z.areas && Array.isArray(z.areas)) {
            z.areas.forEach((a: any) => {
              allAreas.push({
                id: a.id,
                name: a.name,
                code: a.code,
                zoneId: a.zoneId,
                zone: { id: z.id, name: z.name, code: z.code },
              });
            });
          }
        });
        setAreas(allAreas);
        if (allAreas.length > 0) {
          setPickupAreaId(allAreas[0].id);
          setDropAreaId(allAreas.length > 2 ? allAreas[2].id : allAreas[0].id);
        }
      } catch (err: any) {
        setError('Failed to load zone coverage map.');
      } finally {
        setIsLoadingAreas(false);
      }
    };

    fetchZones();
  }, []);

  // Live preliminary UX volumetric estimate
  const uxVolumetricEstimate = ((lengthCm * widthCm * heightCm) / 5000).toFixed(2);

  const handleCalculatePrice = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsCalculating(true);
    setError(null);
    setCalculatedPrice(null);

    try {
      const result = await apiClient.post<PriceCalculationResult>('/orders/calculate-price', {
        pickupAreaId,
        dropAreaId,
        length: lengthCm,
        breadth: widthCm,
        height: heightCm,
        actualWeight: actualWeightKg,
        orderType,
        paymentType,
      });
      setCalculatedPrice(result);
    } catch (err: any) {
      setError(err.message || 'Price calculation failed.');
    } finally {
      setIsCalculating(false);
    }
  };

  const handleConfirmOrder = async () => {
    if (!calculatedPrice) return;
    setIsConfirming(true);
    setError(null);

    try {
      const createdOrder = await apiClient.post<Order>('/orders', {
        pickupAreaId,
        dropAreaId,
        pickupAddress,
        dropAddress,
        length: lengthCm,
        breadth: widthCm,
        height: heightCm,
        actualWeight: actualWeightKg,
        orderType,
        paymentType,
      });
      onOrderCreated(createdOrder.id);
    } catch (err: any) {
      setError(err.message || 'Order creation failed.');
    } finally {
      setIsConfirming(false);
    }
  };

  if (isLoadingAreas) {
    return <div style={{ color: 'var(--text-muted)', padding: '2rem', textAlign: 'center' }}>Loading dynamic zone rate map...</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Intro Context Banner */}
      <div className="card" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h3 style={{ fontSize: '1rem', fontWeight: 600 }}>Create New Shipping Order</h3>
          <p style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
            Configure delivery route, package metrics, and calculate rate card preview before confirmation.
          </p>
        </div>
        <span style={{ fontSize: '0.75rem', color: 'var(--brand-primary)', fontWeight: 600, backgroundColor: 'rgba(56, 189, 248, 0.1)', padding: '4px 8px', borderRadius: 'var(--radius-sm)', border: '1px solid rgba(56, 189, 248, 0.2)' }}>
          Step 1 of 2: Quotation & Confirmation
        </span>
      </div>

      {error && (
        <div style={{ backgroundColor: 'rgba(244, 63, 94, 0.15)', border: '1px solid rgba(244, 63, 94, 0.3)', color: '#fb7185', padding: '0.875rem 1rem', borderRadius: 'var(--radius-md)', fontSize: '0.875rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <AlertCircle size={18} /> {error}
        </div>
      )}

      <form onSubmit={handleCalculatePrice} style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.25rem' }}>
        {/* Section 1: Route */}
        <div className="card">
          <h4 style={{ fontSize: '0.875rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <MapPin size={16} color="var(--brand-primary)" /> 1. Route Configuration
          </h4>

          <div style={{ marginBottom: '1rem' }}>
            <label className="label">Pickup Area & Zone</label>
            <select
              className="input-field"
              value={pickupAreaId}
              onChange={(e) => setPickupAreaId(Number(e.target.value))}
            >
              {areas.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name} ({a.zone?.name || `Zone ${a.zoneId}`})
                </option>
              ))}
            </select>
          </div>

          <div style={{ marginBottom: '1rem' }}>
            <label className="label">Pickup Street Address</label>
            <input
              type="text"
              className="input-field"
              value={pickupAddress}
              onChange={(e) => setPickupAddress(e.target.value)}
              required
            />
          </div>

          <div style={{ marginBottom: '1rem' }}>
            <label className="label">Drop Area & Zone</label>
            <select
              className="input-field"
              value={dropAreaId}
              onChange={(e) => setDropAreaId(Number(e.target.value))}
            >
              {areas.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name} ({a.zone?.name || `Zone ${a.zoneId}`})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="label">Drop Street Address</label>
            <input
              type="text"
              className="input-field"
              value={dropAddress}
              onChange={(e) => setDropAddress(e.target.value)}
              required
            />
          </div>
        </div>

        {/* Section 2: Package & Dimensions */}
        <div className="card">
          <h4 style={{ fontSize: '0.875rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Package size={16} color="var(--brand-primary)" /> 2. Package Dimensions & Weight
          </h4>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '0.5rem', marginBottom: '1rem' }}>
            <div>
              <label className="label">Length (cm)</label>
              <input
                type="number"
                min="1"
                className="input-field"
                value={lengthCm}
                onChange={(e) => setLengthCm(Number(e.target.value))}
                required
              />
            </div>
            <div>
              <label className="label">Width (cm)</label>
              <input
                type="number"
                min="1"
                className="input-field"
                value={widthCm}
                onChange={(e) => setWidthCm(Number(e.target.value))}
                required
              />
            </div>
            <div>
              <label className="label">Height (cm)</label>
              <input
                type="number"
                min="1"
                className="input-field"
                value={heightCm}
                onChange={(e) => setHeightCm(Number(e.target.value))}
                required
              />
            </div>
          </div>

          <div style={{ marginBottom: '1rem' }}>
            <label className="label">Actual Scale Weight (kg)</label>
            <input
              type="number"
              step="0.1"
              min="0.1"
              className="input-field"
              value={actualWeightKg}
              onChange={(e) => setActualWeightKg(Number(e.target.value))}
              required
            />
          </div>

          {/* UX Volumetric Estimate Notice */}
          <div style={{ backgroundColor: 'var(--bg-input)', padding: '0.625rem 0.875rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)', fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
            Preliminary Volumetric UX Estimate: <strong style={{ color: 'var(--text-primary)' }}>{uxVolumetricEstimate} kg</strong>
            <span style={{ display: 'block', fontSize: '0.6875rem', color: 'var(--text-muted)', marginTop: '2px' }}>
              Final chargeable weight determined by backend pricing engine.
            </span>
          </div>
        </div>

        {/* Section 3: Service & Payment */}
        <div className="card">
          <h4 style={{ fontSize: '0.875rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <CreditCard size={16} color="var(--brand-primary)" /> 3. Service & Payment Type
          </h4>

          <div style={{ marginBottom: '1rem' }}>
            <label className="label">Service Rate Card Tier</label>
            <select
              className="input-field"
              value={orderType}
              onChange={(e) => setOrderType(e.target.value as OrderType)}
            >
              <option value="B2C">B2C Standard Consumer Shipping</option>
              <option value="B2B">B2B Commercial Bulk Rate</option>
            </select>
          </div>

          <div style={{ marginBottom: '1.25rem' }}>
            <label className="label">Payment Collection Method</label>
            <select
              className="input-field"
              value={paymentType}
              onChange={(e) => setPaymentType(e.target.value as PaymentType)}
            >
              <option value="Prepaid">Prepaid (Digital Payment)</option>
              <option value="COD">Cash on Delivery (COD Surcharge Applies)</option>
            </select>
          </div>

          <button
            type="submit"
            className="btn btn-secondary"
            style={{ width: '100%', padding: '0.625rem' }}
            disabled={isCalculating}
          >
            <Calculator size={16} /> {isCalculating ? 'Calculating Backend Quote...' : 'Calculate Delivery Quote'}
          </button>
        </div>
      </form>

      {/* Section 4: Price Review & Confirmation */}
      {calculatedPrice && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <PriceBreakdownCard price={calculatedPrice} isConfirmed={false} />

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem' }}>
            <button
              onClick={handleConfirmOrder}
              className="btn btn-primary"
              style={{ padding: '0.75rem 1.5rem', fontSize: '1rem' }}
              disabled={isConfirming}
            >
              <CheckCircle size={18} /> {isConfirming ? 'Creating Order...' : 'Confirm Delivery & Book Order'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
