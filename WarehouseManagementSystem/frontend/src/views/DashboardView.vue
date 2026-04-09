<template>
  <div class="dashboard-container">
    <h1>{{ $t('dashboard.title') }}</h1>
    
    <!-- 统计图表区域 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :span="12">
        <a-card :title="$t('dashboard.statusDistribution')" :bordered="false" size="small" :bodyStyle="{ padding: '10px' }">
          <div class="chart-container">
            <div ref="locationChartRef" class="chart-box"></div>
            <div class="chart-info">
              <div class="info-item">
                <span class="label">{{ $t('dashboard.available') }}</span>
                <span class="value success">{{ stats.available }}</span>
              </div>
              <div class="info-item">
                <span class="label">{{ $t('dashboard.occupied') }}</span>
                <span class="value warning">{{ stats.occupied }}</span>
              </div>
              <div class="info-item">
                <span class="label">{{ $t('dashboard.locked') }}</span>
                <span class="value error">{{ stats.locked }}</span>
              </div>
              <div class="info-item">
                <span class="label">{{ $t('dashboard.disabled') }}</span>
                <span class="value disabled">{{ stats.disabled }}</span>
              </div>
              <div class="info-item total">
                <span class="label">{{ $t('dashboard.total') }}</span>
                <span class="value">{{ stats.total }}</span>
              </div>
            </div>
          </div>
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card :title="$t('dashboard.taskStats')" :bordered="false" size="small" :bodyStyle="{ padding: '10px' }">
          <div class="chart-container">
            <div ref="taskChartRef" class="chart-box"></div>
            <div class="chart-info">
              <div class="info-item">
                <span class="label">{{ $t('dashboard.totalTasks') }}</span>
                <span class="value primary">{{ taskStats.totalTasks }}</span>
              </div>
              <div class="info-item">
                <span class="label">{{ $t('dashboard.completed') }}</span>
                <span class="value success">{{ taskStats.completedTasks }}</span>
              </div>
              <div class="info-item">
                <span class="label">{{ $t('dashboard.running') }}</span>
                <span class="value warning">{{ taskStats.runningTasks }}</span>
              </div>
              <div class="info-item">
                <span class="label">{{ $t('dashboard.waiting') }}</span>
                <span class="value primary">{{ taskStats.waitingTasks }}</span>
              </div>
              <div class="info-item">
                <span class="label">{{ $t('dashboard.canceled') }}</span>
                <span class="value disabled">{{ taskStats.canceledTasks }}</span>
              </div>
              <div class="info-item total">
                <span class="label">{{ $t('dashboard.completionRate') }}</span>
                <span class="value">{{ taskStats.completionRate }}%</span>
              </div>
            </div>
          </div>
        </a-card>
      </a-col>
    </a-row>

    <!-- 搜索和操作栏 -->
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="searchText"
          :placeholder="$t('dashboard.searchPlaceholder')"
          style="width: 250px"
          size="small"
        />
        <a-button type="primary" size="small" @click="handleSearch">{{ $t('common.search') }}</a-button>
        <a-select
          v-model:value="selectedGroup"
          :placeholder="$t('dashboard.selectGroup')"
          style="width: 150px"
          size="small"
          allow-clear
          @change="handleGroupChange"
        >
          <a-select-option v-for="group in groups" :key="group" :value="group">
            {{ group }}
          </a-select-option>
        </a-select>

        <a-select
          v-model:value="selectedStatus"
          :placeholder="$t('dashboard.selectStatus')"
          style="width: 120px"
          size="small"
          allow-clear
          @change="handleStatusChange"
        >
          <a-select-option value="available">{{ $t('dashboard.available') }}</a-select-option>
          <a-select-option value="occupied">{{ $t('dashboard.occupied') }}</a-select-option>
          <a-select-option value="locked">{{ $t('dashboard.locked') }}</a-select-option>
          <a-select-option value="disabled">{{ $t('dashboard.disabled') }}</a-select-option>
        </a-select>
        
        <a-button size="small" @click="refreshData">{{ $t('common.reset') }}</a-button>

        <a-radio-group v-model:value="viewMode" size="small" button-style="solid">
          <a-radio-button value="card">{{ $t('dashboard.cardView') }}</a-radio-button>
          <a-radio-button value="map">{{ $t('dashboard.mapView') }}</a-radio-button>
        </a-radio-group>

        <!-- 布局方向和数量设置，暂时保留但不作为默认 -->
        <template v-if="false"> 
          <a-divider type="vertical" />
          <a-space size="small" align="center">
              <span>布局:</span>
              <a-radio-group v-model:value="layoutDirection" size="small" button-style="solid">
                  <a-radio-button value="row">横向</a-radio-button>
                  <a-radio-button value="column">纵向</a-radio-button>
              </a-radio-group>
              
              <template v-if="layoutDirection === 'column'">
                  <span>每列:</span>
                  <a-input-number 
                      v-model:value="itemsPerColumn" 
                      size="small" 
                      :min="1" 
                      :max="50" 
                      style="width: 60px"
                  />
              </template>
          </a-space>
        </template>
      </a-space>

      <a-space>
        <a-button type="primary" size="small" @click="handleBatchClearMaterial" :disabled="selectedLocationIds.length === 0">
          {{ $t('dashboard.batchClear') }}
        </a-button>
        <a-button size="small" @click="handleBatchToggleLock(true)" :disabled="selectedLocationIds.length === 0">
          {{ $t('dashboard.batchLock') }}
        </a-button>
        <a-button size="small" @click="handleBatchToggleLock(false)" :disabled="selectedLocationIds.length === 0">
          {{ $t('dashboard.batchUnlock') }}
        </a-button>
      </a-space>
    </div>

    <!-- 选择提示 -->
    <a-alert
      v-if="selectedLocationIds.length > 0"
      :message="$t('dashboard.selectedCount', { n: selectedLocationIds.length })"
      type="info"
      closable
      banner
      @close="selectedLocationIds = []"
      style="margin-bottom: 8px"
    />

    <!-- 储位显示区域 -->
    <div :class="['location-grid', `view-mode-${viewMode}`]" :style="gridStyle">
      <div
        v-for="location in displayLocations"
        :key="location.id"
        :class="[
          'location-card',
          `card-mode-${viewMode}`,
          {
            'location-empty': location.isEmpty,
            'location-occupied': !location.isEmpty,
            'location-locked': location.lock,
            'location-disabled': !location.enabled,
            'location-selected': selectedLocationIds.includes(location.id)
          },
        ]"
        @click="handleLocationClick(location)"
      >
        <a-tooltip :mouseEnterDelay="0.5" :destroyTooltipOnHide="true">
          <template #title>
            <div><strong>{{ location.nodeRemark }}</strong></div>
            <div v-if="location.group">{{ $t('dashboard.group') }}: {{ location.group }}</div>
            <div v-if="!location.enabled">{{ $t('dashboard.status') }}: <span style="color: #ff4d4f">{{ $t('dashboard.disabled') }}</span></div>
            <div v-else-if="location.lock">{{ $t('dashboard.status') }}: <span style="color: #ff4d4f">{{ $t('dashboard.locked') }}</span></div>
            <div v-else-if="location.isEmpty">{{ $t('dashboard.status') }}: <span style="color: #52c41a">{{ $t('dashboard.available') }}</span></div>
            <div v-else>
              <div>{{ $t('dashboard.status') }}: <span style="color: #faad14">{{ $t('dashboard.occupied') }}</span></div>
              <div>{{ $t('dashboard.material') }}: {{ location.materialCode }}</div>
              <div v-if="location.palletID && location.palletID !== '0'">{{ $t('dashboard.pallet') }}: {{ location.palletID }}</div>
            </div>
          </template>
          
          <div class="card-content">
            <!-- 卡片模式显示详细内容 -->
            <template v-if="viewMode === 'card'">
              <div class="card-header">
                <span class="location-name">{{ location.nodeRemark }}</span>
                <span class="status-indicator"></span>
              </div>
              <div class="card-body" v-if="!location.isEmpty">
                <div class="material-code" :title="location.materialCode">{{ location.materialCode }}</div>
                <div class="pallet-id" v-if="location.palletID && location.palletID !== '0'">{{ location.palletID }}</div>
              </div>
              <div class="card-body empty" v-else>
                <span>空闲</span>
              </div>
            </template>
            
            <!-- 高密度模式显示极简内容 -->
            <template v-else>
              <!-- 极简模式：仅显示状态颜色，无文字 -->
            </template>
          </div>
        </a-tooltip>
        
        <div class="selection-overlay" v-if="selectedLocationIds.includes(location.id)" @click.stop="handleLocationSelect(location.id, false)">
          <check-circle-filled />
        </div>
        <div class="selection-trigger" @click.stop="handleLocationSelect(location.id, !selectedLocationIds.includes(location.id))"></div>
      </div>
    </div>

    <!-- 分页 -->
    <div style="text-align: center; margin-top: 16px">
      <a-pagination
        v-model:current="currentPage"
        :total="filteredLocations.length"
        :page-size="pageSize"
        show-size-changer
        :page-size-options="['100', '200', '500', '1000']"
        size="small"
        @change="handlePageChange"
        @show-size-change="handlePageSizeChange"
      />
    </div>

    <!-- 储位详情弹框 -->
    <LocationDetailModal
      v-model:open="showDetailModal"
      :location="selectedLocation"
      @refresh="refreshData"
      @transfer-material="handleTransferMaterial"
      @relocate-material="handleRelocateMaterial"
    />

    <!-- 物料转移弹框 -->
    <MaterialTransferModal
      v-model:open="showTransferModal"
      :source-location="transferSourceLocation"
      :transfer-type="transferType"
      @confirm="handleTransferConfirm"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Location } from '@/services/location'
