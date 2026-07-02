<template>
  <div class="locations-container">
    <h1>{{ $t('location.title') }}</h1>
    <a-space style="margin-bottom: 16px">
      <a-input
        v-model:value="searchText"
        :placeholder="$t('location.searchPlaceholder')"
        style="width: 220px"
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
        onShowSizeChange: (_current, size) => {
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
      :scroll="{ x: 2200, y: 'calc(100vh - 280px)' }"
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
              @click.stop="handleToggleEnabled(record)"
            >
              {{ record.enabled ? $t('location.disable') : $t('location.enable') }}
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
      :importing="isImporting"
      @confirm="handleConfirmImport"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { DownloadOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons-vue'
import { message, Modal } from 'ant-design-vue'
import * as XLSX from 'xlsx'
import { useI18n } from 'vue-i18n'
import { useLocationStore } from '@/stores/location'
import locationService, { Location } from '@/services/location'
import BatchOperationToolbar from '@/components/BatchOperationToolbar.vue'
import LocationDetailModal from '@/components/LocationDetailModal.vue'
import LocationImportPreviewModal from '@/components/LocationImportPreviewModal.vue'

const { t } = useI18n()
const router = useRouter()
const locationStore = useLocationStore()
const searchText = ref('')
const showDetailModal = ref(false)
const selectedLocation = ref<Location | null>(null)
const selectedIds = ref<number[]>([])
const showImportPreview = ref(false)
const isImporting = ref(false)
const importPreviewData = ref<Array<{ rowNumber: number; data: Partial<Location>; errors: string[] }>>([])

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
    title: t('location.laneCode'),
    dataIndex: 'laneCode',
    key: 'laneCode',
    width: 100,
  },
  {
    title: t('location.depthIndex'),
    dataIndex: 'depthIndex',
    key: 'depthIndex',
    width: 100,
  },
  {
    title: t('location.waitingNode'),
    dataIndex: 'wattingNode',
    key: 'wattingNode',
    width: 140,
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
    title: t('common.operation'),
    key: 'action',
    width: 360,
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
      await fetchLocations()
    } else {
      message.error(response.message || t('common.fail'))
    }
  } catch (error: any) {
    message.error(error.message || t('common.fail'))
  }
}

