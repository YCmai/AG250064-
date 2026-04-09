<template>
  <div class="material-container">
    <a-row :gutter="16">
      <a-col :xs="24" :sm="12" :md="6">
        <a-statistic title="物料总数" :value="materialStore.materialCount" />
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-statistic title="低库存物料" :value="materialStore.lowStockCount" :value-style="{ color: '#cf1322' }" />
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-statistic title="总库存数量" :value="materialStore.totalQuantity" />
      </a-col>
    </a-row>

    <a-card title="物料管理" :bordered="false" style="margin-top: 20px">
      <template #extra>
        <a-space>
          <a-button type="primary" @click="activeTab = 'instock'">
            <template #icon><ArrowUpOutlined /></template>
            入库
          </a-button>
          <a-button type="primary" @click="activeTab = 'outstock'">
            <template #icon><ArrowDownOutlined /></template>
            出库
          </a-button>
        </a-space>
      </template>

      <a-tabs v-model:activeKey="activeTab">
        <a-tab-pane key="materials" tab="物料列表">
          <a-input-search
            v-model:value="searchText"
            placeholder="搜索物料编码或名称"
            style="margin-bottom: 16px; width: 200px"
            @search="handleSearch"
          />
          <a-table
            :columns="materialColumns"
            :data-source="filteredMaterials"
            :loading="materialStore.loading"
            :pagination="{ pageSize: 10 }"
            rowKey="id"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'quantity'">
                <a-tag :color="record.quantity > (record.minStock || 0) ? 'green' : 'red'">
                  {{ record.quantity }}
                </a-tag>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-space>
                  <a-button type="link" size="small" @click="viewHistory(record)">
                    历史
                  </a-button>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="instock" tab="入库">
          <a-form :model="instockForm" layout="vertical" @finish="handleInStock">
            <a-row :gutter="16">
              <a-col :xs="24" :md="12">
                <a-form-item label="物料编码" required>
                  <a-select
                    v-model:value="instockForm.materialCode"
                    placeholder="请选择物料"
                    @change="onMaterialChange"
                  >
                    <a-select-option v-for="m in materialStore.materials" :key="m.code" :value="m.code">
                      {{ m.code }} - {{ m.name }}
                    </a-select-option>
                  </a-select>
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item label="入库数量" required>
                  <a-input-number v-model:value="instockForm.quantity" placeholder="请输入数量" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :xs="24" :md="12">
                <a-form-item label="储位编码">
                  <a-input v-model:value="instockForm.locationCode" placeholder="请输入储位编码" />
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item label="操作员">
                  <a-input v-model:value="instockForm.operatorName" placeholder="请输入操作员名称" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-form-item label="备注">
              <a-textarea v-model:value="instockForm.remark" placeholder="请输入备注" />
            </a-form-item>
            <a-form-item>
              <a-button type="primary" html-type="submit">确认入库</a-button>
            </a-form-item>
          </a-form>
        </a-tab-pane>

        <a-tab-pane key="outstock" tab="出库">
          <a-form :model="outstockForm" layout="vertical" @finish="handleOutStock">
            <a-row :gutter="16">
              <a-col :xs="24" :md="12">
                <a-form-item label="物料编码" required>
                  <a-select
                    v-model:value="outstockForm.materialCode"
                    placeholder="请选择物料"
                  >
                    <a-select-option v-for="m in materialStore.materials" :key="m.code" :value="m.code">
                      {{ m.code }} - {{ m.name }}
                    </a-select-option>
                  </a-select>
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item label="出库数量" required>
                  <a-input-number v-model:value="outstockForm.quantity" placeholder="请输入数量" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :xs="24" :md="12">
                <a-form-item label="出库原因" required>
                  <a-select v-model:value="outstockForm.outReason" placeholder="请选择出库原因">
                    <a-select-option value="销售">销售</a-select-option>
                    <a-select-option value="报废">报废</a-select-option>
                    <a-select-option value="调拨">调拨</a-select-option>
                    <a-select-option value="其他">其他</a-select-option>
                  </a-select>
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item label="操作员">
                  <a-input v-model:value="outstockForm.operatorName" placeholder="请输入操作员名称" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-form-item label="备注">
              <a-textarea v-model:value="outstockForm.remark" placeholder="请输入备注" />
            </a-form-item>
            <a-form-item>
              <a-button type="primary" html-type="submit">确认出库</a-button>
            </a-form-item>
          </a-form>
        </a-tab-pane>

        <a-tab-pane key="transactions" tab="交易记录">
          <a-button type="primary" @click="loadTransactions" style="margin-bottom: 16px">
            刷新记录
          </a-button>
          <a-table
            :columns="transactionColumns"
            :data-source="materialStore.transactions"
            :loading="materialStore.loading"
            :pagination="{ pageSize: 10 }"
            rowKey="id"
          />
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <!-- 交易历史模态框 -->
    <a-modal
      v-model:open="showHistoryModal"
      title="交易历史"
      width="800px"
      :footer="null"
    >
      <a-table
        :columns="historyColumns"
        :data-source="transactionHistory"
        :loading="materialStore.loading"
        :pagination="{ pageSize: 10 }"
        rowKey="id"
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { message } from 'ant-design-vue';
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons-vue';
import { useMaterialStore } from '../stores/material';
import type { Material } from '../services/material';