import locationService from '@/services/location'
import LocationDetailModal from '@/components/LocationDetailModal.vue'
import MaterialTransferModal from '@/components/MaterialTransferModal.vue'
import { message, Modal } from 'ant-design-vue'
import { CheckCircleFilled } from '@ant-design/icons-vue'
import * as echarts from 'echarts'
import taskService from '@/services/task'

const { t } = useI18n()
const router = useRouter()
const viewMode = ref('card')
const layoutDirection = ref('row')
const itemsPerColumn = ref(10)
const searchText = ref('')
const selectedGroup = ref<string | undefined>(undefined)
const selectedStatus = ref<string | undefined>(undefined)
const currentPage = ref(1)
const pageSize = ref(100) // Default to 100 as requested
const locations = ref<Location[]>([])
const groups = ref<string[]>([])
const isLoading = ref(false)
const showDetailModal = ref(false)
const selectedLocation = ref<Location | null>(null)
const showTransferModal = ref(false)
const transferSourceLocation = ref<Location | null>(null)
const transferType = ref<'transfer' | 'relocate'>('transfer')
const selectedLocationIds = ref<number[]>([])

// 图表相关
const locationChartRef = ref<HTMLElement | null>(null)
const taskChartRef = ref<HTMLElement | null>(null)
let locationChart: echarts.ECharts | null = null
let taskChart: echarts.ECharts | null = null

