import React, { useState, useEffect, useRef } from 'react';
import { Bell, CheckCheck, Package } from 'lucide-react';
import { apiClient } from '../api/apiClient';
import type { AppNotification } from '../types';

interface NotificationCenterProps {
  onSelectOrder: (orderId: number) => void;
}

export const NotificationCenter: React.FC<NotificationCenterProps> = ({ onSelectOrder }) => {
  const [isOpen, setIsOpen] = useState(false);
  const [notifications, setNotifications] = useState<AppNotification[]>([]);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const fetchNotifications = async () => {
    try {
      const data = await apiClient.get<AppNotification[]>('/notifications');
      setNotifications(data);
    } catch {
      // Silently fail in polling
    }
  };

  useEffect(() => {
    fetchNotifications();
    const interval = setInterval(fetchNotifications, 5000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  const handleMarkAsRead = async (id: number, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    try {
      await apiClient.patch<AppNotification>(`/notifications/${id}/read`);
      setNotifications((prev) =>
        prev.map((n) => (n.id === id ? { ...n, isRead: true } : n))
      );
    } catch {
      // Failed to mark read
    }
  };

  const handleNotificationClick = async (notification: AppNotification) => {
    if (!notification.isRead) {
      await handleMarkAsRead(notification.id);
    }
    setIsOpen(false);
    if (notification.orderId) {
      onSelectOrder(notification.orderId);
    }
  };

  const handleMarkAllRead = async () => {
    const unread = notifications.filter((n) => !n.isRead);
    for (const n of unread) {
      await handleMarkAsRead(n.id);
    }
  };

  return (
    <div style={{ position: 'relative' }} ref={dropdownRef}>
      <button
        onClick={() => {
          setIsOpen(!isOpen);
          if (!isOpen) fetchNotifications();
        }}
        aria-label="Notifications"
        style={{
          position: 'relative',
          background: 'none',
          border: '1px solid var(--border-subtle)',
          borderRadius: 'var(--radius-md)',
          padding: '0.45rem',
          color: unreadCount > 0 ? 'var(--brand-primary)' : 'var(--text-secondary)',
          backgroundColor: isOpen ? 'var(--bg-surface-hover)' : 'transparent',
          cursor: 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          transition: 'all 0.15s ease',
        }}
      >
        <Bell size={18} />
        {unreadCount > 0 && (
          <span
            style={{
              position: 'absolute',
              top: '-4px',
              right: '-4px',
              backgroundColor: '#ef4444',
              color: '#ffffff',
              fontSize: '0.65rem',
              fontWeight: 700,
              minWidth: '17px',
              height: '17px',
              borderRadius: '999px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              padding: '0 3px',
              boxShadow: '0 0 0 2px var(--bg-surface)',
            }}
          >
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div
          style={{
            position: 'absolute',
            right: 0,
            top: 'calc(100% + 8px)',
            width: '360px',
            maxHeight: '440px',
            backgroundColor: 'var(--bg-surface)',
            border: '1px solid var(--border-subtle)',
            borderRadius: 'var(--radius-lg)',
            boxShadow: '0 10px 25px -5px rgba(0, 0, 0, 0.5), 0 8px 10px -6px rgba(0, 0, 0, 0.5)',
            zIndex: 1000,
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
          }}
        >
          {/* Header */}
          <div
            style={{
              padding: '0.875rem 1rem',
              borderBottom: '1px solid var(--border-subtle)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              backgroundColor: 'rgba(15, 23, 42, 0.6)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <span style={{ fontSize: '0.875rem', fontWeight: 600, color: 'var(--text-primary)' }}>
                Notifications
              </span>
              {unreadCount > 0 && (
                <span
                  style={{
                    backgroundColor: 'rgba(56, 189, 248, 0.15)',
                    color: 'var(--brand-primary)',
                    fontSize: '0.7rem',
                    fontWeight: 600,
                    padding: '0.1rem 0.45rem',
                    borderRadius: 'var(--radius-sm)',
                  }}
                >
                  {unreadCount} new
                </span>
              )}
            </div>

            {unreadCount > 0 && (
              <button
                onClick={handleMarkAllRead}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--text-muted)',
                  fontSize: '0.75rem',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.25rem',
                }}
                title="Mark all as read"
              >
                <CheckCheck size={14} /> Mark all read
              </button>
            )}
          </div>

          {/* List */}
          <div style={{ overflowY: 'auto', flex: 1 }}>
            {notifications.length === 0 ? (
              <div style={{ padding: '2.5rem 1rem', textAlign: 'center', color: 'var(--text-muted)' }}>
                <Bell size={28} style={{ opacity: 0.3, marginBottom: '0.5rem' }} />
                <p style={{ fontSize: '0.8125rem' }}>No notifications yet</p>
              </div>
            ) : (
              notifications.map((n) => (
                <div
                  key={n.id}
                  onClick={() => handleNotificationClick(n)}
                  style={{
                    padding: '0.875rem 1rem',
                    borderBottom: '1px solid var(--border-subtle)',
                    cursor: 'pointer',
                    backgroundColor: n.isRead ? 'transparent' : 'rgba(56, 189, 248, 0.05)',
                    transition: 'background-color 0.15s ease',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '0.35rem',
                  }}
                  onMouseEnter={(e) =>
                    (e.currentTarget.style.backgroundColor = n.isRead
                      ? 'var(--bg-surface-hover)'
                      : 'rgba(56, 189, 248, 0.1)')
                  }
                  onMouseLeave={(e) =>
                    (e.currentTarget.style.backgroundColor = n.isRead
                      ? 'transparent'
                      : 'rgba(56, 189, 248, 0.05)')
                  }
                >
                  <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '0.5rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                      {!n.isRead && (
                        <span
                          style={{
                            width: '6px',
                            height: '6px',
                            borderRadius: '50%',
                            backgroundColor: 'var(--brand-primary)',
                            display: 'inline-block',
                          }}
                        />
                      )}
                      <span
                        style={{
                          fontSize: '0.8125rem',
                          fontWeight: n.isRead ? 500 : 700,
                          color: n.isRead ? 'var(--text-secondary)' : 'var(--text-primary)',
                        }}
                      >
                        {n.title}
                      </span>
                    </div>
                    <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>
                      {new Date(n.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    </span>
                  </div>

                  <p
                    style={{
                      fontSize: '0.75rem',
                      color: 'var(--text-muted)',
                      margin: 0,
                      lineHeight: 1.4,
                    }}
                  >
                    {n.message}
                  </p>

                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: '0.2rem' }}>
                    {n.orderTrackingNumber && (
                      <span
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '0.25rem',
                          fontSize: '0.7rem',
                          color: 'var(--brand-primary)',
                          fontFamily: 'var(--font-mono)',
                        }}
                      >
                        <Package size={11} /> {n.orderTrackingNumber}
                      </span>
                    )}

                    {!n.isRead && (
                      <button
                        onClick={(e) => handleMarkAsRead(n.id, e)}
                        style={{
                          background: 'none',
                          border: 'none',
                          color: 'var(--text-muted)',
                          fontSize: '0.7rem',
                          cursor: 'pointer',
                          padding: '0 0.25rem',
                        }}
                        title="Mark as read"
                      >
                        Mark read
                      </button>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
};
