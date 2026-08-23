import React from 'react';
import { useAuth } from '../context/AuthContext';
import { Truck, PackagePlus, ListOrdered, Shield, LogOut, User as UserIcon } from 'lucide-react';

interface LayoutProps {
  children: React.ReactNode;
  activeTab: 'orders' | 'create-order' | 'operations';
  onTabChange: (tab: 'orders' | 'create-order' | 'operations') => void;
  title: string;
}

export const Layout: React.FC<LayoutProps> = ({ children, activeTab, onTabChange, title }) => {
  const { user, logout } = useAuth();

  return (
    <div style={{ display: 'flex', minHeight: '100vh', backgroundColor: 'var(--bg-app)' }}>
      {/* Left Application Sidebar */}
      <aside
        style={{
          width: '260px',
          backgroundColor: 'var(--bg-sidebar)',
          borderRight: '1px solid var(--border-subtle)',
          display: 'flex',
          flexDirection: 'column',
          flexShrink: 0,
        }}
      >
        {/* Brand Header */}
        <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-subtle)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.625rem' }}>
            <div
              style={{
                width: '32px',
                height: '32px',
                borderRadius: 'var(--radius-md)',
                backgroundColor: 'rgba(56, 189, 248, 0.15)',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <Truck size={18} color="var(--brand-primary)" />
            </div>
            <div>
              <h1 style={{ fontSize: '1rem', fontWeight: 700, letterSpacing: '-0.01em', color: 'var(--text-primary)' }}>
                DeliveryTracker
              </h1>
              <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>
                Operations Platform
              </span>
            </div>
          </div>
        </div>

        {/* Navigation Menu Links */}
        <nav style={{ padding: '1rem 0.75rem', flex: 1 }}>
          <div style={{ fontSize: '0.6875rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-muted)', padding: '0.5rem 0.75rem', marginBottom: '0.25rem' }}>
            Navigation
          </div>

          {user?.role === 'Admin' && (
            <button
              onClick={() => onTabChange('operations')}
              style={{
                width: '100%',
                display: 'flex',
                alignItems: 'center',
                gap: '0.625rem',
                padding: '0.625rem 0.75rem',
                borderRadius: 'var(--radius-md)',
                fontSize: '0.875rem',
                fontWeight: 500,
                color: activeTab === 'operations' ? 'var(--brand-primary)' : 'var(--text-secondary)',
                backgroundColor: activeTab === 'operations' ? 'rgba(56, 189, 248, 0.1)' : 'transparent',
                border: activeTab === 'operations' ? '1px solid rgba(56, 189, 248, 0.2)' : '1px solid transparent',
                cursor: 'pointer',
                textAlign: 'left',
                marginBottom: '0.35rem',
              }}
            >
              <Shield size={16} /> Operations Console
            </button>
          )}

          <button
            onClick={() => onTabChange('orders')}
            style={{
              width: '100%',
              display: 'flex',
              alignItems: 'center',
              gap: '0.625rem',
              padding: '0.625rem 0.75rem',
              borderRadius: 'var(--radius-md)',
              fontSize: '0.875rem',
              fontWeight: 500,
              color: activeTab === 'orders' ? 'var(--brand-primary)' : 'var(--text-secondary)',
              backgroundColor: activeTab === 'orders' ? 'rgba(56, 189, 248, 0.1)' : 'transparent',
              border: activeTab === 'orders' ? '1px solid rgba(56, 189, 248, 0.2)' : '1px solid transparent',
              cursor: 'pointer',
              textAlign: 'left',
              marginBottom: '0.35rem',
            }}
          >
            <ListOrdered size={16} />
            {user?.role === 'Agent' ? 'My Deliveries' : user?.role === 'Customer' ? 'My Orders' : 'All Orders'}
          </button>

          {(user?.role === 'Customer' || user?.role === 'Admin') && (
            <button
              onClick={() => onTabChange('create-order')}
              style={{
                width: '100%',
                display: 'flex',
                alignItems: 'center',
                gap: '0.625rem',
                padding: '0.625rem 0.75rem',
                borderRadius: 'var(--radius-md)',
                fontSize: '0.875rem',
                fontWeight: 500,
                color: activeTab === 'create-order' ? 'var(--brand-primary)' : 'var(--text-secondary)',
                backgroundColor: activeTab === 'create-order' ? 'rgba(56, 189, 248, 0.1)' : 'transparent',
                border: activeTab === 'create-order' ? '1px solid rgba(56, 189, 248, 0.2)' : '1px solid transparent',
                cursor: 'pointer',
                textAlign: 'left',
              }}
            >
              <PackagePlus size={16} /> Create Delivery
            </button>
          )}
        </nav>

        {/* User Identity Section */}
        <div style={{ padding: '1rem', borderTop: '1px solid var(--border-subtle)', backgroundColor: 'rgba(15, 23, 42, 0.4)' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', overflow: 'hidden' }}>
              <div
                style={{
                  width: '32px',
                  height: '32px',
                  borderRadius: '50%',
                  backgroundColor: 'var(--bg-surface-hover)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <UserIcon size={16} color="var(--text-secondary)" />
              </div>
              <div style={{ overflow: 'hidden' }}>
                <div style={{ fontSize: '0.8125rem', fontWeight: 600, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {user?.fullName}
                </div>
                <div style={{ fontSize: '0.7rem', color: 'var(--brand-primary)', fontWeight: 600 }}>
                  {user?.role}
                </div>
              </div>
            </div>

            <button
              onClick={logout}
              title="Log out"
              style={{
                background: 'none',
                border: 'none',
                color: 'var(--text-muted)',
                cursor: 'pointer',
                padding: '4px',
                borderRadius: 'var(--radius-sm)',
              }}
            >
              <LogOut size={16} />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Operational Container */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflowX: 'hidden' }}>
        {/* Top Header Bar */}
        <header
          style={{
            height: '60px',
            backgroundColor: 'var(--bg-surface)',
            borderBottom: '1px solid var(--border-subtle)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0 2rem',
            flexShrink: 0,
          }}
        >
          <h2 style={{ fontSize: '1.125rem', fontWeight: 600, color: 'var(--text-primary)' }}>
            {title}
          </h2>

          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
            <span>Environment: <strong>Local Development</strong></span>
            <span>API Server: <strong>http://localhost:5055</strong></span>
          </div>
        </header>

        {/* View Content */}
        <main
          style={{
            flex: 1,
            padding: '2rem',
            maxWidth: '1200px',
            width: '100%',
            margin: '0 auto',
          }}
        >
          {children}
        </main>
      </div>
    </div>
  );
};
