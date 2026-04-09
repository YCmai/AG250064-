<template>
  <div class="task-create-container">
    <a-card :title="t('task.createTitle')" :body-style="{ padding: '12px' }">
      
      <!-- Selection Summary -->
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
        <!-- Task Type Selection -->
        <a-form-item :label="t('task.type')" name="taskType" style="margin-bottom: 12px" v-show="false">
          <a-select v-model:value="formState.taskType" style="width: 100%">
            <a-select-option v-for="opt in taskTypeOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </a-select-option>
          </a-select>
        </a-form-item>

        <div class="selection-panels">
          <!-- Source Panel -->
          <div class="panel source-panel">
            <div class="panel-header">
              <span class="title">{{ t('task.sourcePosition') }}</span>
              <a-input 
                v-model:value="sourceSearch" 
                :placeholder="t('task.searchSource')" 
                allow-clear 
                size="small"
                style="width: 150px"
              />
            </div>
            <div class="grid-container custom-scrollbar">
              <div 
                v-for="loc in filteredSourceLocations" 
                :key="loc.id"
                class="grid-item" 
                :class="[
                  { 'selected': formState.sourcePosition === loc.nodeRemark },
                  { 'disabled': formState.targetPosition === loc.nodeRemark || loc.isLocked },
                  loc.isLocked ? 'status-locked' : (!loc.isEmpty ? 'status-full' : 'status-empty')
                ]"
                @click="!loc.isLocked && selectSource(loc)"
              >
                <div class="loc-name">{{ loc.nodeRemark }}</div>
                <div class="loc-group" v-if="loc.group">{{ loc.group }}</div>
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

          <!-- Target Panel -->
          <div class="panel target-panel">
            <div class="panel-header">
              <span class="title">{{ t('task.targetPosition') }}</span>
              <a-input 
                v-model:value="targetSearch" 
                :placeholder="t('task.searchTarget')" 
                allow-clear 
                size="small"
                style="width: 150px"
              />
            </div>
            <div class="grid-container custom-scrollbar">
              <div 
                v-for="loc in filteredTargetLocations" 
                :key="loc.id"
                class="grid-item" 
                :class="[
                  { 'selected': formState.targetPosition === loc.nodeRemark },
                  { 'disabled': formState.sourcePosition === loc.nodeRemark || loc.isLocked },
                  loc.isLocked ? 'status-locked' : (!loc.isEmpty ? 'status-full' : 'status-empty')
                ]"
                @click="!loc.isLocked && selectTarget(loc)"
              >
                <div class="loc-name">{{ loc.nodeRemark }}</div>
                <div class="loc-group" v-if="loc.group">{{ loc.group }}</div>
                <div class="loc-status">
                  <span v-if="loc.isLocked" class="indicator locked">锁定</span>
                  <span v-else-if="!loc.isEmpty" class="indicator full">有货</span>
                  <span v-else class="indicator empty">空置</span>
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
import { reactive, ref, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import taskService from '@/services/task'
import { message } from 'ant-design-vue'
import { useI18n } from 'vue-i18n'
import { useSettingStore } from '@/stores/setting'
import { getTaskTypeOptions } from '@/constants/taskType'

const { t } = useI18n()
const router = useRouter()
const settingStore = useSettingStore()

const taskTypeOptions = computed(() => {
  return getTaskTypeOptions(settingStore.systemType)
})

const isSubmitting = ref(false)
const availableLocations = ref<Array<{ id: number; name: string; nodeRemark: string; group: string; isEmpty: boolean }>>([])

const sourceSearch = ref('')
const targetSearch = ref('')

const formState = reactive({
  sourcePosition: '',
  targetPosition: '',
  materialCode: '', // Hidden but kept for compatibility
  taskType: 0, 
  priority: 1, 
})

watch(() => taskTypeOptions.value, (newOptions) => {
  if (newOptions && newOptions.length > 0) {
    // Check if current value is valid
    const isValid = newOptions.some(opt => opt.value === formState.taskType)
    if (!isValid) {
      formState.taskType = newOptions[0].value
    }
  }
}, { immediate: true })

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

// Filter Logic
const filterLocations = (locations: any[], search: string) => {
  if (!search) return locations
  const lowerSearch = search.toLowerCase()
  return locations.filter(loc => {
    const name = loc.name || ''
    const remark = loc.nodeRemark || ''
    const group = loc.group || ''
    return name.toLowerCase().includes(lowerSearch) || 
           remark.toLowerCase().includes(lowerSearch) ||
           group.toLowerCase().includes(lowerSearch)
  })
}

const filteredSourceLocations = computed(() => {
  return filterLocations(availableLocations.value, sourceSearch.value)
})

const filteredTargetLocations = computed(() => {
  return filterLocations(availableLocations.value, targetSearch.value)
})

// Selection Handlers
const selectSource = (loc: any) => {
  const val = loc.nodeRemark
  if (!val) {
    message.warning(t('task.noRemark'))
    return
  }
  
  if (formState.targetPosition === val) {
    message.warning(t('task.targetOccupied'))
    return
  }
  
  if (formState.sourcePosition === val) {
    formState.sourcePosition = '' // Deselect
  } else {
    formState.sourcePosition = val
    // Optional: Auto-focus target search or scroll to target?
  }
  checkDuplicate()
}

const selectTarget = (loc: any) => {
  const val = loc.nodeRemark
  if (!val) {
    message.warning(t('task.noRemark'))
    return
  }

  if (formState.sourcePosition === val) {
    message.warning('该位置已被选为源位置，无法选择')
    return
  }

  if (formState.targetPosition === val) {
    formState.targetPosition = '' // Deselect
  } else {
    formState.targetPosition = val
  }
  checkDuplicate()
}

const checkDuplicate = async () => {
  if (formState.sourcePosition && formState.targetPosition) {
    if (formState.sourcePosition === formState.targetPosition) {
       message.error(t('task.samePositionError'));
       return;
    }
    try {
      const response = await taskService.checkDuplicateTask(
        formState.sourcePosition,
        formState.targetPosition
      )
      if (response.success && response.data?.isDuplicate) {
        message.warning(t('task.checkDuplicate'))
      }
    } catch (error: any) {
      console.error('检查重复任务失败:', error)
    }
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
      materialCode: '', // User requested no material input
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
  height: calc(100vh - 120px); /* Fill available height */
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

/* Selection Summary */
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

/* Panels */
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
  overflow: hidden; /* Ensure scrollbars are inside panels */
  min-height: 0; /* Important for flex child scrolling */
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
  grid-template-columns: repeat(auto-fill, minmax(110px, 1fr));
  gap: 12px;
  align-content: start;
}

.grid-item {
  position: relative;
  background: #fff;
  border: 1px solid #e8e8e8;
  border-radius: 6px;
  padding: 12px 6px;
  text-align: center;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  align-items: center;
  min-height: 90px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.02);
}

.grid-item:hover:not(.disabled) {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.08);
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
  box-shadow: 0 2px 8px rgba(24,144,255,0.15);
}

.grid-item .loc-name {
  font-weight: 600;
  font-size: 13px;
  color: #333;
  margin-bottom: 4px;
  word-break: break-all;
}

.grid-item .loc-group {
  font-size: 10px;
  color: #888;
  margin-bottom: 8px;
  background: #f0f0f0;
  padding: 2px 6px;
  border-radius: 4px;
}

.loc-status {
  width: 100%;
  display: flex;
  justify-content: center;
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

.grid-item.status-locked:not(.selected) {
  border-left: 3px solid #cf1322;
}
.grid-item.status-full:not(.selected) {
  border-left: 3px solid #08979c;
}
.grid-item.status-empty:not(.selected) {
  border-left: 3px solid #389e0d;
}

.empty-state {
  grid-column: 1 / -1;
  text-align: center;
  padding: 20px;
  color: #999;
}

/* Actions */
.form-actions {
  margin-top: 16px;
  display: flex;
  justify-content: center;
}

/* Scrollbar */
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
