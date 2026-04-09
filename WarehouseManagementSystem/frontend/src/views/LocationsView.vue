<template>
  <div class="locations-container">
    <h1>{{ $t('location.title') }}</h1>
    <a-space style="margin-bottom: 16px">
      <a-input
        v-model:value="searchText"
        :placeholder="$t('location.searchPlaceholder')"
        style="width: 200px"
      />
      <a-button type="primary" @click="handleSearch">{{ $t('common.search') }}</a-button>
      <a-button type="primary" @click="handleCreateLocation">
        <template #icon>
          <PlusOutlined />
        </template>
        {{ $t('location.add') }}
      </a-button>
      <a-upload
        :before-upload="handleBeforeUpload"
        :show-upload-list="false"
        accept=".xlsx,.xls"
      >
        <a-button type="default">
          <template #icon>
            <UploadOutlined />
          </template>
          {{ $t('location.batchImport') }}
        </a-button>
      </a-upload>
      <a-button type="default" @click="handleExportTemplate">
        <template #icon>
          <DownloadOutlined />
        </template>
        {{ $t('location.downloadTemplate') }}
      </a-button>
    </a-space>

    <BatchOperationToolbar
      :selected-ids="selectedIds"
      :locations="locationStore.locations"
      @clear-selection="handleClearSelection"
      @refresh="fetchLocations"
    />

    <a-table
      :columns="columns"
      :data-source="locationStore.locations"
      :loading="locationStore.isLoading"
      :pagination="{
        current: locationStore.page,
        pageSize: locationStore.pageSize,
        total: locationStore.total,
        onChange: (page, size) => {
          locationStore.setPage(page)
          locationStore.setPageSize(size)
          fetchLocations()
        },
        onShowSizeChange: (current, size) => {
          locationStore.setPage(1)
          locationStore.setPageSize(size)
          fetchLocations()
        },
        showSizeChanger: true,
        pageSizeOptions: ['20', '50', '100'],
        showTotal: (total, range) => `${range[0]}-${range[1]} / ${total}`,
      }"
      :row-key="(record) => record.id"
      :on-row="(record) => ({ onClick: () => handleRowClick(record) })"
      style="cursor: pointer"
      :row-selection="{
        selectedRowKeys: selectedIds,
        onChange: handleSelectionChange,
      }"
      :scroll="{ x: 2000, y: 'calc(100vh - 280px)' }"
      size="middle"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isEmpty'">
          <a-tag :color="record.isEmpty ? '#52c41a' : '#faad14'">
            {{ record.isEmpty ? $t('location.yes') : $t('location.no') }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'lock'">
          <a-tag :color="record.lock ? '#f5222d' : '#52c41a'">
            {{ record.lock ? $t('location.yes') : $t('location.no') }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'enabled'">
          <a-tag :color="record.enabled ? '#52c41a' : '#8c8c8c'">
            {{ record.enabled ? $t('location.yes') : $t('location.no') }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'materialCode'">
          <span :style="{ color: record.materialCode ? '#faad14' : '#d9d9d9', fontWeight: record.materialCode ? 'bold' : 'normal' }">
            {{ record.materialCode || '-' }}
          </span>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button
              type="link"
              size="small"
              @click.stop="handleEditLocation(record)"
            >
              {{ $t('common.edit') }}
            </a-button>
            <a-button
              type="primary"
              size="small"
              @click.stop="handleClearMaterial(record.id)"
              :disabled="record.isEmpty"
            >
              {{ $t('location.clearMaterial') }}
            </a-button>
            <a-button
              size="small"
              @click.stop="handleToggleLock(record)"
            >
              {{ record.lock ? $t('location.unlock') : $t('location.lock') }}
            </a-button>
            <a-button
              size="small"
              danger
              @click.stop="handleDeleteLocation(record)"
            >
              {{ $t('common.delete') }}
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <LocationDetailModal
      v-model="showDetailModal"
      :location="selectedLocation"
      @refresh="fetchLocations"
    />

    <LocationImportPreviewModal
      v-model="showImportPreview"
      :preview-data="importPreviewData"
      @confirm="handleConfirmImport"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useLocationStore } from '@/stores/location'
import { Location } from '@/services/location'
import locationService from '@/services/location'
import LocationDetailModal from '@/components/LocationDetailModal.vue'
import BatchOperationToolbar from '@/components/BatchOperationToolbar.vue'
import LocationImportPreviewModal from '@/components/LocationImportPreviewModal.vue'
import { PlusOutlined, UploadOutlined, DownloadOutlined } from '@ant-design/icons-vue'
import { message, Modal } from 'ant-design-vue'
import * as XLSX from 'xlsx'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const router = useRouter()
const locationStore = useLocationStore()
const searchText = ref('')
const showDetailModal = ref(false)
const selectedLocation = ref<Location | null>(null)
const selectedIds = ref<number[]>([])
const showImportPreview = ref(false)
const importPreviewData = ref<any[]>([])

const columns = computed(() => [
  {
    title: t('location.name'),
    dataIndex: 'name',
    key: 'name',
    width: 120,
    fixed: 'left',
  },
  {
    title: t('location.nodeRemark'),
    dataIndex: 'nodeRemark',
    key: 'nodeRemark',
    width: 120,
  },
  {
    title: t('location.group'),
    dataIndex: 'group',
    key: 'group',
    width: 100,
  },
  {
    title: t('location.isEmpty'),
    dataIndex: 'isEmpty',
    key: 'isEmpty',
    width: 90,
  },
  {
    title: t('location.isLocked'),
    dataIndex: 'lock',
    key: 'lock',
    width: 90,
  },
  {
    title: t('location.isEnabled'),
    dataIndex: 'enabled',
    key: 'enabled',
    width: 90,
  },
  {
    title: t('location.materialCode'),
    dataIndex: 'materialCode',
    key: 'materialCode',
    width: 120,
  },
  {
    title: t('location.palletId'),
    dataIndex: 'palletID',
    key: 'palletID',
    width: 100,
  },
  {
    title: t('location.weight'),
    dataIndex: 'weight',
    key: 'weight',
    width: 80,
  },
  {
    title: t('location.quantity'),
    dataIndex: 'quanitity',
    key: 'quanitity',
    width: 80,
  },
  {
    title: t('location.entryDate'),
    dataIndex: 'entryDate',
    key: 'entryDate',
    width: 150,
  },
  {
    title: t('location.liftingHeight'),
    dataIndex: 'liftingHeight',
    key: 'liftingHeight',
    width: 100,
  },
  {
    title: t('location.unloadHeight'),
    dataIndex: 'unloadHeight',
    key: 'unloadHeight',
    width: 100,
  },
  {
    title: t('location.depth'),
    dataIndex: 'depth',
    key: 'depth',
    width: 80,
  },
  {
    title: t('location.waitingNode'),
    dataIndex: 'wattingNode',
    key: 'wattingNode',
    width: 100,
  },
  {
    title: t('common.operation'),
    key: 'action',
    width: 280,
    fixed: 'right',
  },
])

onMounted(() => {
  fetchLocations()
})

const fetchLocations = async () => {
  locationStore.setLoading(true)
  try {
    const response = await locationService.getLocations(
      searchText.value,
      locationStore.page,
      locationStore.pageSize
    )
    if (response.success && response.data) {
      locationStore.setLocations(response.data.items, response.data.total)
    } else {
      message.error(response.message || t('common.fail'))
    }
  } catch (error: any) {
    message.error(error.message || t('common.fail'))
  } finally {
    locationStore.setLoading(false)
  }
}

const handleSearch = () => {
  locationStore.setPage(1)
  fetchLocations()
}

const handleClearMaterial = (id: number) => {
  Modal.confirm({
    title: t('location.confirmClear'),
    content: t('location.confirmClearContent'),
    okText: t('common.confirm'),
    cancelText: t('common.cancel'),
    onOk: async () => {
      try {
        const response = await locationService.clearMaterial(id)
        if (response.success) {
          message.success(t('common.success'))
          fetchLocations()
        } else {
          message.error(response.message || t('common.fail'))
        }
      } catch (error: any) {
        message.error(error.message || t('common.fail'))
      }
    },
  })
}

const handleRowClick = (record: Location) => {
  selectedLocation.value = record
  showDetailModal.value = true
}

const handleSelectionChange = (selectedRowKeys: number[]) => {
  selectedIds.value = selectedRowKeys
}

const handleClearSelection = () => {
  selectedIds.value = []
}

const handleCreateLocation = () => {
  router.push('/locations/create')
}

const handleEditLocation = (record: Location) => {
  router.push(`/locations/${record.id}/edit`)
}

const handleToggleLock = async (record: Location) => {
  try {
    const response = await locationService.toggleLock(record.id, !record.lock)
    if (response.success) {
      message.success(response.message || t('common.success'))
      // 立即刷新数据
      await fetchLocations()
    } else {
      message.error(response.message || t('common.fail'))
    }
  } catch (error: any) {
    message.error(error.message || t('common.fail'))
  }
}

const handleDeleteLocation = (record: Location) => {
  Modal.confirm({
    title: t('location.confirmDelete'),
    content: t('location.confirmDeleteContent', { name: record.name, remark: record.nodeRemark }),
    okText: t('common.delete'),
    cancelText: t('common.cancel'),
    okType: 'danger',
    onOk: async () => {
      try {
        const response = await locationService.deleteLocation(record.id)
        if (response.success) {
          message.success(response.message || t('common.success'))
          // 立即刷新数据
          await fetchLocations()
        } else {
          message.error(response.message || t('common.fail'))
        }
      } catch (error: any) {
        message.error(error.message || t('common.fail'))
      }
    },
  })
}

const handleBeforeUpload = (file: File) => {
  const reader = new FileReader()
  reader.onload = async (e) => {
    try {
      const data = new Uint8Array(e.target?.result as ArrayBuffer)
      const workbook = XLSX.read(data, { type: 'array' })
      const firstSheet = workbook.Sheets[workbook.SheetNames[0]]
      const jsonData: any[] = XLSX.utils.sheet_to_json(firstSheet)
      
      if (jsonData.length === 0) {
        message.error(t('common.fail'))
        return
      }
      
      // 用于检测重复的节点备注
      const nodeRemarkSet = new Set<string>()
      const duplicateNodeRemarks = new Set<string>()
      
      // Helper to get value from either English or Chinese column header
      const getVal = (row: any, enKey: string, zhKey: string) => {
        return row[enKey] !== undefined ? row[enKey] : row[zhKey];
      }

      // 验证并转换数据
      const previewData = jsonData.map((row: any, index: number) => {
        const errors: string[] = []
        
        const mapNodeVal = getVal(row, 'Map Node', '地图节点');
        const nodeRemarkVal = getVal(row, 'Node Remark', '节点备注');
        const groupVal = getVal(row, 'Group', '分组');
        const waitingNodeVal = getVal(row, 'Waiting Node', '挂作点');
        const liftingHeightVal = getVal(row, 'Lifting Height', '举升高度');
        const unloadHeightVal = getVal(row, 'Unload Height', '卸载高度');
        const depthVal = getVal(row, 'Depth', '储位深度');

        // 验证必填字段
        if (!mapNodeVal || String(mapNodeVal).trim() === '') {
          errors.push(t('location.nameRequired'))
        }
        
        const nodeRemark = String(nodeRemarkVal || '').trim()
        if (!nodeRemark) {
          errors.push(t('location.nodeRemarkRequired'))
        } else {
          // 检查节点备注是否重复
          if (nodeRemarkSet.has(nodeRemark)) {
            duplicateNodeRemarks.add(nodeRemark)
            errors.push(t('location.nodeRemark') + ' ' + t('common.fail')) // Duplicate
          } else {
            nodeRemarkSet.add(nodeRemark)
          }
        }
        
        if (!groupVal || String(groupVal).trim() === '') {
          errors.push(t('location.groupRequired'))
        }
        
        // 转换数据
        const locationData = {
          name: String(mapNodeVal || '').trim(),
          nodeRemark: nodeRemark,
          group: String(groupVal || '').trim(),
          wattingNode: String(waitingNodeVal || '').trim(),
          liftingHeight: parseInt(liftingHeightVal) || 0,
          unloadHeight: parseInt(unloadHeightVal) || 0,
          depth: parseInt(depthVal) || 0,
          lock: false,
          enabled: true,
          materialCode: null,
          palletID: '0',
          weight: '0',
          quanitity: '0',
          entryDate: null,
        }
        
        return {
          rowNumber: index + 2, // Excel行号从2开始（第1行是表头）
          data: locationData,
          errors: errors,
        }
      })
      
      // 标记所有重复的节点备注
      if (duplicateNodeRemarks.size > 0) {
        previewData.forEach(item => {
          if (duplicateNodeRemarks.has(item.data.nodeRemark) && 
              !item.errors.some(e => e.includes(t('location.nodeRemark')))) {
            item.errors.push(t('location.nodeRemark') + ' ' + t('common.fail'))
          }
        })
      }
      
      // 显示预览对话框
      importPreviewData.value = previewData
      showImportPreview.value = true
      
    } catch (error: any) {
      message.error(t('common.fail') + ': ' + error.message)
    }
  }
  reader.readAsArrayBuffer(file)
  return false // 阻止自动上传
}

// ... (handleConfirmImport omitted for brevity as it doesn't need changes mostly) ...

const handleExportTemplate = () => {
  // 创建模板数据 - 根据实际需求
  // Use computed keys for the template to support i18n
  const row1 = {};
  row1[t('location.mapNode')] = 'A001';
  row1[t('location.nodeRemark')] = 'A-01';
  row1[t('location.waitingNode')] = 'OP001';
  row1[t('location.group')] = 'A';
  row1[t('location.liftingHeight')] = 100;
  row1[t('location.unloadHeight')] = 50;
  row1[t('location.depth')] = 200;

  const row2 = {};
  row2[t('location.mapNode')] = 'A002';
  row2[t('location.nodeRemark')] = 'A-02';
  row2[t('location.waitingNode')] = 'OP002';
  row2[t('location.group')] = 'A';
  row2[t('location.liftingHeight')] = 100;
  row2[t('location.unloadHeight')] = 50;
  row2[t('location.depth')] = 200;

  const template = [row1, row2];
  
  const ws = XLSX.utils.json_to_sheet(template)
  
  // 设置列宽
  ws['!cols'] = [
    { wch: 12 }, // mapNode
    { wch: 15 }, // nodeRemark
    { wch: 12 }, // waitingNode
    { wch: 10 }, // group
    { wch: 12 }, // liftingHeight
    { wch: 12 }, // unloadHeight
    { wch: 12 }, // depth
  ]
  
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, t('location.title'))
  XLSX.writeFile(wb, `LocationTemplate_${new Date().getTime()}.xlsx`)
  message.success(t('location.downloadSuccess'))
}
</script>

<style scoped>
.locations-container {
  width: 100%;
}

.locations-container h1 {
  margin-bottom: 24px;
  font-size: 24px;
  font-weight: 600;
}
</style>
