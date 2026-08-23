import React from 'react';
import type { OrderStatusHistory } from '../types';
import { StatusBadge } from './StatusBadge';
import { CheckCircle2, AlertTriangle, Calendar, User, Clock } from 'lucide-react';

interface TrackingTimelineProps {
  history: OrderStatusHistory[];
}

export const TrackingTimeline: React.FC<TrackingTimelineProps> = ({ history }) => {
  if (!history || history.length === 0) {
    return <div style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>No tracking history recorded yet.</div>;
  }

  // Sort history chronologically (oldest to newest)
  const sortedHistory = [...history].sort(
    (a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
  );

  return (
    <div style={{ position: 'relative', paddingLeft: '1.5rem' }}>
      {/* Vertical Connection Line */}
      <div
        style={{
          position: 'absolute',
          left: '11px',
          top: '12px',
          bottom: '24px',
          width: '2px',
          backgroundColor: 'var(--border-subtle)',
        }}
      />

      {sortedHistory.map((item, index) => {
        const isLatest = index === sortedHistory.length - 1;
        const isFailed = item.status === 'Failed';
        const isRescheduled = item.status === 'Rescheduled';

        const dateFormatted = new Date(item.timestamp).toLocaleString('en-US', {
          month: 'short',
          day: 'numeric',
          year: 'numeric',
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
        });

        return (
          <div
            key={item.id || index}
            style={{
              position: 'relative',
              marginBottom: '1.5rem',
            }}
          >
            {/* Indicator Icon */}
            <div
              style={{
                position: 'absolute',
                left: '-1.5rem',
                top: '2px',
                width: '24px',
                height: '24px',
                borderRadius: '50%',
                backgroundColor: isFailed
                  ? 'rgba(244, 63, 94, 0.2)'
                  : isRescheduled
                  ? 'rgba(168, 85, 247, 0.2)'
                  : isLatest
                  ? 'rgba(56, 189, 248, 0.2)'
                  : 'var(--bg-surface)',
                border: `2px solid ${
                  isFailed
                    ? '#fb7185'
                    : isRescheduled
                    ? '#c084fc'
                    : isLatest
                    ? 'var(--brand-primary)'
                    : 'var(--border-strong)'
                }`,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                zIndex: 1,
              }}
            >
              {isFailed ? (
                <AlertTriangle size={12} color="#fb7185" />
              ) : isRescheduled ? (
                <Calendar size={12} color="#c084fc" />
              ) : (
                <CheckCircle2 size={12} color={isLatest ? 'var(--brand-primary)' : 'var(--text-secondary)'} />
              )}
            </div>

            {/* Event Details Box */}
            <div
              style={{
                backgroundColor: isLatest ? 'rgba(30, 41, 59, 0.8)' : 'var(--bg-surface)',
                border: `1px solid ${isLatest ? 'var(--border-strong)' : 'var(--border-subtle)'}`,
                borderRadius: 'var(--radius-md)',
                padding: '0.875rem 1rem',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.35rem' }}>
                <StatusBadge status={item.status} size="sm" />
                <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', display: 'flex', alignItems: 'center', gap: '4px' }}>
                  <Clock size={12} /> {dateFormatted}
                </span>
              </div>

              {item.notes && (
                <p style={{ fontSize: '0.875rem', color: 'var(--text-primary)', margin: '0.35rem 0' }}>
                  {item.notes}
                </p>
              )}

              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '0.35rem', fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                <User size={12} />
                <span>Actor ID #{item.actorId}</span>
                <span style={{ backgroundColor: 'var(--bg-input)', padding: '1px 6px', borderRadius: '3px', border: '1px solid var(--border-subtle)', fontWeight: 600 }}>
                  {item.actorRole}
                </span>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
};
