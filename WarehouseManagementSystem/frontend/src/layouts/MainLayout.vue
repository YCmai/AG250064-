<template>
  <a-layout style="min-height: 100vh">
    <a-layout-sider :theme="menuTheme" :width="200" class="app-sider">
      <div class="logo" :class="{ 'logo-light': menuTheme === 'light' }">
        <h2>{{ systemName }}</h2>
      </div>
      <a-menu
        :theme="menuTheme"
        mode="inline"
        :selected-keys="[activeKey]"
        @click="handleSidebarClick"
      >
        <a-menu-item key="/">
          <template #icon><DashboardOutlined /></template>
          <span>{{ t('menu.dashboard') }}</span>
        </a-menu-item>

        <a-sub-menu key="warehouse">
          <template #icon><AppstoreOutlined /></template>
          <template #title>{{ t('menu.warehouse') }}</template>
          <a-menu-item key="/locations" v-if="authStore.hasPermission('LOCATION_MANAGEMENT')">
            {{ t('menu.locations') }}
          </a-menu-item>
          <a-menu-item key="/tasks" v-if="authStore.hasPermission('TASK_MANAGEMENT')">
            {{ t('menu.tasks') }}
          </a-menu-item>
          <a-menu-item key="/external/workorders">
            外部工单
          </a-menu-item>
          <a-menu-item key="/external/agv-commands">
            AGV指令
          </a-menu-item>
        </a-sub-menu>

        <a-sub-menu key="equipment" v-if="settingStore.plcEnabled || settingStore.ioEnabled || settingStore.apiEnabled">
          <template #icon><CheckCircleOutlined /></template>
          <template #title>{{ t('menu.equipment') }}</template>
          <a-menu-item key="/plc/signal-management" v-if="authStore.hasPermission('PLC_SIGNAL_MANAGEMENT') && settingStore.plcEnabled">
            {{ t('menu.plcSignalManagement') }}
          </a-menu-item>
          <a-menu-item key="/plc/signal-display" v-if="authStore.hasPermission('PLC_SIGNAL_DISPLAY') && settingStore.plcEnabled">
            {{ t('menu.plcSignalDisplay') }}
          </a-menu-item>
          <a-menu-item key="/plc/task-management" v-if="authStore.hasPermission('PLC_TASK_MANAGEMENT') && settingStore.plcEnabled">
            {{ t('menu.plcTaskManagement') }}
          </a-menu-item>
          <a-menu-item key="/io/signal-management" v-if="authStore.hasPermission('IO_SIGNAL_MANAGEMENT') && settingStore.ioEnabled">
            {{ t('menu.ioSignalManagement') }}
          </a-menu-item>
          <a-menu-item key="/sys-api-management" v-if="authStore.hasPermission('API_MANAGEMENT') && settingStore.apiEnabled">
            {{ t('menu.apiManagement') }}
          </a-menu-item>
        </a-sub-menu>

        <a-sub-menu key="system">
          <template #icon><CheckCircleOutlined /></template>
          <template #title>{{ t('menu.system') }}</template>
          <a-menu-item key="/user-management" v-if="authStore.hasPermission('USER_MANAGEMENT')">
            {{ t('menu.userManagement') }}
          </a-menu-item>
          <a-menu-item key="/setting" v-if="authStore.hasPermission('SETTINGS')">
            {{ t('menu.settings') }}
          </a-menu-item>
        </a-sub-menu>
      </a-menu>
    </a-layout-sider>
    <a-layout>
      <a-layout-header class="header">
        <div class="header-right">
          <a-dropdown>
            <template #overlay>
              <a-menu @click="handleProfileClick">
                <a-menu-item key="logout">
                  <template #icon><LogoutOutlined /></template>
                  <span>{{ t('menu.logout') }}</span>
                </a-menu-item>
              </a-menu>
            </template>
            <a-space style="cursor: pointer">
              <a-avatar icon="user" />
              <span>{{ authStore.user?.username || '用户' }}</span>
            </a-space>
          </a-dropdown>
        </div>
      </a-layout-header>
      <a-layout-content class="content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import authService from '@/services/auth'
import {
  DashboardOutlined,
  AppstoreOutlined,
  CheckCircleOutlined,
  LogoutOutlined,
} from '@ant-design/icons-vue'
import { message, theme } from 'ant-design-vue'
import { useSettingStore } from '@/stores/setting'

const router = useRouter()
const route = useRoute()
const { t } = useI18n()
const authStore = useAuthStore()
const settingStore = useSettingStore()
const { useToken } = theme
const { token } = useToken()

const activeKey = computed(() => route.path)
const menuTheme = computed(() => (settingStore.currentTheme === 'dark' ? 'dark' : 'light'))

const systemName = computed(() => {
  return settingStore.settings.find(s => s.key.toLowerCase() === 'systemname')?.value
    || localStorage.getItem('app_system_name')
    || '仓库管理系统'
})

const handleSidebarClick = ({ key }: { key: string }) => {
  if (key && typeof key === 'string' && key.startsWith('/')) {
    router.push(key)
  }
}

const handleProfileClick = async (e: any) => {
  if (e.key === 'logout') {
    try {
      await authService.logout()
    } catch (error) {
      console.error('Logout error:', error)
    } finally {
      authStore.logout()
      router.push('/login')
      message.success('已退出登录')
    }
  }
}
</script>

<style scoped>
.logo {
  padding: 16px;
  color: white;
  text-align: center;
  border-bottom: 1px solid rgba(255, 255, 255, 0.2);
}

.logo-light {
  color: rgba(0, 0, 0, 0.88);
  border-bottom: 1px solid rgba(5, 5, 5, 0.06);
}

.logo h2 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
}

.app-sider {
  border-right: 1px solid v-bind('menuTheme === "dark" ? "rgba(255,255,255,0.08)" : "rgba(5,5,5,0.06)"');
}

.header {
  background: v-bind('token.colorBgContainer');
  padding: 0 24px;
  display: flex;
  justify-content: flex-end;
  align-items: center;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
}

.content {
  padding: 24px;
  background: v-bind('token.colorBgLayout');
  min-height: calc(100vh - 64px);
}
</style>
