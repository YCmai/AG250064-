<template>
  <div class="log-center-container">
    <a-card :bordered="false" class="log-card">
      <!-- Title and Toolbar Header -->
      <template #title>
        <div class="log-header-title">
          <span class="title-text">{{ t('logs.title') }}</span>
          <a-tag color="processing" v-if="filteredLines.length > 0">
            {{ t('logs.totalLines', { n: filteredLines.length }) }}
          </a-tag>
        </div>
      </template>

      <template #extra>
        <a-space wrap class="log-controls">
          <!-- Log File Selector -->
          <div class="control-item">
            <span class="control-label">{{ t('logs.selectFile') }}:</span>
            <a-select
              v-model:value="selectedFile"
              style="width: 260px"
              :placeholder="t('logs.selectFile')"
              :loading="loadingFiles"
              @change="fetchLogContent"
            >
              <a-select-option v-for="file in logFiles" :key="file.filename" :value="file.filename">
                <div class="file-option">
                  <span class="file-name">{{ file.filename }}</span>
                  <span class="file-meta">({{ formatBytes(file.size) }})</span>
                </div>
              </a-select-option>
            </a-select>
          </div>

          <!-- Log Level Filter -->
          <div class="control-item">
            <a-select v-model:value="selectedLevel" style="width: 140px" @change="filterLogs">
              <a-select-option value="ALL">{{ t('logs.levelAll') }}</a-select-option>
              <a-select-option value="DEBUG">DEBUG</a-select-option>
              <a-select-option value="INFO">INFO</a-select-option>
              <a-select-option value="WARN">WARNING</a-select-option>
              <a-select-option value="ERROR">ERROR</a-select-option>
            </a-select>
          </div>

          <!-- Text Search Filter -->
          <div class="control-item">
            <a-input
              v-model:value="searchText"
              :placeholder="t('logs.searchPlaceholder')"
              allow-clear
              style="width: 240px"
              @input="filterLogs"
            >
              <template #prefix><SearchOutlined style="color: rgba(0,0,0,0.45)" /></template>
            </a-input>
          </div>

          <!-- Load Full Log Toggle -->
          <a-tooltip :title="t('logs.limitTip')" placement="bottom">
            <a-button
              :type="loadAll ? 'primary' : 'default'"
              @click="toggleLoadAll"
              class="glow-button"
            >
              {{ t('logs.loadAllBtn') }}
            </a-button>
          </a-tooltip>

          <!-- Refresh Button -->
          <a-button @click="fetchLogContent" :loading="loadingContent">
            <template #icon><ReloadOutlined /></template>
            {{ t('logs.refreshBtn') }}
          </a-button>

          <!-- Auto Refresh Switch -->
          <div class="control-item auto-refresh-switch">
            <a-switch
              v-model:checked="autoRefresh"
              checked-children="Auto"
              un-checked-children="Off"
              @change="toggleAutoRefresh"
            />
          </div>

          <!-- Fullscreen Button -->
          <a-button @click="toggleFullscreen">
            <template #icon>
              <FullscreenOutlined v-if="!isFullscreen" />
              <FullscreenExitOutlined v-else />
            </template>
          </a-button>
        </a-space>
      </template>

      <!-- Terminal Body -->
      <div
        class="terminal-wrapper"
        :class="{ 'is-fullscreen': isFullscreen, 'dark-terminal': isDarkTheme }"
        ref="terminalRef"
      >
        <!-- Floating Close Button when Fullscreen -->
        <a-button
          v-if="isFullscreen"
          class="terminal-exit-btn"
          type="primary"
          shape="circle"
          size="large"
          @click="toggleFullscreen"
        >
          <template #icon><FullscreenExitOutlined /></template>
        </a-button>

        <!-- Loading State -->
        <div class="terminal-overlay" v-if="loadingContent">
          <a-spin size="large" :tip="t('common.loading')" />
        </div>

        <!-- Terminal Console lines -->
        <div class="terminal-body" ref="terminalBodyRef">
          <div v-if="filteredLines.length === 0" class="terminal-empty">
            {{ t('logs.emptyLogs') }}
          </div>
          <div
            v-else
            v-for="(line, idx) in filteredLines"
            :key="idx"
            class="terminal-line"
            :style="{
              color: getLineColor(line),
              backgroundColor: getLineBg(line)
            }"
          >
            <span class="line-number">{{ idx + 1 }}</span>
            <span class="line-text">{{ line }}</span>
          </div>
        </div>
      </div>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, computed, watch, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import { message, theme } from 'ant-design-vue'
import {
  SearchOutlined,
  ReloadOutlined,
  FullscreenOutlined,
  FullscreenExitOutlined
} from '@ant-design/icons-vue'
import { logService, LogFile } from '@/services/log'
import { useSettingStore } from '@/stores/setting'

