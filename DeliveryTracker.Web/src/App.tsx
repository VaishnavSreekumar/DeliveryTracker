import React, { useState } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import { Layout } from './components/Layout';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { OrdersListPage } from './pages/OrdersListPage';
import { CustomerCreateOrderPage } from './pages/CustomerCreateOrderPage';
import { OrderDetailPage } from './pages/OrderDetailPage';
import { AdminOperationsPage } from './pages/AdminOperationsPage';

const AppContent: React.FC = () => {
  const { user, token } = useAuth();
  const [authView, setAuthView] = useState<'login' | 'register'>('login');

  const [activeTab, setActiveTab] = useState<'orders' | 'create-order' | 'operations'>(() => {
    if (user?.role === 'Admin') return 'operations';
    return 'orders';
  });

  const [selectedOrderId, setSelectedOrderId] = useState<number | null>(null);

  // If not logged in, render Auth pages
  if (!user || !token) {
    if (authView === 'register') {
      return <RegisterPage onNavigateLogin={() => setAuthView('login')} />;
    }
    return <LoginPage onNavigateRegister={() => setAuthView('register')} />;
  }

  // Handle Order Selection for Detail View
  if (selectedOrderId !== null) {
    return (
      <Layout
        activeTab={activeTab}
        onTabChange={(tab) => {
          setSelectedOrderId(null);
          setActiveTab(tab);
        }}
        title={`Order Detail #${selectedOrderId}`}
      >
        <OrderDetailPage orderId={selectedOrderId} onBack={() => setSelectedOrderId(null)} />
      </Layout>
    );
  }

  // Active Tab Title Mapping
  const getTitle = () => {
    switch (activeTab) {
      case 'create-order':
        return 'Create Delivery Order';
      case 'operations':
        return 'Operations Management Console';
      case 'orders':
      default:
        return user.role === 'Agent'
          ? 'My Assigned Deliveries'
          : user.role === 'Customer'
          ? 'My Delivery Orders'
          : 'System Orders Overview';
    }
  };

  return (
    <Layout
      activeTab={activeTab}
      onTabChange={(tab) => {
        setSelectedOrderId(null);
        setActiveTab(tab);
      }}
      title={getTitle()}
    >
      {activeTab === 'create-order' && (
        <CustomerCreateOrderPage
          onOrderCreated={(newId) => {
            setSelectedOrderId(newId);
          }}
        />
      )}

      {activeTab === 'operations' && user.role === 'Admin' && (
        <AdminOperationsPage onSelectOrder={(id) => setSelectedOrderId(id)} />
      )}

      {activeTab === 'orders' && (
        <OrdersListPage
          onSelectOrder={(id) => setSelectedOrderId(id)}
          onCreateOrderClick={() => setActiveTab('create-order')}
        />
      )}
    </Layout>
  );
};

export const App: React.FC = () => {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );
};

export default App;