const handleToggleEnabled = async (record: Location) => {
  try {
    const response = await locationService.toggleEnabled(record.id, !record.enabled)
    if (response.success) {
      message.success(response.message || t('common.success'))
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
    title: t('location.deleteConfirm'),
    content: t('location.confirmDeleteContent', { name: record.name, remark: record.nodeRemark }),
    okText: t('common.delete'),
    cancelText: t('common.cancel'),
    okType: 'danger',
    onOk: async () => {
      try {
        const response = await locationService.deleteLocation(record.id)
        if (response.success) {
          message.success(response.message || t('common.success'))
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

const buildImportErrors = (locationData: Partial<Location>, rowNumber: number) => {
  const errors: string[] = []

  if (!locationData.name) {
    errors.push(t('location.nameRequired'))
  }

  if (!locationData.nodeRemark) {
    errors.push(t('location.nodeRemarkRequired'))
  }

  if (!locationData.group) {
    errors.push(t('location.groupRequired'))
  }

  if (!locationData.laneCode) {
    errors.push(t('location.laneCodeRequired'))
  }

  if (!locationData.depthIndex || locationData.depthIndex <= 0) {
    errors.push(t('location.depthIndexPositive'))
  }

  return {
    rowNumber,
    data: locationData,
    errors,
  }
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

      const nodeRemarkSet = new Set<string>()
      const duplicateNodeRemarks = new Set<string>()
      const getVal = (row: any, enKey: string, zhKey: string) => (row[enKey] !== undefined ? row[enKey] : row[zhKey])

      const previewData = jsonData.map((row: any, index: number) => {
        const nodeRemark = String(getVal(row, 'Node Remark', '节点备注') || '').trim()
        if (nodeRemark) {
          if (nodeRemarkSet.has(nodeRemark)) {
            duplicateNodeRemarks.add(nodeRemark)
          } else {
            nodeRemarkSet.add(nodeRemark)
          }
        }

        const locationData: Partial<Location> = {
          name: String(getVal(row, 'Map Node', '地图节点') || '').trim(),
          nodeRemark,
          group: String(getVal(row, 'Group', '分组') || '').trim(),
          laneCode: String(getVal(row, 'Lane Code', '库道编号') || '').trim(),
          depthIndex: parseInt(String(getVal(row, 'Depth Index', '深度序号') || '0'), 10) || 0,
          wattingNode: String(getVal(row, 'Signal Request Point', '信号请求点') || '').trim(),
          liftingHeight: parseInt(String(getVal(row, 'Lifting Height', '举升高度') || '0'), 10) || 0,
          unloadHeight: parseInt(String(getVal(row, 'Unload Height', '卸载高度') || '0'), 10) || 0,
          lock: false,
          enabled: true,
          materialCode: null as any,
          palletID: '0',
          weight: '0',
          quanitity: '0',
          entryDate: null as any,
        }

        const previewItem = buildImportErrors(locationData, index + 2)
        return previewItem
      })

      if (duplicateNodeRemarks.size > 0) {
        previewData.forEach((item) => {
          if (item.data.nodeRemark && duplicateNodeRemarks.has(item.data.nodeRemark)) {
            item.errors.push(`${t('location.nodeRemark')} ${t('common.fail')}`)
          }
        })
      }

      importPreviewData.value = previewData
      showImportPreview.value = true
    } catch (error: any) {
      message.error(`${t('common.fail')}: ${error.message}`)
    }
  }

  reader.readAsArrayBuffer(file)
  return false
}

const handleConfirmImport = async (validData: Partial<Location>[]) => {
  if (validData.length === 0) {
    message.warning(t('location.invalidData'))
    return
  }

  isImporting.value = true
  try {
    const response = await locationService.batchImport(validData)
    if (response.success) {
      const failCount = response.data?.failCount || 0
      if (failCount > 0) {
        const errors = response.data?.errors?.join('；') || response.message
        message.warning(errors)
      } else {
        message.success(response.message || t('common.success'))
      }

      if (failCount === 0) {
        showImportPreview.value = false
        importPreviewData.value = []
      }

      await fetchLocations()
    } else {
      message.error(response.message || t('common.fail'))
    }
  } catch (error: any) {
    message.error(error.message || t('common.fail'))
  } finally {
    isImporting.value = false
  }
}

const handleExportTemplate = () => {
  const row1: Record<string, string | number> = {}
  row1[t('location.mapNode')] = 'A001'
  row1[t('location.nodeRemark')] = 'A-01'
  row1[t('location.group')] = 'A'
  row1[t('location.laneCode')] = 'A-L01'
  row1[t('location.depthIndex')] = 1
  row1[t('location.waitingNode')] = 'REQ001'
  row1[t('location.liftingHeight')] = 100
  row1[t('location.unloadHeight')] = 50

  const row2: Record<string, string | number> = {}
  row2[t('location.mapNode')] = 'A002'
  row2[t('location.nodeRemark')] = 'A-02'
  row2[t('location.group')] = 'A'
  row2[t('location.laneCode')] = 'A-L01'
  row2[t('location.depthIndex')] = 2
  row2[t('location.waitingNode')] = 'REQ002'
  row2[t('location.liftingHeight')] = 100
  row2[t('location.unloadHeight')] = 50

  const template = [row1, row2]
  const ws = XLSX.utils.json_to_sheet(template)
  ws['!cols'] = [
    { wch: 12 },
    { wch: 15 },
    { wch: 10 },
    { wch: 12 },
    { wch: 12 },
    { wch: 16 },
    { wch: 12 },
    { wch: 12 },
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
