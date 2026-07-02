<template>
  <div class="task-create-container">
    <a-card :title="t('task.createTitle')" :body-style="{ padding: '12px' }">
      <div class="selection-summary">
        <div class="summary-step source" :class="{ active: !formState.sourcePosition, completed: formState.sourcePosition }">
          <span class="label">{{ t('task.source') }}:</span>
          <span class="value">{{ formState.sourcePosition || t('common.select') }}</span>
        </div>
        <div class="arrow">→</div>
        <div class="summary-step target" :class="{ active: formState.sourcePosition && !formState.targetPosition, completed: formState.targetPosition }">
          <span class="label">{{ t('task.target') }}:</span>
          <span class="value">{{ formState.targetPosition || t('common.select') }}</span>
        </div>
      </div>

      <a-divider style="margin: 12px 0" />

      <a-form
        :model="formState"
        layout="vertical"
        @finish="handleSubmit"
        class="task-create-form"
      >
        <a-form-item :label="t('task.type')" name="taskType" style="margin-bottom: 12px" v-show="false">
          <a-select v-model:value="formState.taskType" style="width: 100%">
            <a-select-option v-for="opt in taskTypeOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </a-select-option>
          </a-select>
        </a-form-item>

        <div class="selection-panels">
          <div class="panel source-panel">
            <div class="panel-header">
              <span class="title">{{ t('task.sourcePosition') }}</span>
              <a-input
                v-model:value="sourceSearch"
                :placeholder="t('task.searchSource')"
                allow-clear
                size="small"
                style="width: 180px"
              />
            </div>
            <div class="grid-container custom-scrollbar">
              <div
                v-for="loc in filteredSourceLocations"
                :key="loc.id"
                class="grid-item"
                :class="[
                  { selected: formState.sourcePosition === loc.nodeRemark },
                  { disabled: formState.targetPosition === loc.nodeRemark },
                  loc.isLocked ? 'status-locked' : (!loc.isEmpty ? 'status-full' : 'status-empty')
                ]"
                @click="selectSource(loc)"
              >
                <div class="loc-name">{{ loc.nodeRemark }}</div>
                <div class="loc-group">{{ `${loc.group || '-'} / ${loc.laneCode || '-'}` }}</div>
                <div class="loc-depth">深度 {{ loc.depthIndex || '-' }}</div>
                <div class="loc-status">
                  <span v-if="loc.isLocked" class="indicator locked">锁定</span>
                  <span v-else-if="!loc.isEmpty" class="indicator full">有货</span>
                  <span v-else class="indicator empty">空置</span>
                </div>
              </div>
              <div v-if="filteredSourceLocations.length === 0" class="empty-state">
                {{ t('task.noMatch') }}
              </div>
            </div>
          </div>

          <div class="panel target-panel">
            <div class="panel-header">
              <span class="title">{{ t('task.targetPosition') }}</span>
              <a-input
                v-model:value="targetSearch"
                :placeholder="t('task.searchTarget')"
                allow-clear
                size="small"
                style="width: 180px"
              />
            </div>
            <div class="grid-container custom-scrollbar">
              <div
                v-for="loc in filteredTargetLocations"
                :key="loc.id"
                class="grid-item"
                :class="[
                  { selected: formState.targetPosition === loc.nodeRemark },
                  { disabled: formState.sourcePosition === loc.nodeRemark || loc.isLocked || !loc.isReachableTarget || !loc.isEmpty },
                  loc.isLocked ? 'status-locked' : (loc.isRecommendedTarget ? 'status-recommended' : (loc.isReachableTarget ? 'status-empty' : 'status-blocked'))
                ]"
                @click="!loc.isLocked && loc.isReachableTarget && loc.isEmpty && selectTarget(loc)"
              >
                <div class="loc-name">{{ loc.nodeRemark }}</div>
                <div class="loc-group">{{ `${loc.group || '-'} / ${loc.laneCode || '-'}` }}</div>
                <div class="loc-depth">深度 {{ loc.depthIndex || '-' }}</div>
                <div class="loc-status">
                  <span v-if="loc.isLocked" class="indicator locked">锁定</span>
                  <span v-else-if="!loc.isReachableTarget" class="indicator blocked">不可达</span>
                  <span v-else-if="loc.isRecommendedTarget" class="indicator recommended">
                    {{ `推荐${loc.recommendationOrder ? `#${loc.recommendationOrder}` : ''}` }}
                  </span>
                  <span v-else class="indicator empty">可达</span>
                </div>
              </div>
              <div v-if="filteredTargetLocations.length === 0" class="empty-state">
                {{ t('task.noMatch') }}
              </div>
            </div>
          </div>
        </div>

        <div class="form-actions">
          <a-space size="large">
            <a-button @click="handleCancel">{{ t('common.cancel') }}</a-button>
            <a-button
              type="primary"
              html-type="submit"
              :loading="isSubmitting"
              :disabled="!formState.sourcePosition || !formState.targetPosition"
              size="large"
            >
              {{ t('task.create') }}
            </a-button>
          </a-space>
        </div>
      </a-form>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { useI18n } from 'vue-i18n'
import { getTaskTypeOptions } from '@/constants/taskType'
import { useSettingStore } from '@/stores/setting'
import taskService from '@/services/task'

const { t } = useI18n()
const router = useRouter()
const settingStore = useSettingStore()

const taskTypeOptions = computed(() => getTaskTypeOptions(settingStore.systemType))

const isSubmitting = ref(false)
const availableLocations = ref<Array<{
  id: number
  name: string
  nodeRemark: string
  group: string
  laneCode: string
  depthIndex: number
  wattingNode: string
  isEmpty: boolean
  isLocked: boolean
  enabled: boolean
  materialCode: string | null
  palletID: string | null
  isReachableTarget: boolean
  isRecommendedTarget: boolean
  recommendationOrder: number | null
}>>([])

const sourceSearch = ref('')
const targetSearch = ref('')

const formState = reactive({
  sourcePosition: '',
  targetPosition: '',
  materialCode: '',
  taskType: 0,
  priority: 1,
})

watch(
  () => taskTypeOptions.value,
  (newOptions) => {
    if (newOptions.length > 0 && !newOptions.some(opt => opt.value === formState.taskType)) {
      formState.taskType = newOptions[0].value
    }
  },
  { immediate: true }
)

onMounted(() => {
  fetchAvailableLocations()
})

const fetchAvailableLocations = async () => {
  try {
    const response = await taskService.getAvailableLocations()
    if (response.success && response.data) {
      availableLocations.value = response.data
    } else {
      message.error(response.message || '获取可用位置失败')
    }
  } catch (error: any) {
    message.error(error.message || '获取可用位置失败')
  }
}

const filterLocations = (locations: typeof availableLocations.value, search: string) => {
  if (!search) {
    return locations
  }

  const lowerSearch = search.toLowerCase()
  return locations.filter(loc => {
    return [loc.name, loc.nodeRemark, loc.group, loc.laneCode]
      .filter(Boolean)
      .some(value => String(value).toLowerCase().includes(lowerSearch))
  })
}

const filteredSourceLocations = computed(() => {
  return filterLocations(
    // Why: 人工创建任务页先放开起点选择，只保留“已被目标位占用不能重复选同一格”的基础防呆，
    // 这样现场可直接手动指定起点，不再被空置/锁定状态提前阻断。
    availableLocations.value.filter(loc => loc.enabled),
    sourceSearch.value
  )
})

const filteredTargetLocations = computed(() => {
  return filterLocations(
    availableLocations.value.filter(loc => loc.enabled),
    targetSearch.value
  ).sort((left, right) => {
    const leftOrder = left.recommendationOrder ?? Number.MAX_SAFE_INTEGER
    const rightOrder = right.recommendationOrder ?? Number.MAX_SAFE_INTEGER
    return leftOrder - rightOrder
  })
})

const selectSource = (loc: typeof availableLocations.value[number]) => {
  const val = loc.nodeRemark
  if (!val) {
    message.warning(t('task.noRemark'))
    return
  }

  if (formState.targetPosition === val) {
    message.warning(t('task.targetOccupied'))
    return
  }

  formState.sourcePosition = formState.sourcePosition === val ? '' : val
  checkDuplicate()
}

const selectTarget = (loc: typeof availableLocations.value[number]) => {
  const val = loc.nodeRemark
  if (!val) {
    message.warning(t('task.noRemark'))
    return
  }

  if (!loc.isReachableTarget) {
    message.warning('外侧储位未占用时，当前深位储位不可达')
    return
  }

  if (formState.sourcePosition === val) {
    message.warning('该位置已被选为源位置，无法选择')
    return
  }

  formState.targetPosition = formState.targetPosition === val ? '' : val
  checkDuplicate()
}

const checkDuplicate = async () => {
  if (!formState.sourcePosition || !formState.targetPosition) {
    return
  }

  if (formState.sourcePosition === formState.targetPosition) {
    message.error(t('task.samePositionError'))
    return
  }

  try {
    const response = await taskService.checkDuplicateTask(formState.sourcePosition, formState.targetPosition)
    if (response.success && response.data?.isDuplicate) {
      message.warning(t('task.checkDuplicate'))
    }
  } catch (error) {
    console.error('检查重复任务失败:', error)
  }
}

const handleSubmit = async () => {
  if (!formState.sourcePosition || !formState.targetPosition) {
    message.error(t('task.selectBothError'))
    return
  }

  if (formState.sourcePosition === formState.targetPosition) {
    message.error(t('task.samePositionError'))
    return
  }

  isSubmitting.value = true
  try {
    const response = await taskService.createTask({
      sourcePosition: formState.sourcePosition,
      targetPosition: formState.targetPosition,
      materialCode: '',
      taskType: formState.taskType,
      priority: formState.priority,
    })

    if (response.success) {
      message.success(t('task.createSuccess'))
      router.push('/tasks')
    } else {
      message.error(response.message || t('common.fail'))
    }
  } catch (error: any) {
    message.error(error.message || t('common.fail'))
  } finally {
    isSubmitting.value = false
  }
}

const handleCancel = () => {
  router.back()
}
</script>

<style scoped>
.task-create-container {
  width: 100%;
  height: calc(100vh - 120px);
  display: flex;
  flex-direction: column;
}

:deep(.ant-card) {
  display: flex;
  flex-direction: column;
  height: 100%;
}

:deep(.ant-card-body) {
  flex: 1;
  display: flex;
  flex-direction: column;
  padding: 12px;
  overflow: hidden;
}

.selection-summary {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 20px;
  padding: 10px;
  background: #f5f5f5;
  border-radius: 8px;
  margin-bottom: 10px;
}

.summary-step {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 8px 16px;
  border-radius: 6px;
  min-width: 120px;
  background: #fff;
  border: 1px solid #d9d9d9;
  transition: all 0.3s;
}

.summary-step.active {
  border-color: #1890ff;
  box-shadow: 0 0 0 2px rgba(24, 144, 255, 0.2);
}

.summary-step.completed {
  background: #e6f7ff;
  border-color: #1890ff;
}

.summary-step .label {
  font-size: 12px;
  color: #888;
}

.summary-step .value {
  font-weight: bold;
  font-size: 16px;
  color: #333;
}

.arrow {
  font-size: 24px;
  color: #ccc;
  font-weight: bold;
}

.task-create-form {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

.selection-panels {
  display: flex;
  flex: 1;
  gap: 16px;
  overflow: hidden;
  min-height: 0;
}

.panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  background: #fff;
}

.panel-header {
  padding: 10px;
  background: #fafafa;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.panel-header .title {
  font-weight: bold;
}

.grid-container {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(130px, 1fr));
  gap: 12px;
  align-content: start;
}

.grid-item {
  background: #fff;
  border: 1px solid #e8e8e8;
  border-radius: 6px;
  padding: 10px 8px;
  text-align: center;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  align-items: center;
  min-height: 110px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
}

.grid-item:hover:not(.disabled) {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  border-color: #1890ff;
  z-index: 1;
}

.grid-item.disabled {
  background: #f5f5f5;
  border-color: #d9d9d9;
  cursor: not-allowed;
  opacity: 0.6;
}

.grid-item.selected {
  background: #e6f7ff;
  border-color: #1890ff;
  border-width: 2px;
  box-shadow: 0 2px 8px rgba(24, 144, 255, 0.15);
}

.loc-name {
  font-weight: 600;
  font-size: 13px;
  color: #333;
  margin-bottom: 4px;
  word-break: break-all;
}

.loc-group,
.loc-depth {
  font-size: 11px;
  color: #666;
}

.loc-status {
  width: 100%;
  display: flex;
  justify-content: center;
  margin-top: 8px;
}

.indicator {
  font-size: 10px;
  padding: 2px 8px;
  border-radius: 10px;
  font-weight: 500;
}

.indicator.locked {
  background: #fff1f0;
  color: #cf1322;
  border: 1px solid #ffa39e;
}

.indicator.full {
  background: #e6fffb;
  color: #08979c;
  border: 1px solid #87e8de;
}

.indicator.empty {
  background: #f6ffed;
  color: #389e0d;
  border: 1px solid #b7eb8f;
}

.indicator.recommended {
  background: #e6f4ff;
  color: #0958d9;
  border: 1px solid #91caff;
}

.indicator.blocked {
  background: #fff7e6;
  color: #d48806;
  border: 1px solid #ffd591;
}

.grid-item.status-locked:not(.selected) {
  border-left: 3px solid #cf1322;
}

.grid-item.status-full:not(.selected) {
  border-left: 3px solid #08979c;
}

.grid-item.status-empty:not(.selected) {
  border-left: 3px solid #389e0d;
}

.grid-item.status-recommended:not(.selected) {
  border-left: 3px solid #0958d9;
}

.grid-item.status-blocked:not(.selected) {
  border-left: 3px solid #d48806;
}

.empty-state {
  grid-column: 1 / -1;
  text-align: center;
  padding: 20px;
  color: #999;
}

.form-actions {
  margin-top: 16px;
  display: flex;
  justify-content: center;
}

.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
}

.custom-scrollbar::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #ccc;
  border-radius: 3px;
}

.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: #999;
}
</style>
