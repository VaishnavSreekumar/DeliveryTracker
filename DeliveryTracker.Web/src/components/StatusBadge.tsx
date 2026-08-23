import React from 'react';
import type { OrderStatus } from '../types';

interface StatusBadgeProps {
  status: OrderStatus;
  size?: 'sm' | 'md' | 'lg';
}

export const StatusBadge: React.FC<StatusBadgeProps> = ({ status, size = 'md' }) => {
  const getStatusStyles = (status: OrderStatus) => {
    switch (status) {
      case 'Created':
        return {
          bg: 'var(--status-created-bg)',
          fg: 'var(--status-created-fg)',
          border: 'var(--status-created-border)',
          label: 'Created',
        };
      case 'PickedUp':
        return {
          bg: 'var(--status-pickedup-bg)',
          fg: 'var(--status-pickedup-fg)',
          border: 'var(--status-pickedup-border)',
          label: 'Picked Up',
        };
      case 'InTransit':
        return {
          bg: 'var(--status-intransit-bg)',
          fg: 'var(--status-intransit-fg)',
          border: 'var(--status-intransit-border)',
          label: 'In Transit',
        };
      case 'OutForDelivery':
        return {
          bg: 'var(--status-outfordelivery-bg)',
          fg: 'var(--status-outfordelivery-fg)',
          border: 'var(--status-outfordelivery-border)',
          label: 'Out For Delivery',
        };
      case 'Delivered':
        return {
          bg: 'var(--status-delivered-bg)',
          fg: 'var(--status-delivered-fg)',
          border: 'var(--status-delivered-border)',
          label: 'Delivered',
        };
      case 'Failed':
        return {
          bg: 'var(--status-failed-bg)',
          fg: 'var(--status-failed-fg)',
          border: 'var(--status-failed-border)',
          label: 'Failed Attempt',
        };
      case 'Rescheduled':
        return {
          bg: 'var(--status-rescheduled-bg)',
          fg: 'var(--status-rescheduled-fg)',
          border: 'var(--status-rescheduled-border)',
          label: 'Rescheduled',
        };
      default:
        return {
          bg: 'var(--status-created-bg)',
          fg: 'var(--status-created-fg)',
          border: 'var(--status-created-border)',
          label: status,
        };
    }
  };

  const style = getStatusStyles(status);
  const padding = size === 'sm' ? '2px 6px' : size === 'lg' ? '6px 14px' : '4px 10px';
  const fontSize = size === 'sm' ? '0.7rem' : size === 'lg' ? '0.875rem' : '0.75rem';

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '6px',
        backgroundColor: style.bg,
        color: style.fg,
        border: `1px solid ${style.border}`,
        borderRadius: 'var(--radius-sm)',
        padding,
        fontSize,
        fontWeight: 600,
        letterSpacing: '0.02em',
        whiteSpace: 'nowrap',
      }}
    >
      <span
        style={{
          width: '6px',
          height: '6px',
          borderRadius: '50%',
          backgroundColor: style.fg,
        }}
      />
      {style.label}
    </span>
  );
};
