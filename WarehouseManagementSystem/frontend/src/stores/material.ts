import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { materialService, Material, MaterialTransaction } from '../services/material';

export const useMaterialStore = defineStore('material', () => {
  const materials = ref<Material[]>([]);
  const transactions = ref<MaterialTransaction[]>([]);
  const lowStockMaterials = ref<Material[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const materialCount = computed(() => materials.value.length);
  const lowStockCount = computed(() => lowStockMaterials.value.length);
  const totalQuantity = computed(() =>
    materials.value.reduce((sum, m) => sum + m.quantity, 0)
  );

  const fetchMaterials = async () => {
    loading.value = true;
    error.value = null;
    try {
      materials.value = await materialService.getAllMaterials();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取物料列表失败';
    } finally {
      loading.value = false;
    }
  };

  const fetchTransactions = async (type: 'InStock' | 'OutStock') => {
    loading.value = true;
    error.value = null;
    try {
      if (type === 'InStock') {
        transactions.value = await materialService.getInStockTransactions();
      } else {
        transactions.value = await materialService.getOutStockTransactions();
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取交易记录失败';
    } finally {
      loading.value = false;
    }
  };

  const fetchLowStockMaterials = async () => {
    try {
      lowStockMaterials.value = await materialService.getLowStockMaterials();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取低库存物料失败';
    }
  };

  const getMaterialByCode = async (code: string) => {
    try {
      return await materialService.getMaterialByCode(code);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取物料信息失败';
      throw err;
    }
  };

  const inStock = async (data: {
    materialCode: string;
    quantity: number;
    locationCode?: string;
    operatorName?: string;
    remark?: string;
  }) => {
    try {
      await materialService.inStock(data);
      await fetchMaterials();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '入库操作失败';
      throw err;
    }
  };

  const outStock = async (data: {
    materialCode: string;
    quantity: number;
    locationCode?: string;
    outReason?: string;
    operatorName?: string;
    remark?: string;
  }) => {
    try {
      await materialService.outStock(data);
      await fetchMaterials();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '出库操作失败';
      throw err;
    }
  };

  const getTransactionHistory = async (materialCode: string) => {
    try {
      return await materialService.getTransactionHistory(materialCode);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取交易历史失败';
      throw err;
    }
  };

  const clearError = () => {
    error.value = null;
  };

  return {
    materials,
    transactions,
    lowStockMaterials,
    loading,
    error,
    materialCount,
    lowStockCount,
    totalQuantity,
    fetchMaterials,
    fetchTransactions,
    fetchLowStockMaterials,
    getMaterialByCode,
    inStock,
    outStock,
    getTransactionHistory,
    clearError,
  };
});