// 任务统计数据
const taskStats = ref({
  totalTasks: 0,
  completedTasks: 0,
  runningTasks: 0,
  canceledTasks: 0,
  waitingTasks: 0,
  completionRate: 0
})

// 统计数据 - 组合状态统计
const stats = computed(() => {
  const total = locations.value.length
  const available = locations.value.filter(l => l.isEmpty && !l.lock && l.enabled).length
  const occupied = locations.value.filter(l => !l.isEmpty && !l.lock && l.enabled).length
  const locked = locations.value.filter(l => l.lock && l.enabled).length
  const lockedWithMaterial = locations.value.filter(l => l.lock && !l.isEmpty && l.enabled).length
  const disabled = locations.value.filter(l => !l.enabled).length
  
  return {
    total,
    available,
    occupied,
    locked,
    lockedWithMaterial,
    disabled
  }
})

// 过滤后的储位
const filteredLocations = computed(() => {
  let filtered = locations.value

  if (searchText.value) {
    const search = searchText.value.toLowerCase()
    filtered = filtered.filter(l => 
      l.name.toLowerCase().includes(search) ||
      l.nodeRemark.toLowerCase().includes(search) ||
      (l.materialCode && l.materialCode.toLowerCase().includes(search))
    )
  }

  if (selectedGroup.value) {
    filtered = filtered.filter(l => l.group === selectedGroup.value)
  }

  if (selectedStatus.value) {
    switch (selectedStatus.value) {
      case 'available':
        filtered = filtered.filter(l => l.isEmpty && !l.lock && l.enabled)
        break
      case 'occupied':
        filtered = filtered.filter(l => !l.isEmpty && !l.lock && l.enabled)
        break
      case 'locked':
        filtered = filtered.filter(l => l.lock && l.enabled)
        break
      case 'disabled':
        filtered = filtered.filter(l => !l.enabled)
        break
    }
  }

  return filtered
})