const { t } = useI18n()
const settingStore = useSettingStore()
const { useToken } = theme
const { token } = useToken()

const isDarkTheme = computed(() => settingStore.currentTheme === 'dark')

// Log data states
const logFiles = ref<LogFile[]>([])
const selectedFile = ref<string>('')
const logLines = ref<string[]>([])
const filteredLines = ref<string[]>([])

// Query filters
const selectedLevel = ref<string>('ALL')
const searchText = ref<string>('')
const loadAll = ref<boolean>(false)

// UI and control states
const loadingFiles = ref<boolean>(false)
const loadingContent = ref<boolean>(false)
const autoRefresh = ref<boolean>(false)
const isFullscreen = ref<boolean>(false)

const terminalRef = ref<HTMLElement | null>(null)
const terminalBodyRef = ref<HTMLElement | null>(null)

let refreshTimer: number | null = null

const handleKeyDown = (e: KeyboardEvent) => {
  if (e.key === 'Escape' && isFullscreen.value) {
    isFullscreen.value = false
  }
}

// Load initial states
onMounted(async () => {
  await fetchLogFiles()
  window.addEventListener('keydown', handleKeyDown)
})

onBeforeUnmount(() => {
  stopAutoRefresh()
  window.removeEventListener('keydown', handleKeyDown)
})

// Fetch files from server
const fetchLogFiles = async () => {
  loadingFiles.value = true
  try {
    const files = await logService.getLogFiles()
    logFiles.value = files
    if (files.length > 0) {
      // Default to the first one (most recent daily file)
      selectedFile.value = files[0].filename
      await fetchLogContent()
    }
  } catch (error: any) {
    message.error(t('logs.loadFail') + ': ' + (error.message || ''))
  } finally {
    loadingFiles.value = false
  }
}

// Fetch log content
const fetchLogContent = async () => {
  if (!selectedFile.value) return
  
  // Only show full loader if not auto-refreshing in background to avoid screen flickering
  if (!autoRefresh.value) {
    loadingContent.value = true
  }

  try {
    const limit = loadAll.value ? undefined : 1000
    const lines = await logService.getLogContent(selectedFile.value, limit)
    logLines.value = lines
    filterLogs()
    
    // Auto scroll to bottom on initial load or if already at bottom
    if (!autoRefresh.value) {
      await nextTick()
      scrollToBottom()
    }
  } catch (error: any) {
    message.error(t('logs.loadFail') + ': ' + (error.message || ''))
  } finally {
    loadingContent.value = false
  }
}

// Filter logs locally
const filterLogs = () => {
  let result = logLines.value

  // Level filter
  if (selectedLevel.value !== 'ALL') {
    const levelTag = getLevelSearchTag(selectedLevel.value)
    result = result.filter(line => line.includes(levelTag))
  }

  // Text keyword search (case-insensitive)
  if (searchText.value) {
    const query = searchText.value.toLowerCase()
    result = result.filter(line => line.toLowerCase().includes(query))
  }

  filteredLines.value = result
}

// Map selection levels to log severity brackets
const getLevelSearchTag = (level: string) => {
  switch (level) {
    case 'DEBUG': return '[DBG]'
    case 'INFO': return '[INF]'
    case 'WARN': return '[WRN]'
    case 'ERROR': return '[ERR]'
    default: return ''
  }
}

// Color coding for monospace console log output
const getLineColor = (line: string): string => {
  if (line.includes('[ERR]') || line.includes('[ERROR]') || line.includes('Exception:')) {
    return '#ff4d4f' // Soft bright red
  }
  if (line.includes('[WRN]') || line.includes('[WARNING]')) {
    return '#ffa940' // Soft bright orange
  }
  if (line.includes('[INF]') || line.includes('[INFO]')) {
    return '#73d13d' // Soft bright green
  }
  if (line.includes('[DBG]') || line.includes('[DEBUG]')) {
    return '#40a9ff' // Soft bright blue
  }
  return isDarkTheme.value ? '#cfd8dc' : '#333333'
}

const getLineBg = (line: string): string => {
  if (line.includes('[ERR]') || line.includes('[ERROR]')) {
    return 'rgba(255, 77, 79, 0.08)'
  }
  if (line.includes('[WRN]') || line.includes('[WARNING]')) {
    return 'rgba(255, 169, 64, 0.06)'
  }
  return 'transparent'
}

// Formatter helper for bytes
const formatBytes = (bytes: number): string => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

// Toggle showing full file instead of last 1000 lines
const toggleLoadAll = () => {
  loadAll.value = !loadAll.value
  fetchLogContent()
}

