import api from './api';

export interface Material {
  id: number;
  code: string;
  name: string;
  specification?: string;
  unit?: string;
  quantity: number;
  minStock?: number;
  maxStock?: number;
  createdTime?: string;
  updatedTime?: string;
}

export interface MaterialTransaction {
  id: number;
  materialCode: string;
  quantity: number;
  type: 'InStock' | 'OutStock';
  locationCode?: string;
  operatorName?: string;
  remark?: string;
  outReason?: string;
  createTime?: string;
}

export const materialService = {
  // 获取所有物料
  getAllMaterials: async (): Promise<Material[]> => {
    const response = await api.get('/material');
    return response.data.data || [];
  },

  // 根据编码获取物料
  getMaterialByCode: async (code: string): Promise<Material> => {
    const response = await api.get(`/material/${code}`);
    return response.data.data;
  },

  // 物料入库
  inStock: async (data: {
    materialCode: string;
    quantity: number;
    locationCode?: string;
    operatorName?: string;
    remark?: string;
  }): Promise<void> => {
    await api.post('/material/instock', data);
  },

  // 物料出库
  outStock: async (data: {
    materialCode: string;
    quantity: number;
    locationCode?: string;
    outReason?: string;
    operatorName?: string;
    remark?: string;
  }): Promise<void> => {
    await api.post('/material/outstock', data);
  },

  // 获取交易历史
  getTransactionHistory: async (materialCode: string): Promise<MaterialTransaction[]> => {
    const response = await api.get(`/material/history/${materialCode}`);
    return response.data.data || [];
  },

  // 获取入库交易记录
  getInStockTransactions: async (): Promise<MaterialTransaction[]> => {
    const response = await api.get('/material/transactions/instock');
    return response.data.data || [];
  },

  // 获取出库交易记录
  getOutStockTransactions: async (): Promise<MaterialTransaction[]> => {
    const response = await api.get('/material/transactions/outstock');
    return response.data.data || [];
  },

  // 获取低库存物料
  getLowStockMaterials: async (): Promise<Material[]> => {
    const response = await api.get('/material/low-stock');
    return response.data.data || [];
  },
};