// 当前页显示的储位
const displayLocations = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  const end = start + pageSize.value
  return filteredLocations.value.slice(start, end)
})

const gridStyle = computed(() => {
  if (layoutDirection.value === 'column') {
    const cardHeight = viewMode.value === 'card' ? 82 : 22 // 80+2 border/shadow approx, or just strict height
    // .location-card.card-mode-card is 80px height + border 2px = 82px
    // .location-card.card-mode-map is 20px height + border 2px = 22px
    // gap is 8px for card, 2px for map
    
    const h = viewMode.value === 'card' ? 80 : 20
    // border is 1px solid -> 2px total.
    // However, box-sizing might be content-box or border-box. Antd usually is border-box.
    // Let's assume height 80 includes border.
    
    const height = viewMode.value === 'card' ? 80 : 20
    const gap = viewMode.value === 'card' ? 8 : 2
    
    // Total height = (itemHeight * count) + (gap * (count - 1))
    // We add a bit of buffer or just set it exactly.
    const totalHeight = itemsPerColumn.value * (height + gap) - gap + 2 // +2 for potential border offset
    
    return {
      display: 'flex',
      flexDirection: 'column',
      flexWrap: 'wrap',
      height: `${totalHeight}px`,
      overflowX: 'auto',
      alignContent: 'flex-start',
      paddingBottom: '4px' // Scrollbar space
    }
  }
  return {}
})

onMounted(() => {
  refreshData()
  loadTaskStatistics()
  
  // 监听窗口大小变化调整图表
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  if (locationChart) locationChart.dispose()
  if (taskChart) taskChart.dispose()
})

const handleResize = () => {
  locationChart?.resize()
  taskChart?.resize()
}

// 初始化/更新储位图表
const updateLocationChart = () => {
  if (!locationChartRef.value) return
  
  if (!locationChart) {
    locationChart = echarts.init(locationChartRef.value)
  }
  
  const option = {
    tooltip: {
      trigger: 'item'
    },
    series: [
      {
        name: '储位状态',
        type: 'pie',
        radius: ['40%', '70%'], // 减小半径适应高度
        center: ['50%', '50%'],
        avoidLabelOverlap: false,
        itemStyle: {
          borderRadius: 5,
          borderColor: '#fff',
          borderWidth: 1
        },
        label: {
          show: false
        },
        data: [
          { value: stats.value.available, name: '空闲', itemStyle: { color: '#52c41a' } },
          { value: stats.value.occupied, name: '占用', itemStyle: { color: '#faad14' } },
          { value: stats.value.locked, name: '锁定', itemStyle: { color: '#f5222d' } },
          { value: stats.value.disabled, name: '禁用', itemStyle: { color: '#8c8c8c' } }
        ]
      }
    ]
  }
  
  locationChart.setOption(option)
}

// 初始化/更新任务图表
const updateTaskChart = () => {
  if (!taskChartRef.value) return
  
  if (!taskChart) {
    taskChart = echarts.init(taskChartRef.value)
  }
  
  const option = {
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'shadow'
      }
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '3%',
      top: '10%',
      containLabel: true
    },
    xAxis: [
      {
        type: 'category',
        data: [t('dashboard.completed'), t('dashboard.running'), t('dashboard.waiting'), t('dashboard.canceled')],
        axisTick: {
          alignWithLabel: true
        },
        axisLabel: {
          interval: 0,
          fontSize: 10
        }
      }
    ],
    yAxis: [
      {
        type: 'value'
      }
    ],
    series: [
      {
        name: '任务数量',
        type: 'bar',
        barWidth: '50%',
        data: [
          { value: taskStats.value.completedTasks, itemStyle: { color: '#52c41a' } },
          { value: taskStats.value.runningTasks, itemStyle: { color: '#faad14' } },
          { value: taskStats.value.waitingTasks, itemStyle: { color: '#1890ff' } },
          { value: taskStats.value.canceledTasks, itemStyle: { color: '#8c8c8c' } }
        ]
      }
    ]
  }
  
  taskChart.setOption(option)
}

// 监听数据变化更新图表
watch(stats, () => {
  nextTick(() => {
    updateLocationChart()
  })
}, { deep: true })

