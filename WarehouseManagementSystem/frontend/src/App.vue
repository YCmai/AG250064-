<template>
  <a-config-provider 
    :theme="{ 
      algorithm: themeAlgorithm, 
      token: { 
        colorPrimary: '#1890ff',
        colorBgContainer: settingStore.currentTheme === 'dark' ? '#141414' : '#ffffff',
        colorBgLayout: settingStore.currentTheme === 'dark' ? '#000000' : '#f5f5f5',
      } 
    }" 
    :locale="locale"
  >
    <router-view />
  </a-config-provider>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { theme } from 'ant-design-vue';
import zhCN from 'ant-design-vue/es/locale/zh_CN';
import enUS from 'ant-design-vue/es/locale/en_US';
import { useSettingStore } from '@/stores/setting';

const settingStore = useSettingStore();

onMounted(() => {
  settingStore.fetchSettings();
});

const themeAlgorithm = computed(() => {
  return settingStore.currentTheme === 'dark' ? theme.darkAlgorithm : theme.defaultAlgorithm;
});

const locale = computed(() => {
  const lang = settingStore.currentLanguage.toLowerCase();
  return lang === 'en-us' ? enUS : zhCN;
});
</script>

<style scoped>
</style>
