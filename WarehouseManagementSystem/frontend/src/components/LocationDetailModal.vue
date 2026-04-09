<template>
  <a-modal
    v-model:open="visible"
    :title="`储位详情 - ${location?.name}`"
    width="700px"
    @ok="handleOk"
  >
    <a-spin v-if="isLoading" />
    <a-descriptions v-else-if="location" :column="2" bordered>
      <a-descriptions-item label="储位名称" :span="2">
        {{ location.name }}
      </a-descriptions-item>
      <a-descriptions-item label="节点备注" :span="2">
        {{ location.nodeRemark || '-' }}
      </a-descriptions-item>
      <a-descriptions-item label="分组">
        {{ location.group }}
      </a-descriptions-item>
      <a-descriptions-item label="入库时间">
        {{ location.entryDate || '-' }}
      </a-descriptions-item>
      <a-descriptions-item label="物料代码">
        {{ location.materialCode || '-' }}
      </a-descriptions-item>
      <a-descriptions-item label="托盘ID">
        {{ location.palletID || '-' }}
      </a-descriptions-item>
      <a-descriptions-item label="是否为空">
        <a-tag :color="location.isEmpty ? '#52c41a' : '#faad14'">
          {{ location.isEmpty ? '是' : '否' }}
        </a-tag>
      </a-descriptions-item>
      <a-descriptions-item label="是否锁定">
        <a-tag :color="location.lock ? '#f5222d' : '#52c41a'">
          {{ location.lock ? '是' : '否' }}
        </a-tag>
      </a-descriptions-item>
      <a-descriptions-item label="是否启用" :span="2">
        <a-tag :color="location.enabled ? '#52c41a' : '#8c8c8c'">
          {{ location.enabled ? '是' : '否' }}
        </a-tag>
      </a-descriptions-item>
      <a-descriptions-item label="操作" :span="2">
        <a-space wrap>
          <a-button
            type="primary"
            size="small"
            @click="handleEdit"
          >
            编辑
          </a-button>
          <a-button
            size="small"
            @click="handleClearMaterial"
            :loading="isClearingMaterial"
            :disabled="location.isEmpty"
            danger
          >
            清空物料
          </a-button>
          <a-button
            size="small"
            @click="handleToggleLock"
            :loading="isTogglingLock"
          >
            {{ location.lock ? '解锁' : '锁定' }}
          </a-button>
          <a-button
            size="small"
            @click="handleTransferMaterial"
            :disabled="location.isEmpty"
          >
            物料转移（直接转移）
          </a-button>
          <a-button
            size="small"
            @click="handleRelocateMaterial"
            :disabled="location.isEmpty"
          >
            物料移库（生成AGV任务）
          </a-button>
        </a-space>
      </a-descriptions-item>
    </a-descriptions>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Location } from '@/services/location'
import locationService from '@/services/location'
import { message } from 'ant-design-vue'

interface Props {
  modelValue?: boolean // Vue 3 v-model for visible
  open?: boolean // Ant Design Vue 4 uses open
  location: Location | null
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'update:open', value: boolean): void
  (e: 'refresh'): void
  (e: 'transfer-material', location: Location): void
  (e: 'relocate-material', location: Location): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  open: undefined,
  location: null,
})

const emit = defineEmits<Emits>()
const router = useRouter()
const { t } = useI18n()

// Handle both v-model:open (Antdv 4) and v-model:visible (Antdv 3/Custom)
const visible = computed({
  get: () => props.open !== undefined ? props.open : props.modelValue,
  set: (val) => {
    emit('update:open', val)
    emit('update:modelValue', val)
  }
})

const isLoading = ref(false)
const isClearingMaterial = ref(false)
const isTogglingLock = ref(false)

const handleOk = () => {
  visible.value = false
}

const handleEdit = () => {
  if (!props.location) return
  
  // Close the modal
  visible.value = false
  
  // Navigate to edit page
  router.push({
    name: 'LocationEdit',
    params: { id: props.location.id }
  })
}

const handleClearMaterial = async () => {
  if (!props.location) return

  isClearingMaterial.value = true
  try {
    const response = await locationService.clearMaterial(props.location.id)
    if (response.success) {
      message.success('清空物料成功')
      emit('refresh')
      visible.value = false
    } else {
      message.error(response.message || '清空物料失败')
    }
  } catch (error: any) {
    message.error(error.message || '清空物料失败')
  } finally {
    isClearingMaterial.value = false
  }
}

const handleToggleLock = async () => {
  if (!props.location) return

  isTogglingLock.value = true
  try {
    const response = await locationService.toggleLock(props.location.id, !props.location.lock)
    if (response.success) {
      message.success('操作成功')
      emit('refresh')
      // visible.value = false // Keep modal open to see change
    } else {
      message.error(response.message || '操作失败')
    }
  } catch (error: any) {
    message.error(error.message || '操作失败')
  } finally {
    isTogglingLock.value = false
  }
}


const handleTransferMaterial = () => {
  // 触发父组件的物料转移事件
  emit('transfer-material', props.location)
  visible.value = false
}

const handleRelocateMaterial = () => {
  // 触发父组件的物料移库事件
  emit('relocate-material', props.location)
  visible.value = false
}
</script>

<style scoped>
</style>