watch(taskStats, () => {
  nextTick(() => {
    updateTaskChart()
  })
}, { deep: true })

const refreshData = async () => {
  isLoading.value = true
  try {
    // 获取所有储位数据
    const response = await locationService.getLocations('', 1, 10000)
    if (response.success && response.data) {
      locations.value = response.data.items
      
      // 提取分组信息
      const groupSet = new Set<string>()
      locations.value.forEach(l => {
        if (l.group) {
          groupSet.add(l.group)
        }
      })
      groups.value = Array.from(groupSet).sort()
      
      // 更新图表
      nextTick(() => updateLocationChart())
    } else {
      message.error(response.message || '获取储位数据失败')
    }
  } catch (error: any) {
    message.error(error.message || '获取储位数据失败')
  } finally {
    isLoading.value = false
  }
}

const loadTaskStatistics = async () => {
  try {
    const response = await taskService.getTaskStatistics()
    
    if (response.success && response.data) {
      taskStats.value = {
        totalTasks: response.data.totalTasks || 0,
        completedTasks: response.data.completedTasks || 0,
        runningTasks: response.data.runningTasks || 0,
        canceledTasks: response.data.canceledTasks || 0,
        waitingTasks: response.data.waitingTasks || 0,
        completionRate: parseFloat(response.data.completionRate) || 0
      }
      nextTick(() => updateTaskChart())
    }
  } catch (error: any) {
    console.error('获取任务统计失败:', error)
  }
}

const handleSearch = () => {
  currentPage.value = 1
}

const handleGroupChange = () => {
  currentPage.value = 1
}

const handleStatusChange = () => {
  currentPage.value = 1
}

const handlePageChange = (page: number) => {
  currentPage.value = page
}

const handlePageSizeChange = (current: number, size: number) => {
  pageSize.value = size
  currentPage.value = 1
}

const handleLocationClick = (location: Location) => {
  selectedLocation.value = location
  showDetailModal.value = true
}

const handleTransferMaterial = (location: Location) => {
  transferSourceLocation.value = location
  transferType.value = 'transfer'
  showTransferModal.value = true
}

const handleRelocateMaterial = (location: Location) => {
  transferSourceLocation.value = location
  transferType.value = 'relocate'
  showTransferModal.value = true
}

const handleTransferConfirm = async (sourceId: number, targetId: number) => {
  try {
    let response
    if (transferType.value === 'transfer') {
      response = await locationService.transferMaterial(sourceId, targetId)
      if (response.success) {
        message.success('物料转移成功')
      }
    } else {
      response = await locationService.relocateMaterial(sourceId, targetId)
      if (response.success) {
        message.success(`移库任务创建成功，任务ID: ${response.data?.taskId}`)
      }
    }
    
    if (!response.success) {
      message.error(response.message || '操作失败')
    } else {
      showTransferModal.value = false
      refreshData()
    }
  } catch (error: any) {
    message.error(error.message || '操作失败')
  }
}

const handleLocationSelect = (locationId: number, checked: boolean) => {
  if (checked) {
    if (!selectedLocationIds.value.includes(locationId)) {
      selectedLocationIds.value.push(locationId)
    }
  } else {
    selectedLocationIds.value = selectedLocationIds.value.filter(id => id !== locationId)
  }
}

const handleBatchClearMaterial = () => {
  Modal.confirm({
    title: '确认批量清空物料',
    content: `确定要清空选中的 ${selectedLocationIds.value.length} 个储位的物料吗？`,
    okText: '确定',
    cancelText: '取消',
    onOk: async () => {
      try {
        const response = await locationService.batchClearMaterial(selectedLocationIds.value)
        if (response.success) {
          message.success(`成功清空 ${response.data?.successCount} 个储位的物料`)
          selectedLocationIds.value = []
          refreshData()
        } else {
          message.error(response.message || '批量清空物料失败')
        }
      } catch (error: any) {
        message.error(error.message || '批量清空物料失败')
      }
    },
  })
}

