<template>
  <div class="login-container">
    <a-card class="login-card" :title="t('login.title')">
      <a-form
        :model="formState"
        layout="vertical"
        @finish="handleLogin"
        autocomplete="off"
      >
        <a-form-item
          name="username"
          :label="t('login.username')"
          :rules="[{ required: true, message: t('login.inputUsername') }]"
        >
          <a-input
            v-model:value="formState.username"
            :placeholder="t('login.inputUsername')"
            size="large"
          >
            <template #prefix>
              <UserOutlined />
            </template>
          </a-input>
        </a-form-item>

        <a-form-item
          name="password"
          :label="t('login.password')"
          :rules="[{ required: true, message: t('login.inputPassword') }]"
        >
          <a-input-password
            v-model:value="formState.password"
            :placeholder="t('login.inputPassword')"
            size="large"
          >
            <template #prefix>
              <LockOutlined />
            </template>
          </a-input-password>
        </a-form-item>

        <a-form-item name="rememberMe" :value-prop-name="'checked'">
          <a-checkbox v-model:checked="formState.rememberMe">{{ t('login.rememberMe') }}</a-checkbox>
        </a-form-item>

        <a-form-item>
          <a-button
            type="primary"
            html-type="submit"
            size="large"
            block
            :loading="authStore.isLoading"
          >
            {{ t('login.loginBtn') }}
          </a-button>
        </a-form-item>
      </a-form>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import authService from '@/services/auth'
import { UserOutlined, LockOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { useI18n } from 'vue-i18n'

const router = useRouter()
const authStore = useAuthStore()
const { t } = useI18n()

const formState = reactive({
  username: '',
  password: '',
  rememberMe: false,
})

const handleLogin = async () => {
  authStore.setLoading(true)
  try {
    const response = await authService.login({
      username: formState.username,
      password: formState.password,
      rememberMe: formState.rememberMe,
    })

    if (response.success && response.data) {
      authStore.setToken(response.data.token)
      authStore.setUser(response.data.user)
      message.success(t('login.success'))
      router.push('/')
    } else {
      message.error(response.message || t('login.fail'))
    }
  } catch (error: any) {
    const errorMsg = error.response?.data?.message || error.message || t('login.networkError');
    message.error(errorMsg);
  } finally {
    authStore.setLoading(false)
  }
}
</script>

<style scoped>
.login-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.login-card {
  width: 100%;
  max-width: 400px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
}

:deep(.ant-card-head) {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-color: transparent;
}

:deep(.ant-card-head-title) {
  color: white;
  font-size: 18px;
  font-weight: 600;
}
</style>
