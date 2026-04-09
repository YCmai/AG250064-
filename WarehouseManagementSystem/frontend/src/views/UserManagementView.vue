<template>
  <div class="user-management-container">
    <a-card :title="t('user.title')" :bordered="false" style="margin-top: 20px">
      <template #extra>
        <a-button type="primary" @click="showAddUserModal = true">
          <template #icon><PlusOutlined /></template>
          {{ t('user.addUser') }}
        </a-button>
      </template>

      <a-table
        :columns="userColumns"
        :data-source="userStore.users"
        :loading="userStore.loading"
        :pagination="{ pageSize: 10 }"
        rowKey="id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'isAdmin'">
            <a-tag :color="record.isAdmin ? 'blue' : 'default'">
              {{ record.isAdmin ? t('user.admin') : t('user.generalUser') }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'isActive'">
            <a-switch
              :checked="record.isActive"
              @change="toggleUserStatus(record.id, $event)"
              :loading="record.loading"
            />
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatDate(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="editUser(record)">
                {{ t('common.edit') }}
              </a-button>
              <a-button type="link" size="small" @click="managePermissions(record)">
                {{ t('user.permissions') }}
              </a-button>
              <a-button type="link" size="small" @click="openResetPassword(record)">
                {{ t('user.resetPassword') }}
              </a-button>
              <a-popconfirm
                :title="t('user.deleteConfirm')"
                :ok-text="t('common.confirm')"
                :cancel-text="t('common.cancel')"
                @confirm="deleteUser(record.id)"
              >
                <a-button type="link" danger size="small">{{ t('common.delete') }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 添加/编辑用户模态框 -->
    <a-modal
      v-model:open="showAddUserModal"
      :title="userForm.id ? t('user.editUser') : t('user.addUser')"
      :ok-text="t('common.save')"
      :cancel-text="t('common.cancel')"
      @ok="saveUser"
    >
      <a-form :model="userForm" layout="vertical">
        <a-form-item :label="t('user.username')" required>
          <a-input v-model:value="userForm.username" :placeholder="t('user.inputUsername')" :disabled="!!userForm.id" />
        </a-form-item>
        <a-form-item :label="t('user.email')">
          <a-input v-model:value="userForm.email" type="email" :placeholder="t('user.inputEmail')" />
        </a-form-item>
        <a-form-item :label="t('user.displayName')">
          <a-input v-model:value="userForm.displayName" :placeholder="t('user.inputDisplayName')" />
        </a-form-item>
        <a-form-item v-if="!userForm.id" :label="t('user.password')" required>
          <a-input-password v-model:value="userForm.password" :placeholder="t('user.inputPassword')" />
        </a-form-item>
        <a-form-item :label="t('user.isAdmin')">
          <a-checkbox v-model:checked="userForm.isAdmin">{{ t('common.yes') }}</a-checkbox>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 权限管理模态框 -->
    <a-modal
      v-model:open="showPermissionModal"
      :title="t('user.managePermissions')"
      :ok-text="t('common.save')"
      :cancel-text="t('common.cancel')"
      @ok="handleSavePermissions"
    >
      <div v-if="selectedUser">
        <p>{{ t('user.assigningPermsTo', { username: selectedUser.username }) }}</p>
        <a-transfer
          v-model:target-keys="targetPermissionKeys"
          :data-source="permissionTransferData"
          :titles="[t('user.availablePerms'), t('user.assignedPerms')]"
          :render="item => item.title"
          :list-style="{
            width: '250px',
            height: '300px',
          }"
        />
      </div>
    </a-modal>

    <!-- 重置密码模态框 -->
    <a-modal
      v-model:open="showResetPasswordModal"
      :title="t('user.resetPassword')"
      :ok-text="t('common.confirm')"
      :cancel-text="t('common.cancel')"
      @ok="handleResetPassword"
    >
      <a-form layout="vertical">
        <a-alert :message="t('user.passwordHiddenNote')" type="info" show-icon style="margin-bottom: 16px;" />
        <a-form-item :label="t('user.newPassword')" required>
          <a-input-password v-model:value="newPassword" :placeholder="t('user.inputNewPassword')" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive, computed } from 'vue';
import { message } from 'ant-design-vue';
import { PlusOutlined } from '@ant-design/icons-vue';
import { useUserManagementStore } from '../stores/userManagement';
import type { User, Permission } from '../services/userManagement';
import dayjs from 'dayjs';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
const userStore = useUserManagementStore();

// State
const showAddUserModal = ref(false);
const showPermissionModal = ref(false);
const showResetPasswordModal = ref(false);
const newPassword = ref('');
const selectedUser = ref<User | null>(null);
const targetPermissionKeys = ref<string[]>([]);

const userForm = reactive({
  id: 0,
  username: '',
  email: '',
  displayName: '',
  password: '',
  isAdmin: false,
});

const userColumns = computed(() => [
  { title: 'ID', dataIndex: 'id', key: 'id', width: 80 },
  { title: t('user.username'), dataIndex: 'username', key: 'username' },
  { title: t('user.displayName'), dataIndex: 'displayName', key: 'displayName' },
  { title: t('user.email'), dataIndex: 'email', key: 'email' },
  { title: t('user.role'), key: 'isAdmin', width: 100 },
  { title: t('user.status'), key: 'isActive', width: 100 },
  { title: t('user.createdAt'), key: 'createdAt', width: 180 },
  { title: t('common.operation'), key: 'action', width: 250, align: 'center' },
]);

// Computed
const permissionTransferData = computed(() => {
  return userStore.permissions.map(p => ({
    key: p.id.toString(),
    title: `${p.name} (${p.code})`,
    description: p.description,
  }));
});

// Methods
const loadData = async () => {
  await Promise.all([
    userStore.fetchUsers(),
    userStore.fetchPermissions(),
  ]);
};

const formatDate = (dateStr: string) => {
  if (!dateStr) return '-';
  return dayjs(dateStr).format('YYYY-MM-DD HH:mm:ss');
};

const editUser = (user: User) => {
  userForm.id = user.id;
  userForm.username = user.username;
  userForm.email = user.email || '';
  userForm.displayName = user.displayName || '';
  userForm.password = ''; // 编辑时不显示密码
  userForm.isAdmin = user.isAdmin;
  showAddUserModal.value = true;
};

const saveUser = async () => {
  if (!userForm.username) {
    message.error(t('user.inputUsername'));
    return;
  }
  
  try {
    if (userForm.id) {
      await userStore.updateUser({
        id: userForm.id,
        username: userForm.username,
        email: userForm.email,
        displayName: userForm.displayName,
        isAdmin: userForm.isAdmin,
        isActive: true // 保持原有状态或默认为true，这里简化处理
      } as User);
      message.success(t('user.updateSuccess'));
    } else {
      if (!userForm.password) {
        message.error(t('user.inputPassword'));
        return;
      }
      await userStore.createUser({
        username: userForm.username,
        email: userForm.email,
        displayName: userForm.displayName,
        password: userForm.password,
        isAdmin: userForm.isAdmin,
        isActive: true
      });
      message.success(t('user.createSuccess'));
    }
    showAddUserModal.value = false;
    resetUserForm();
  } catch (err) {
    // 错误处理已在store中完成，这里可以额外提示
  }
};

const resetUserForm = () => {
  userForm.id = 0;
  userForm.username = '';
  userForm.email = '';
  userForm.displayName = '';
  userForm.password = '';
  userForm.isAdmin = false;
};

const deleteUser = async (id: number) => {
  try {
    await userStore.deleteUser(id);
    message.success(t('user.deleteSuccess'));
  } catch (err) {
    // Error handled in store
  }
};

const toggleUserStatus = async (id: number, checked: boolean) => {
  try {
    const user = userStore.users.find(u => u.id === id);
    if (user) user.loading = true; // 需要在User类型中增加loading可选属性
    
    await userStore.toggleUserStatus(id, checked);
    message.success(t('user.statusChangeSuccess'));
    
    // 更新本地状态
    if (user) {
        user.isActive = checked;
        user.loading = false;
    }
  } catch (err) {
    const user = userStore.users.find(u => u.id === id);
    if (user) user.loading = false;
  }
};

const managePermissions = async (user: User) => {
  selectedUser.value = user;
  try {
    const userPerms = await userStore.getUserPermissions(user.id);
    targetPermissionKeys.value = userPerms.map((p: Permission) => p.id.toString());
    showPermissionModal.value = true;
  } catch (err) {
    message.error(t('user.loadPermsFail'));
  }
};

const handleSavePermissions = async () => {
  if (!selectedUser.value) return;
  
  try {
    const permissionIds = targetPermissionKeys.value.map(k => parseInt(k));
    await userStore.assignPermissions(selectedUser.value.id, permissionIds);
    message.success(t('user.assignPermsSuccess'));
    showPermissionModal.value = false;
  } catch (err) {
    // Error handled in store
  }
};

const openResetPassword = (user: User) => {
  selectedUser.value = user;
  newPassword.value = '';
  showResetPasswordModal.value = true;
};

const handleResetPassword = async () => {
  if (!selectedUser.value || !newPassword.value) {
    message.error(t('user.inputNewPassword'));
    return;
  }
  
  try {
    await userStore.resetPassword(selectedUser.value.id, newPassword.value);
    message.success(t('user.resetPasswordSuccess'));
    showResetPasswordModal.value = false;
  } catch (err) {
    // Error handled in store
  }
};

// Lifecycle
onMounted(() => {
  loadData();
});
</script>

<style scoped>
.user-management-container {
  padding: 24px;
}
</style>