// Toggle auto refresh
const toggleAutoRefresh = (checked: boolean) => {
  if (checked) {
    startAutoRefresh()
  } else {
    stopAutoRefresh()
  }
}

const startAutoRefresh = () => {
  stopAutoRefresh()
  refreshTimer = window.setInterval(async () => {
    await fetchLogContent()
  }, 5000)
}

const stopAutoRefresh = () => {
  if (refreshTimer) {
    window.clearInterval(refreshTimer)
    refreshTimer = null
  }
}

// Scroll terminal body to bottom
const scrollToBottom = () => {
  if (terminalBodyRef.value) {
    terminalBodyRef.value.scrollTop = terminalBodyRef.value.scrollHeight
  }
}

// Fullscreen toggle logic
const toggleFullscreen = () => {
  isFullscreen.value = !isFullscreen.value
}

// Watch theme change to update styles
watch(() => settingStore.currentTheme, () => {
  filterLogs()
})
</script>

<script lang="ts">
export default {
  name: 'LogCenterView'
}
</script>

<style scoped>
.log-center-container {
  padding: 0 0 24px 0;
  display: flex;
  flex-direction: column;
  height: calc(100vh - 140px);
}

.log-card {
  display: flex;
  flex-direction: column;
  height: 100%;
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.05);
}

:deep(.ant-card-body) {
  padding: 16px;
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.log-header-title {
  display: flex;
  align-items: center;
  gap: 12px;
}

.title-text {
  font-size: 18px;
  font-weight: 600;
  color: v-bind('token.colorTextHeading');
}

.log-controls {
  display: flex;
  align-items: center;
}

.control-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.control-label {
  font-size: 13px;
  color: v-bind('token.colorTextDescription');
  white-space: nowrap;
}

.file-option {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.file-name {
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-meta {
  font-size: 11px;
  opacity: 0.65;
  margin-left: 8px;
  flex-shrink: 0;
}

.glow-button {
  transition: all 0.3s ease;
}
.glow-button:hover {
  box-shadow: 0 0 8px v-bind('token.colorPrimary');
}

/* Terminal Console View styling */
.terminal-wrapper {
  position: relative;
  flex: 1;
  border-radius: 8px;
  border: 1px solid v-bind('token.colorBorder');
  background: #fdfdfd;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}

.terminal-wrapper.dark-terminal {
  background: #0f172a; /* Slate 900 for modern aesthetic */
  border-color: #1e293b;
}

.terminal-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.05);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 10;
  border-radius: 8px;
  backdrop-filter: blur(2px);
}

.terminal-wrapper.dark-terminal .terminal-overlay {
  background: rgba(15, 23, 42, 0.7);
}

.terminal-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  font-family: 'Consolas', 'Courier New', Courier, monospace;
  font-size: 13px;
  line-height: 1.6;
  scroll-behavior: smooth;
}

/* Custom Scrollbar for Terminal */
.terminal-body::-webkit-scrollbar {
  width: 10px;
  height: 10px;
}

.terminal-body::-webkit-scrollbar-track {
  background: transparent;
}

.terminal-body::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.15);
  border-radius: 5px;
}

.terminal-wrapper.dark-terminal .terminal-body::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.15);
}

.terminal-body::-webkit-scrollbar-thumb:hover {
  background: rgba(0, 0, 0, 0.3);
}

.terminal-wrapper.dark-terminal .terminal-body::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.3);
}

.terminal-empty {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100%;
  color: v-bind('token.colorTextDisabled');
  font-size: 14px;
}

.terminal-line {
  display: flex;
  padding: 2px 8px;
  border-radius: 3px;
  white-space: pre-wrap;
  word-break: break-all;
  transition: background-color 0.2s ease;
}

.terminal-line:hover {
  background-color: rgba(0, 0, 0, 0.02) !important;
}

.terminal-wrapper.dark-terminal .terminal-line:hover {
  background-color: rgba(255, 255, 255, 0.04) !important;
}

.line-number {
  flex-shrink: 0;
  width: 45px;
  margin-right: 12px;
  text-align: right;
  user-select: none;
  opacity: 0.35;
  color: v-bind('token.colorText');
}

.terminal-wrapper.dark-terminal .line-number {
  color: #475569;
}

.line-text {
  flex: 1;
}

/* Fullscreen Mode */
.terminal-wrapper.is-fullscreen {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw !important;
  height: 100vh !important;
  z-index: 1000;
  border-radius: 0;
  margin: 0;
}

.terminal-exit-btn {
  position: absolute;
  top: 20px;
  right: 30px;
  z-index: 1010;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}

.terminal-exit-btn:hover {
  transform: scale(1.15);
}
</style>
