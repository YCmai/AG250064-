import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { settingService, SystemSetting, ConnectionSettings } from '../services/setting';
import i18n from '../locales';

const findSettingValue = (settings: SystemSetting[], targetKey: string) => {
  return settings.find(s => s.key.toLowerCase() === targetKey.toLowerCase())?.value;
};

const applyTheme = (themeValue: string) => {
  const theme = themeValue || 'light';
  localStorage.setItem('app_theme', theme);

  if (theme === 'dark') {
    document.body.style.backgroundColor = '#000000';
    document.body.setAttribute('data-theme', 'dark');
  } else {
    document.body.style.backgroundColor = '#f5f5f5';
    document.body.removeAttribute('data-theme');
  }
};

const applyLanguage = (languageValue: string) => {
  const language = languageValue || 'zh-CN';
  localStorage.setItem('app_language', language);
  if (i18n.global.locale.value !== language) {
    i18n.global.locale.value = language as any;
  }
};

const applySystemName = (systemNameValue: string) => {
  const systemName = systemNameValue || '仓库管理系统';
  localStorage.setItem('app_system_name', systemName);
  document.title = systemName;
};

export const useSettingStore = defineStore('setting', () => {
  const settings = ref<SystemSetting[]>([]);
  const connectionSettings = ref<ConnectionSettings>({});
  const systemInfo = ref<any>({});
  const databaseStatus = ref<any>({});

  const savedSystemType = localStorage.getItem('app_system_type');
  const systemType = ref<'Heartbeat' | 'NDC'>(
    savedSystemType === 'Heartbeat' || savedSystemType === 'NDC' ? savedSystemType : 'Heartbeat'
  );

  const loading = ref(false);
  const error = ref<string | null>(null);

  const applyAppearanceSettings = () => {
    applySystemName(findSettingValue(settings.value, 'SystemName') || localStorage.getItem('app_system_name') || '仓库管理系统');
    applyTheme(findSettingValue(settings.value, 'Theme') || localStorage.getItem('app_theme') || 'light');
    applyLanguage(findSettingValue(settings.value, 'Language') || localStorage.getItem('app_language') || 'zh-CN');

    const typeValue = findSettingValue(settings.value, 'SystemType') || localStorage.getItem('app_system_type') || 'Heartbeat';
    if (typeValue === 'Heartbeat' || typeValue === 'NDC') {
      systemType.value = typeValue;
      localStorage.setItem('app_system_type', typeValue);
    }
  };

  const fetchSettings = async () => {
    loading.value = true;
    error.value = null;
    try {
      settings.value = await settingService.getAllSettings();
      console.log('[SettingStore] Normalized settings from backend:', JSON.stringify(settings.value));
      applyAppearanceSettings();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取系统设置失败';
    } finally {
      loading.value = false;
    }
  };

  const currentTheme = computed(() => {
    return findSettingValue(settings.value, 'Theme') || localStorage.getItem('app_theme') || 'light';
  });

  const primaryColor = computed(() => {
    return findSettingValue(settings.value, 'PrimaryColor') || '#1890ff';
  });

  const currentLanguage = computed(() => {
    return findSettingValue(settings.value, 'Language') || localStorage.getItem('app_language') || 'zh-CN';
  });

  const getBoolSetting = (key: string, defaultValue = true) => {
    const val = findSettingValue(settings.value, key);
    if (val === undefined || val === null) return defaultValue;
    return ['1', 'true', 'yes', 'on'].includes(String(val).trim().toLowerCase());
  };

  const ioEnabled = computed(() => getBoolSetting('IOProcessorEnabled', true));
  const plcEnabled = computed(() => getBoolSetting('PlcCommunicationEnabled', true));
  const apiEnabled = computed(() => getBoolSetting('ApiTaskProcessorEnabled', true));

  const fetchConnectionSettings = async () => {
    loading.value = true;
    error.value = null;
    try {
      connectionSettings.value = await settingService.getConnectionSettings();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取连接设置失败';
    } finally {
      loading.value = false;
    }
  };

  const fetchSystemInfo = async () => {
    try {
      systemInfo.value = await settingService.getSystemInfo();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取系统信息失败';
    }
  };

  const fetchDatabaseStatus = async () => {
    try {
      databaseStatus.value = await settingService.getDatabaseStatus();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取数据库状态失败';
    }
  };

  const backupDatabase = async () => {
    try {
      loading.value = true;
      const result = await settingService.backupDatabase();
      await fetchDatabaseStatus();
      return result;
    } catch (err) {
      error.value = err instanceof Error ? err.message : '备份失败';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const openBackupFolder = async () => {
    try {
      await settingService.openBackupFolder();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '打开文件夹失败';
      throw err;
    }
  };

  const getSetting = async (key: string) => {
    try {
      return await settingService.getSetting(key);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取设置失败';
      throw err;
    }
  };

  const updateSetting = async (key: string, value: string) => {
    try {
      loading.value = true;
      await settingService.updateSetting(key, value);

      const existing = settings.value.find(s => s.key.toLowerCase() === key.toLowerCase());
      if (existing) {
        existing.value = value;
      } else {
        settings.value.push({ key, value });
      }

      if (key.toLowerCase() === 'systemtype' && (value === 'Heartbeat' || value === 'NDC')) {
        systemType.value = value;
        localStorage.setItem('app_system_type', value);
      }

      if (key.toLowerCase() === 'systemname') {
        applySystemName(value);
      }

      if (key.toLowerCase() === 'theme') {
        applyTheme(value);
      }

      if (key.toLowerCase() === 'language') {
        applyLanguage(value);
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : '更新设置失败';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const saveConnectionSettings = async (settingsToSave: ConnectionSettings) => {
    try {
      await settingService.saveConnectionSettings(settingsToSave);
      connectionSettings.value = settingsToSave;
    } catch (err) {
      error.value = err instanceof Error ? err.message : '保存连接设置失败';
      throw err;
    }
  };

  const testDatabaseConnection = async (settingsToTest: ConnectionSettings) => {
    try {
      return await settingService.testDatabaseConnection(settingsToTest);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '测试数据库连接失败';
      throw err;
    }
  };

  const exportSettings = async () => {
    try {
      const blob = await settingService.exportSettings();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `settings-${new Date().toISOString().split('T')[0]}.json`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (err) {
      error.value = err instanceof Error ? err.message : '导出设置失败';
      throw err;
    }
  };

  const importSettings = async (file: File) => {
    try {
      await settingService.importSettings(file);
      await fetchSettings();
    } catch (err) {
      error.value = err instanceof Error ? err.message : '导入设置失败';
      throw err;
    }
  };

  const clearError = () => {
    error.value = null;
  };

  return {
    settings,
    currentTheme,
    primaryColor,
    currentLanguage,
    connectionSettings,
    systemInfo,
    databaseStatus,
    systemType,
    loading,
    error,
    fetchSettings,
    fetchConnectionSettings,
    fetchSystemInfo,
    fetchDatabaseStatus,
    backupDatabase,
    openBackupFolder,
    getSetting,
    updateSetting,
    saveConnectionSettings,
    testDatabaseConnection,
    exportSettings,
    importSettings,
    clearError,
    ioEnabled,
    plcEnabled,
    apiEnabled,
  };
});
