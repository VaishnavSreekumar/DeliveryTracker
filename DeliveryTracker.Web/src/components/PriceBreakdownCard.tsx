import React from 'react';
import type { PriceCalculationResult } from '../types';
import { DollarSign, ShieldCheck, Scale, MapPin } from 'lucide-react';

interface PriceBreakdownCardProps {
  price: PriceCalculationResult;
  isConfirmed?: boolean;
}

export const PriceBreakdownCard: React.FC<PriceBreakdownCardProps> = ({ price, isConfirmed = false }) => {
  return (
    <div
      style={{
        backgroundColor: 'var(--bg-surface)',
        border: '1px solid var(--border-strong)',
        borderRadius: 'var(--radius-md)',
        padding: '1.25rem',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem', borderBottom: '1px solid var(--border-subtle)', paddingBottom: '0.75rem' }}>
        <h4 style={{ fontSize: '0.875rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <DollarSign size={16} color="var(--brand-primary)" /> Pricing Calculation Breakdown
        </h4>
        <span style={{ fontSize: '0.75rem', color: isConfirmed ? 'var(--status-delivered-fg)' : 'var(--brand-primary)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '4px' }}>
          <ShieldCheck size={14} /> {isConfirmed ? 'Backend Verified Price' : 'Backend Quoted Rate'}
        </span>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '0.75rem', marginBottom: '1rem' }}>
        <div style={{ backgroundColor: 'var(--bg-input)', padding: '0.625rem 0.875rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)' }}>
          <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', display: 'block', textTransform: 'uppercase' }}>Route Zones</span>
          <span style={{ fontSize: '0.875rem', fontWeight: 600, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '4px', marginTop: '2px' }}>
            <MapPin size={12} color="var(--brand-primary)" /> {price.pickupZone} &rarr; {price.dropZone}
          </span>
        </div>

        <div style={{ backgroundColor: 'var(--bg-input)', padding: '0.625rem 0.875rem', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)' }}>
          <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', display: 'block', textTransform: 'uppercase' }}>Chargeable Weight</span>
          <span style={{ fontSize: '0.875rem', fontWeight: 600, color: 'var(--brand-primary)', display: 'flex', alignItems: 'center', gap: '4px', marginTop: '2px' }}>
            <Scale size={12} /> {price.chargeableWeight.toFixed(2)} kg
          </span>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', fontSize: '0.875rem', borderBottom: '1px solid var(--border-subtle)', paddingBottom: '0.75rem', marginBottom: '0.75rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)' }}>
          <span>Actual Weight</span>
          <span>{price.actualWeight.toFixed(2)} kg</span>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)' }}>
          <span>Volumetric Weight ((L&times;B&times;H)/5000)</span>
          <span>{price.volumetricWeight.toFixed(2)} kg</span>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)' }}>
          <span>Base Rate / kg</span>
          <span>₹{price.ratePerKg.toFixed(2)}</span>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)' }}>
          <span>Delivery Fee (Rate &times; Chargeable Wt)</span>
          <span>₹{price.deliveryFee.toFixed(2)}</span>
        </div>
        {price.codSurcharge > 0 && (
          <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--status-outfordelivery-fg)' }}>
            <span>COD Surcharge</span>
            <span>+ ₹{price.codSurcharge.toFixed(2)}</span>
          </div>
        )}
      </div>

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', paddingTop: '0.25rem' }}>
        <div>
          <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>Total Final Amount</span>
          <span style={{ display: 'block', fontSize: '0.7rem', color: 'var(--text-muted)' }}>Includes all taxes and surcharges</span>
        </div>
        <div style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>
          ₹{price.totalAmount.toFixed(2)}
        </div>
      </div>
    </div>
  );
};
