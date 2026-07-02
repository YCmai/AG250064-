<template>
  <a-modal
    v-model:open="visible"
    :title="t('location.importPreview')"
    width="90%"
    :footer="null"
    :body-style="{ maxHeight: '70vh', overflow: 'auto' }"
  >
    <div class="import-preview-container">
      <a-alert
        :message="`${t('location.importSummary', { n: previewData.length })}`"
        type="info"
        show-icon
        style="margin-bottom: 16px"
      />

      <a-row :gutter="16" style="margin-bottom: 16px">
        <a-col :span="8">
          <a-card size="small">
            <a-statistic
              :title="t('location.validData')"
              :value="validCount"
              :value-style="{ color: '#52c41a' }"
            />
          </a-card>
        </a-col>
        <a-col :span="8">
          <a-card size="small">
            <a-statistic
              :title="t('location.invalidData')"
              :value="invalidCount"
              :value-style="{ color: '#f5222d' }"
            />
          </a-card>
        </a-col>
        <a-col :span="8">
          <a-card size="small">
            <a-statistic
              title="Total"
              :value="previewData.length"
              :value-style="{ color: '#1890ff' }"
            />
          </a-card>
        </a-col>
      </a-row>

      <a-table
        :columns="columns"
        :data-source="previewData"
        :pagination="{ pageSize: 10 }"
        :scroll="{ x: 1400 }"
        size="small"
        bordered
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag v-if="record.errors.length === 0" color="success">
              ✓ {{ t('location.valid') }}
            </a-tag>
            <a-tooltip v-else :title="record.errors.join('; ')">
              <a-tag color="error">
                ✗ {{ t('location.invalid') }}
              </a-tag>
            </a-tooltip>
          </template>
        </template>
      </a-table>

      <div v-if="invalidCount > 0" style="margin-top: 16px">
        <a-alert
          :message="t('location.importErrorTitle', { count: invalidCount })"
          type="error"
          show-icon
          style="margin-bottom: 8px"
        />
        <a-list
          size="small"
          bordered
          :data-source="allErrors"
          style="max-height: 200px; overflow-y: auto"
        >
          <template #renderItem="{ item }">
            <a-list-item>
              <a-typography-text type="danger">{{ item }}</a-typography-text>
            </a-list-item>
          </template>
        </a-list>
      </div>

      <div style="margin-top: 16px; text-align: right">
        <a-space>
          <a-button @click="handleCancel">{{ t('common.cancel') }}</a-button>
          <a-button
            type="primary"
            :disabled="validCount === 0"
            :loading="isImporting"
            @click="handleConfirmImport"
          >
            {{ t('location.confirmImport', { n: validCount }) }}
          </a-button>
        </a-space>
      </div>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface ImportData {
  rowNumber: number
  data: any
  errors: string[]
}

interface Props {
  modelValue: boolean
  previewData: ImportData[]
  importing?: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm', validData: any[]): void
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  previewData: () => [],
  importing: false,
})

const emit = defineEmits<Emits>()

const visible = ref(props.modelValue)

const columns = computed(() => [
  {
    title: t('location.rowNumber'),
    dataIndex: 'rowNumber',
    key: 'rowNumber',
    width: 70,
    fixed: 'left',
  },
  {
    title: t('location.validationResult'),
    key: 'status',
    width: 100,
    fixed: 'left',
  },
  {
    title: t('location.mapNode'),
    dataIndex: ['data', 'name'],
    key: 'name',
    width: 120,
  },
  {
    title: t('location.nodeRemark'),
    dataIndex: ['data', 'nodeRemark'],
    key: 'nodeRemark',
    width: 120,
  },
  {
    title: t('location.group'),
    dataIndex: ['data', 'group'],
    key: 'group',
    width: 100,
  },
  {
    title: t('location.laneCode'),
    dataIndex: ['data', 'laneCode'],
    key: 'laneCode',
    width: 100,
  },
  {
    title: t('location.depthIndex'),
    dataIndex: ['data', 'depthIndex'],
    key: 'depthIndex',
    width: 100,
  },
  {
    title: t('location.waitingNode'),
    dataIndex: ['data', 'wattingNode'],
    key: 'wattingNode',
    width: 140,
  },
  {
    title: t('location.liftingHeight'),
    dataIndex: ['data', 'liftingHeight'],
    key: 'liftingHeight',
    width: 100,
  },
  {
    title: t('location.unloadHeight'),
    dataIndex: ['data', 'unloadHeight'],
    key: 'unloadHeight',
    width: 100,
  },
])

const validCount = computed(() => props.previewData.filter(item => item.errors.length === 0).length)
const invalidCount = computed(() => props.previewData.filter(item => item.errors.length > 0).length)
const isImporting = computed(() => props.importing)

const allErrors = computed(() => {
  return props.previewData
    .filter(item => item.errors.length > 0)
    .map(item => `${t('location.row')} ${item.rowNumber}: ${item.errors.join('; ')}`)
})

watch(
  () => props.modelValue,
  (newVal) => {
    visible.value = newVal
  }
)

watch(
  () => visible.value,
  (newVal) => {
    emit('update:modelValue', newVal)
  }
)

const handleCancel = () => {
  if (isImporting.value) {
    return
  }

  visible.value = false
}

const handleConfirmImport = () => {
  const validData = props.previewData
    .filter(item => item.errors.length === 0)
    .map(item => item.data)

  emit('confirm', validData)
}
</script>

<style scoped>
.import-preview-container {
  padding: 8px 0;
}
</style>