const handleBatchToggleLock = (lockState: boolean) => {
  Modal.confirm({
    title: `确认批量${lockState ? '锁定' : '解锁'}`,
    content: `确定要${lockState ? '锁定' : '解锁'}选中的 ${selectedLocationIds.value.length} 个储位吗？`,
    okText: '确定',
    cancelText: '取消',
    onOk: async () => {
      try {
        const response = await locationService.batchToggleLock(selectedLocationIds.value, lockState)
        if (response.success) {
          message.success(response.message || `成功${lockState ? '锁定' : '解锁'} ${response.data?.successCount} 个储位`)
          selectedLocationIds.value = []
          await refreshData()
        } else {
          message.error(response.message || `批量${lockState ? '锁定' : '解锁'}失败`)
        }
      } catch (error: any) {
        message.error(error.message || `批量${lockState ? '锁定' : '解锁'}失败`)
      }
    },
  })
}
</script>

<style scoped>
.dashboard-container {
  width: 100%;
}

.dashboard-container h1 {
  margin-bottom: 16px;
  font-size: 20px;
  font-weight: 600;
}

/* 统计图表样式 */
.chart-container {
  display: flex;
  align-items: center;
  gap: 12px;
  height: 160px; /* 减小高度 */
}

.chart-box {
  flex: 1;
  height: 100%;
  min-width: 0;
}

.chart-info {
  width: 120px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-right: 8px;
}

.info-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
}

.info-item.total {
  margin-top: 4px;
  padding-top: 4px;
  border-top: 1px solid #f0f0f0;
  font-weight: 600;
}

.info-item .label {
  color: #8c8c8c;
}

.info-item .value {
  font-weight: 500;
  font-family: monospace;
  font-size: 13px;
}

.value.success { color: #52c41a; }
.value.warning { color: #faad14; }
.value.error { color: #f5222d; }
.value.disabled { color: #8c8c8c; }
.value.primary { color: #1890ff; }

.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 8px;
}

/* 高密度网格样式 */
.location-grid {
  display: flex;
  flex-wrap: wrap;
  align-content: flex-start;
}

.location-grid.view-mode-card {
  gap: 4px;
}

.location-grid.view-mode-map {
  gap: 2px;
}

.location-card {
  border: 1px solid #d9d9d9;
  border-radius: 2px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  position: relative;
  transition: all 0.2s;
  background-color: #fff;
  overflow: hidden;
}

/* 视图模式：卡片 */
.location-card.card-mode-card {
  width: 100px;
  height: 60px;
  align-items: flex-start;
  justify-content: flex-start;
}

/* 视图模式：高密度 */
.location-card.card-mode-map {
  width: 20px;
  height: 20px;
}

.location-card:hover {
  transform: translateY(-2px);
  z-index: 10;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

/* 卡片内容样式 */
.card-content {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.card-mode-card .card-content {
  padding: 4px 6px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
  border-bottom: 1px solid rgba(0,0,0,0.05);
  padding-bottom: 2px;
}

.location-name {
  font-weight: 600;
  font-size: 12px;
  color: #333;
}

.card-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  font-size: 11px;
}

.card-body.empty {
  align-items: center;
  color: #b7eb8f;
  font-weight: 500;
}

.material-code {
  color: #1890ff;
  font-family: monospace;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pallet-id {
  color: #8c8c8c;
  transform: scale(0.9);
  transform-origin: left;
}

/* 状态颜色 */
.location-empty {
  background-color: #f6ffed;
  border-color: #b7eb8f;
}

.location-occupied {
  background-color: #fff7e6;
  border-color: #ffe58f;
}

.location-locked {
  background-color: #fff1f0;
  border-color: #ffa39e;
}

.location-disabled {
  background-color: #f5f5f5;
  border-color: #d9d9d9;
  cursor: not-allowed;
}

.location-selected {
  border-color: #1890ff;
  border-width: 2px;
}

.selection-overlay {
  position: absolute;
  top: 0;
  right: 0;
  bottom: 0;
  left: 0;
  background-color: rgba(24, 144, 255, 0.2);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #1890ff;
  font-size: 14px;
}

/* 鼠标悬停时显示的勾选框触发区 */
.selection-trigger {
  position: absolute;
  top: 0;
  right: 0;
  width: 10px;
  height: 10px;
  background-color: rgba(0,0,0,0.05);
  border-bottom-left-radius: 4px;
  display: none;
}

.location-card.card-mode-card .selection-trigger {
  width: 24px;
  height: 24px;
  border-bottom-left-radius: 8px;
}

.location-card:hover .selection-trigger {
  display: block;
}

.selection-trigger:hover {
  background-color: #1890ff;
}
</style>