const materialStore = useMaterialStore();
const activeTab = ref('materials');
const searchText = ref('');
const showHistoryModal = ref(false);
const transactionHistory = ref([]);

const instockForm = ref({
  materialCode: '',
  quantity: 0,
  locationCode: '',
  operatorName: '',
  remark: '',
});

const outstockForm = ref({
  materialCode: '',
  quantity: 0,
  outReason: '',
  operatorName: '',
  remark: '',
});

const materialColumns = [
  { title: '物料编码', dataIndex: 'code', key: 'code' },
  { title: '物料名称', dataIndex: 'name', key: 'name' },
  { title: '规格', dataIndex: 'specification', key: 'specification' },
  { title: '单位', dataIndex: 'unit', key: 'unit' },
  { title: '库存数量', dataIndex: 'quantity', key: 'quantity' },
  { title: '最小库存', dataIndex: 'minStock', key: 'minStock' },
  { title: '操作', key: 'action', width: 100 },
];

const transactionColumns = [
  { title: '物料编码', dataIndex: 'materialCode', key: 'materialCode' },
  { title: '数量', dataIndex: 'quantity', key: 'quantity' },
  { title: '类型', dataIndex: 'type', key: 'type' },
  { title: '储位', dataIndex: 'locationCode', key: 'locationCode' },
  { title: '操作员', dataIndex: 'operatorName', key: 'operatorName' },
  { title: '时间', dataIndex: 'createTime', key: 'createTime' },
];

const historyColumns = [
  { title: '数量', dataIndex: 'quantity', key: 'quantity' },
  { title: '类型', dataIndex: 'type', key: 'type' },
  { title: '储位', dataIndex: 'locationCode', key: 'locationCode' },
  { title: '操作员', dataIndex: 'operatorName', key: 'operatorName' },
  { title: '时间', dataIndex: 'createTime', key: 'createTime' },
];

const filteredMaterials = computed(() => {
  if (!searchText.value) return materialStore.materials;
  return materialStore.materials.filter(m =>
    m.code.includes(searchText.value) || m.name.includes(searchText.value)
  );
});

onMounted(() => {
  materialStore.fetchMaterials();
  materialStore.fetchLowStockMaterials();
});

const handleSearch = () => {
  // 搜索已在computed中处理
};

const onMaterialChange = () => {
  // 可以在这里添加物料变更时的逻辑
};

const handleInStock = async () => {
  try {
    await materialStore.inStock(instockForm.value);
    message.success('入库成功');
    instockForm.value = {
      materialCode: '',
      quantity: 0,
      locationCode: '',
      operatorName: '',
      remark: '',
    };
    activeTab.value = 'materials';
  } catch (err) {
    message.error('入库失败');
  }
};

const handleOutStock = async () => {
  try {
    await materialStore.outStock(outstockForm.value);
    message.success('出库成功');
    outstockForm.value = {
      materialCode: '',
      quantity: 0,
      outReason: '',
      operatorName: '',
      remark: '',
    };
    activeTab.value = 'materials';
  } catch (err) {
    message.error('出库失败');
  }
};

const viewHistory = async (material: Material) => {
  try {
    transactionHistory.value = await materialStore.getTransactionHistory(material.code);
    showHistoryModal.value = true;
  } catch (err) {
    message.error('获取历史记录失败');
  }
};

const loadTransactions = async () => {
  try {
    await materialStore.fetchTransactions('InStock');
    message.success('已刷新');
  } catch (err) {
    message.error('刷新失败');
  }
};
</script>

<style scoped>
.material-container {
  padding: 20px;
}
</style>
