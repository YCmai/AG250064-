<template>
  <div class="setting-container">
    <a-card :title="$t('settings.title')" :bordered="false">
      <a-tabs v-model:activeKey="activeTab">
        <a-tab-pane key="parameters" :tab="$t('settings.businessParams')">
          <a-alert message="这些参数直接影响系统运行逻辑，请谨慎修改。" type="warning" show-icon style="margin-bottom: 24px" />

          <a-form layout="vertical" :model="paramSettings">
            <a-row :gutter="24">
              <a-col :xs="24" :md="12">
                <a-form-item :label="$t('settings.taskTimeout')" help="AGV 或 PLC 任务执行超过此时间将被标记为超时">
                  <a-input-number v-model:value="paramSettings.TaskTimeout" style="width: 100%" :min="10" :max="3600" />
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item :label="$t('settings.maxRetries')" help="任务失败后的自动重试次数">
                  <a-input-number v-model:value="paramSettings.MaxRetries" style="width: 100%" :min="0" :max="10" />
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item :label="$t('settings.refreshInterval')" help="监控页面的数据自动刷新频率">
                  <a-input-number v-model:value="paramSettings.RefreshInterval" style="width: 100%" :min="1" :max="60" />
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12" v-if="false">
                <a-form-item :label="$t('settings.systemType')" help="选择系统运行模式">
                  <a-select v-model:value="paramSettings.SystemType">
                    <a-select-option value="Heartbeat">Heartbeat</a-select-option>
                    <a-select-option value="NDC">NDC</a-select-option>
                  </a-select>
                </a-form-item>
              </a-col>
            </a-row>
            <a-form-item>
              <a-button type="primary" :loading="saving" @click="saveParamSettings">{{ $t('common.save') }}</a-button>
            </a-form-item>
          </a-form>
        </a-tab-pane>

        <a-tab-pane key="appearance" :tab="$t('settings.appearance')">
          <a-form layout="vertical" :model="appearanceSettings">
            <a-row :gutter="24">
              <a-col :xs="24" :md="12">
                <a-form-item :label="$t('settings.systemName')" help="显示在浏览器标题栏和顶部导航栏的名称">
                  <a-input v-model:value="appearanceSettings.SystemName" placeholder="请输入系统名称" />
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item :label="$t('settings.theme')">
                  <a-select v-model:value="appearanceSettings.Theme">
                    <a-select-option value="light">{{ $t('settings.themeLight') }}</a-select-option>
                    <a-select-option value="dark">{{ $t('settings.themeDark') }}</a-select-option>
                  </a-select>
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item :label="$t('settings.language')">
                  <a-select v-model:value="appearanceSettings.Language">
                    <a-select-option value="zh-CN">简体中文</a-select-option>
                    <a-select-option value="en-US">English</a-select-option>
                  </a-select>
                </a-form-item>
              </a-col>
            </a-row>
            <a-form-item>
              <a-button type="primary" :loading="saving" @click="saveAppearanceSettings">{{ $t('common.save') }}</a-button>
            </a-form-item>
          </a-form>
        </a-tab-pane>

        <a-tab-pane key="services" :tab="serviceText.serviceControl">
          <a-alert
            :message="serviceText.serviceControlHint"
            type="info"
            show-icon
            style="margin-bottom: 24px"
          />

          <a-form layout="vertical" :model="serviceSettings">
            <a-row :gutter="24">
              <a-col :xs="24" :md="12">
                <a-form-item :label="serviceText.ioService" :help="serviceText.ioServiceHint">
                  <a-switch
                    v-model:checked="serviceSettings.IOProcessorEnabled"
                    :checked-children="serviceText.enabled"
                    :un-checked-children="serviceText.disabled"
                  />
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item :label="serviceText.plcService" :help="serviceText.plcServiceHint">
                  <a-switch
                    v-model:checked="serviceSettings.PlcCommunicationEnabled"
                    :checked-children="serviceText.enabled"
                    :un-checked-children="serviceText.disabled"
                  />
                </a-form-item>
              </a-col>
              <a-col :xs="24" :md="12">
                <a-form-item :label="serviceText.apiService" :help="serviceText.apiServiceHint">
                  <a-switch
                    v-model:checked="serviceSettings.ApiTaskProcessorEnabled"
                    :checked-children="serviceText.enabled"
                    :un-checked-children="serviceText.disabled"
                  />
                </a-form-item>
              </a-col>
            </a-row>
            <a-form-item>
              <a-button type="primary" :loading="saving" @click="saveServiceSettings">{{ $t('common.save') }}</a-button>
            </a-form-item>
          </a-form>
        </a-tab-pane>

        <a-tab-pane key="database" :tab="$t('settings.database')">
          <a-alert message="数据无价，请定期备份。" type="info" show-icon style="margin-bottom: 24px" />

          <div style="text-align: center; padding: 20px 0;">
            <a-button type="primary" size="large" :loading="backingUp" @click="handleBackup">
              <template #icon><CloudDownloadOutlined /></template>
              立即备份数据库(结构+数据)
            </a-button>

            <div v-if="lastBackupPath" style="margin-top: 16px; padding: 10px; background: #f5f5f5; border-radius: 4px; display: inline-block;">
              <span style="font-weight: bold; color: #52c41a;">备份成功：</span>
              <br />
              文件路径: {{ lastBackupPath }}
            </div>

            <div style="margin-top: 16px; color: #888;" v-if="databaseStatus.lastBackup">
              上次备份时间: {{ databaseStatus.lastBackup }}
            </div>
          </div>
        </a-tab-pane>
      </a-tabs>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { message } from 'ant-design-vue';
