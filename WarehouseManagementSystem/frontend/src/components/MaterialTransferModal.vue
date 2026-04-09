<template>
  <a-modal
    v-model:open="visible"
    :title="modalTitle"
    width="800px"
    @ok="handleConfirm"
    @cancel="handleCancel"
    :confirm-loading="isLoading"
  >
    <div v-if="sourceLocation">
      <a-alert
        :message="`源储位: ${sourceLocation.name} (${sourceLocation.nodeRemark})`"
        :description="`物料代码: ${sourceLocation.materialCode}`"
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
              v-for="location in availableLocations"
              :key="location.id"
              :value="location.id"
            >
              {{ location.name }} - {{ location.nodeRemark }} ({{ location.group }})
            </a-select-option>
          </a-select>
        </a-form-item>
        
        <a-form-item v-if="selectedTargetLocation">
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="储位名称">
              {{ selectedTargetLocation.name }}
            </a-descriptions-item>
            <a-descriptions-item label="分组">
              {{ selectedTargetLocation.group }}
            </a-descriptions-item>
            <a-descriptions-item label="备注">
              {{ selectedTargetLocation.nodeRemark }}
            </a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag color="green">空闲</a-tag>
            </a-descriptions-item>
          </a-descriptions>
        </a-form-item>
      </a-form>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { Location } from '@/services/location'
import locationService from '@/services/location'
import { message } from 'ant-design-vue'

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
  get: () => props.open !== undefined ? props.open : props.modelValue,
  set: (val) => {
    emit('update:open', val)
    emit('update:modelValue', val)
  }
})

const selectedTargetLocationId = ref<number | undefined>(undefined)
const availableLocations = ref<Location[]>([])
const isLoading = ref(false)

const modalTitle = computed(() => {
  return props.transferType === 'transfer' ? '物料转移' : '物料移库'
})

const selectedTargetLocation = computed(() => {
  if (!selectedTargetLocationId.value) return null
  return availableLocations.value.find(l => l.id === selectedTargetLocationId.value) || null
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
    const response = await locationService.getLocations('', 1, 10000)
    if (response.success && response.data) {
      const allItems = response.data.items;
      // 过滤出可用的储位（空闲、启用、未锁定，且不是源储位）
      const filtered = allItems.filter(l => 
        l.isEmpty && 
        l.enabled && 
        !l.lock && 
        l.id !== props.sourceLocation?.id
      );
      
      availableLocations.value = filtered;
      
      if (filtered.length === 0 && allItems.length > 0) {
          message.warning(`未找到可用目标储位。总储位: ${allItems.length}，但没有空闲、启用且未锁定的储位。`);
      }
    }
  } catch (error) {
    console.error('加载可用储位失败:', error)
    message.error('加载可用储位失败')
  }
}

const filterOption = (input: string, option: any) => {
  const location = availableLocations.value.find(l => l.id === option.value)
  if (!location) return false
  
  const searchText = input.toLowerCase()
  return (
    location.name.toLowerCase().includes(searchText) ||
    location.nodeRemark.toLowerCase().includes(searchText) ||
    location.group.toLowerCase().includes(searchText)
  )
}

const handleConfirm = async () => {
  if (!props.sourceLocation || !selectedTargetLocationId.value) {
    message.error('请选择目标储位')
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

<style scoped>
</style>