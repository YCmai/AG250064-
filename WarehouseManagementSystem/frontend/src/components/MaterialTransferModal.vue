<template>
  <a-modal
    v-model:open="visible"
    :title="modalTitle"
    width="860px"
    @ok="handleConfirm"
    @cancel="handleCancel"
    :confirm-loading="isLoading"
  >
    <div v-if="sourceLocation">
      <a-alert
        :message="`源储位: ${sourceLocation.name} (${sourceLocation.nodeRemark})`"
        :description="`物料代码: ${sourceLocation.materialCode || '-'}`"
        type="info"
        style="margin-bottom: 16px"
      />

      <a-form layout="vertical">
        <a-form-item label="选择目标储位">
          <a-select
            v-model:value="selectedTargetLocationId"
            placeholder="请选择目标储位"
            show-search
            :filter-option="filterOption"
            style="width: 100%"
          >
            <a-select-option
              v-for="location in filteredLocations"
              :key="location.id"
              :value="location.id"
            >
              {{ renderLocationLabel(location) }}
            </a-select-option>
          </a-select>
        </a-form-item>

        <a-form-item v-if="selectedTargetLocation">
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="储位名称">
              {{ selectedTargetLocation.name }}
            </a-descriptions-item>
            <a-descriptions-item label="分组">
              {{ selectedTargetLocation.group || '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="节点备注">
              {{ selectedTargetLocation.nodeRemark }}
            </a-descriptions-item>
            <a-descriptions-item label="库道编号">
              {{ selectedTargetLocation.laneCode || '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="深度序号">
              {{ selectedTargetLocation.depthIndex || '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="信号请求点">
              {{ selectedTargetLocation.wattingNode || '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="锁定状态">
              <a-tag :color="selectedTargetLocation.isLocked ? 'error' : 'success'">
                {{ selectedTargetLocation.isLocked ? '锁定' : '未锁定' }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="规则状态">
              <a-tag :color="selectedTargetLocation.isRecommendedTarget ? 'success' : (selectedTargetLocation.isReachableTarget ? 'processing' : 'warning')">
                {{ selectedTargetLocation.isRecommendedTarget ? '推荐目标储位' : (selectedTargetLocation.isReachableTarget ? '可达但不推荐' : '当前不可达') }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="物料代码">
              {{ selectedTargetLocation.materialCode || '-' }}
            </a-descriptions-item>
            <a-descriptions-item label="托盘编号">
              {{ selectedTargetLocation.palletID || '-' }}
            </a-descriptions-item>
          </a-descriptions>
        </a-form-item>
      </a-form>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import { Location, RecommendedLocation } from '@/services/location'
import locationService from '@/services/location'

interface Props {
  modelValue?: boolean
  open?: boolean
  sourceLocation: Location | null
  transferType: 'transfer' | 'relocate'
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'update:open', value: boolean): void
  (e: 'confirm', sourceId: number, targetId: number): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  open: undefined,
  sourceLocation: null,
  transferType: 'transfer',
})

const emit = defineEmits<Emits>()

const visible = computed({
  get: () => (props.open !== undefined ? props.open : props.modelValue),
  set: (val) => {
    emit('update:open', val)
    emit('update:modelValue', val)
  },
})

const selectedTargetLocationId = ref<number | undefined>(undefined)
const availableLocations = ref<RecommendedLocation[]>([])
const isLoading = ref(false)

const modalTitle = computed(() => (props.transferType === 'transfer' ? '物料转移' : '物料移库'))

const filteredLocations = computed(() => {
  return availableLocations.value.filter(location => location.id !== props.sourceLocation?.id)
})

const selectedTargetLocation = computed(() => {
  if (!selectedTargetLocationId.value) {
    return null
  }

  return filteredLocations.value.find(item => item.id === selectedTargetLocationId.value) || null
})

watch(
  () => visible.value,
  (newVal) => {
    if (newVal) {
      loadAvailableLocations()
    } else {
      selectedTargetLocationId.value = undefined
    }
  }
)

onMounted(() => {
  if (visible.value) {
    loadAvailableLocations()
  }
})

const loadAvailableLocations = async () => {
  try {
    const response = await locationService.getRecommendedTargets(props.sourceLocation?.id)
    if (response.success && response.data) {
      availableLocations.value = response.data
      if (response.data.length === 0) {
        message.warning('未找到可用目标储位')
      }
    } else {
      message.error(response.message || '加载可用储位失败')
    }
  } catch (error: any) {
    message.error(error.message || '加载可用储位失败')
  }
}

const renderLocationLabel = (location: RecommendedLocation) => {
  const recommendation = location.isRecommendedTarget
    ? `推荐#${location.recommendationOrder ?? '-'}`
    : (location.isReachableTarget ? '可达' : '不可达')

  return `${location.nodeRemark} | 库道:${location.laneCode || '-'} | 深度:${location.depthIndex || '-'} | ${recommendation}`
}

const filterOption = (input: string, option: any) => {
  const location = filteredLocations.value.find(item => item.id === option.value)
  if (!location) {
    return false
  }

  const searchText = input.toLowerCase()
  return (
    location.name.toLowerCase().includes(searchText) ||
    location.nodeRemark.toLowerCase().includes(searchText) ||
    (location.group || '').toLowerCase().includes(searchText) ||
    (location.laneCode || '').toLowerCase().includes(searchText)
  )
}

const handleConfirm = async () => {
  if (!props.sourceLocation || !selectedTargetLocationId.value) {
    message.error('请选择目标储位')
    return
  }

  if (!selectedTargetLocation.value?.isReachableTarget) {
    message.error('当前目标储位不可达，请先处理外侧储位')
    return
  }

  isLoading.value = true
  try {
    emit('confirm', props.sourceLocation.id, selectedTargetLocationId.value)
  } finally {
    isLoading.value = false
  }
}

const handleCancel = () => {
  visible.value = false
}
</script>
