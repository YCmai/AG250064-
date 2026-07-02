<template>
  <a-modal
    v-model:open="visible"
    :title="`${t('location.detailTitle', { name: location?.name || '-' })}`"
    width="760px"
    @ok="handleOk"
  >
    <a-spin v-if="isLoading" />
    <a-descriptions v-else-if="location" :column="2" bordered>
      <a-descriptions-item :label="t('location.name')" :span="2">
        {{ location.name }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.nodeRemark')" :span="2">
        {{ location.nodeRemark || '-' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.group')">
        {{ location.group || '-' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.entryDate')">
        {{ location.entryDate || '-' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.laneCode')">
        {{ location.laneCode || '-' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.depthIndex')">
        {{ location.depthIndex || '-' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.waitingNode')">
        {{ location.wattingNode || '-' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.materialCode')">
        {{ location.materialCode || '-' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.palletId')">
        {{ location.palletID || '-' }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.isEmpty')">
        <a-tag :color="location.isEmpty ? '#52c41a' : '#faad14'">
          {{ location.isEmpty ? t('location.yes') : t('location.no') }}
        </a-tag>
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.isLocked')">
        <a-tag :color="location.lock ? '#f5222d' : '#52c41a'">
          {{ location.lock ? t('location.yes') : t('location.no') }}
        </a-tag>
      </a-descriptions-item>
      <a-descriptions-item :label="t('location.isEnabled')" :span="2">
        <a-tag :color="location.enabled ? '#52c41a' : '#8c8c8c'">
          {{ location.enabled ? t('location.yes') : t('location.no') }}
        </a-tag>
      </a-descriptions-item>
      <a-descriptions-item :label="t('common.operation')" :span="2">
        <a-space wrap>
          <a-button type="primary" size="small" @click="handleEdit">
            {{ t('common.edit') }}
          </a-button>
          <a-button
            size="small"
            @click="handleClearMaterial"
            :loading="isClearingMaterial"
            :disabled="location.isEmpty"
            danger
          >
            {{ t('location.clearMaterial') }}
          </a-button>
          <a-button
            size="small"
            @click="handleToggleLock"
            :loading="isTogglingLock"
          >
            {{ location.lock ? t('location.unlock') : t('location.lock') }}
          </a-button>
          <a-button
            size="small"
            @click="handleTransferMaterial"
            :disabled="location.isEmpty"
          >
            {{ t('location.transfer') }}
          </a-button>
          <a-button
            size="small"
            @click="handleRelocateMaterial"
            :disabled="location.isEmpty"
          >
            {{ t('location.relocate') }}
          </a-button>
        </a-space>
      </a-descriptions-item>
    </a-descriptions>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { message } from 'ant-design-vue'
import { Location } from '@/services/location'
import locationService from '@/services/location'

interface Props {
  modelValue?: boolean
  open?: boolean
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

const visible = computed({
  get: () => (props.open !== undefined ? props.open : props.modelValue),
  set: (val) => {
    emit('update:open', val)
    emit('update:modelValue', val)
  },
})

const isLoading = ref(false)
const isClearingMaterial = ref(false)
const isTogglingLock = ref(false)

const handleOk = () => {
  visible.value = false
}

const handleEdit = () => {
  if (!props.location) {
    return
  }

  visible.value = false
  router.push({
    name: 'LocationEdit',
    params: { id: props.location.id },
  })
}

const handleClearMaterial = async () => {
  if (!props.location) {
    return
  }

  isClearingMaterial.value = true
  try {
    const response = await locationService.clearMaterial(props.location.id)
    if (response.success) {
      message.success(t('common.success'))
      emit('refresh')
      visible.value = false
    } else {
      message.error(response.message || t('common.fail'))
    }
  } catch (error: any) {
    message.error(error.message || t('common.fail'))
  } finally {
    isClearingMaterial.value = false
  }
}

const handleToggleLock = async () => {
  if (!props.location) {
    return
  }

  isTogglingLock.value = true
  try {
    const response = await locationService.toggleLock(props.location.id, !props.location.lock)
    if (response.success) {
      message.success(response.message || t('common.success'))
      emit('refresh')
    } else {
      message.error(response.message || t('common.fail'))
    }
  } catch (error: any) {
    message.error(error.message || t('common.fail'))
  } finally {
    isTogglingLock.value = false
  }
}

const handleTransferMaterial = () => {
  if (!props.location) {
    return
  }

  emit('transfer-material', props.location)
  visible.value = false
}

const handleRelocateMaterial = () => {
  if (!props.location) {
    return
  }

  emit('relocate-material', props.location)
  visible.value = false
}
</script>