import { CloudDownloadOutlined } from '@ant-design/icons-vue';
import { useSettingStore } from '../stores/setting';
import { useI18n } from 'vue-i18n';

const { t, locale } = useI18n();
const settingStore = useSettingStore();
const activeTab = ref('parameters');
const saving = ref(false);
const backingUp = ref(false);
const lastBackupPath = ref('');

const paramSettings = ref<Record<string, any>>({
  TaskTimeout: 300,
  MaxRetries: 3,
  RefreshInterval: 5,
  SystemType: 'NDC'
});

const appearanceSettings = ref<Record<string, any>>({
  SystemName: '仓库管理系统',
  Theme: 'light',
  Language: 'zh-CN'
});

const serviceSettings = ref<Record<string, boolean>>({
  IOProcessorEnabled: true,
  PlcCommunicationEnabled: true,
  ApiTaskProcessorEnabled: true
});

const databaseStatus = ref<any>({});

const serviceLocaleMap = {
  'zh-CN': {
    serviceControl: '服务控制',
    serviceControlHint: '关闭不需要的后台服务后，系统会在几秒内进入待机状态，可明显减少持续异常日志。',
    ioService: 'IO 服务',
    ioServiceHint: '控制 IO 队列处理和 IO 信号缓存刷新。',
    plcService: 'PLC 服务',
    plcServiceHint: '统一控制 PLC 通讯、PLC 任务处理和心跳服务。',
    apiService: '接口任务服务',
    apiServiceHint: '控制接口任务表轮询及外部接口调用。',
    enabled: '启用',
    disabled: '停用',
    serviceControlSaved: '服务控制设置已保存，开关会在几秒内生效。'
  },
  'en-US': {
    serviceControl: 'Service Control',
    serviceControlHint: 'Disable unused background services to let the system enter standby within a few seconds and reduce repeated exception logs.',
    ioService: 'IO Service',
    ioServiceHint: 'Controls IO queue processing and IO signal cache refresh.',
    plcService: 'PLC Service',
    plcServiceHint: 'Controls PLC communication, PLC task processing, and heartbeat together.',
    apiService: 'API Task Service',
    apiServiceHint: 'Controls API task polling and external API calls.',
    enabled: 'On',
    disabled: 'Off',
    serviceControlSaved: 'Service settings saved and will take effect within a few seconds.'
  }
} as const;

const serviceText = computed(() => {
  return locale.value === 'en-US' ? serviceLocaleMap['en-US'] : serviceLocaleMap['zh-CN'];
});

onMounted(async () => {
  await loadAllData();
});

const loadAllData = async () => {
  await Promise.all([
    settingStore.fetchSettings(),
    settingStore.fetchDatabaseStatus()
  ]);

  databaseStatus.value = { ...settingStore.databaseStatus };
  mapSettingsToModel(settingStore.settings, paramSettings.value);
  mapSettingsToModel(settingStore.settings, appearanceSettings.value);
  mapSettingsToModel(settingStore.settings, serviceSettings.value);
};

const mapSettingsToModel = (settings: any[], model: Record<string, any>) => {
  if (!settings) return;

  settings.forEach((s) => {
    const settingKey = s.key ?? s.Key;
    const settingValue = s.value ?? s.Value;
    if (!settingKey) return;

    const targetKey =
      settingKey in model
        ? settingKey
        : Object.keys(model).find((k) => k.toLowerCase() === String(settingKey).toLowerCase());

    if (!targetKey) {
      return;
    }

    if (typeof model[targetKey] === 'number') {
      model[targetKey] = Number(settingValue);
      return;
    }

    if (typeof model[targetKey] === 'boolean') {
      const normalized = String(settingValue).trim().toLowerCase();
      model[targetKey] = ['1', 'true', 'yes', 'on'].includes(normalized);
      return;
    }

    model[targetKey] = settingValue;
  });
};

const saveParamSettings = async () => {
  await saveSettings(paramSettings.value, t('common.success'));
  await settingStore.fetchSettings();
  mapSettingsToModel(settingStore.settings, paramSettings.value);
};

const saveAppearanceSettings = async () => {
  await saveSettings(appearanceSettings.value, t('common.success'));
  await settingStore.fetchSettings();
  mapSettingsToModel(settingStore.settings, appearanceSettings.value);
};

const saveServiceSettings = async () => {
  await saveSettings(serviceSettings.value, serviceText.value.serviceControlSaved);
  await settingStore.fetchSettings();
  mapSettingsToModel(settingStore.settings, serviceSettings.value);
};

const handleBackup = async () => {
  backingUp.value = true;
  lastBackupPath.value = '';
  try {
    const result = await settingStore.backupDatabase();
    lastBackupPath.value = result.path;
    message.success('数据库备份成功');
    await settingStore.fetchDatabaseStatus();
    databaseStatus.value = { ...settingStore.databaseStatus };
  } catch (_err) {
    message.error(t('settings.backupFail'));
  } finally {
    backingUp.value = false;
  }
};

const saveSettings = async (values: Record<string, any>, successMsg: string) => {
  saving.value = true;
  try {
    const promises = Object.keys(values).map((key) => {
      const originalSetting = settingStore.settings.find((s) => s.key.toLowerCase() === key.toLowerCase());
      const backendKey = originalSetting ? originalSetting.key : key;
      const rawValue = values[key];
      const stringValue = typeof rawValue === 'boolean' ? String(rawValue) : String(rawValue ?? '');
      return settingStore.updateSetting(backendKey, stringValue);
    });

    await Promise.all(promises);
    message.success(successMsg);
  } catch (_err) {
    message.error(t('common.fail'));
  } finally {
    saving.value = false;
  }
};
</script>

<style scoped>
.setting-container {
  padding: 24px;
}
</style>


