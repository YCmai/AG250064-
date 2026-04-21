import { createRouter, createWebHistory, RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/LoginView.vue'),
    meta: { requiresAuth: false },
  },
  {
    path: '/',
    component: () => import('@/layouts/MainLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        name: 'Dashboard',
        component: () => import('@/views/DashboardView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'locations',
        name: 'Locations',
        component: () => import('@/views/LocationsView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'locations/create',
        name: 'LocationCreate',
        component: () => import('@/views/LocationCreateEditView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'locations/:id/edit',
        name: 'LocationEdit',
        component: () => import('@/views/LocationCreateEditView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'tasks',
        name: 'Tasks',
        component: () => import('@/views/TasksView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'integration-data',
        name: 'IntegrationData',
        component: () => import('@/views/IntegrationDataView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'tasks/create',
        name: 'TaskCreate',
        component: () => import('@/views/TaskCreateView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'tasks/:id',
        name: 'TaskDetail',
        component: () => import('@/views/TaskDetailView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'plc/signal-management',
        name: 'PlcSignalManagement',
        component: () => import('@/views/PlcSignalManagementView.vue'),
        meta: { requiresAuth: true, title: 'PLC信号管理' },
      },
      {
        path: 'plc/signal-display',
        name: 'PlcSignalDisplay',
        component: () => import('@/views/PlcSignalDisplayView.vue'),
        meta: { requiresAuth: true, title: 'PLC信号显示' },
      },
      {
        path: 'plc/task-management',
        name: 'PlcTaskManagement',
        component: () => import('@/views/PlcTaskManagementView.vue'),
        meta: { requiresAuth: true, title: 'PLC交互任务管理' },
      },
      {
        path: 'io/signal-management',
        name: 'IoSignalManagement',
        component: () => import('@/views/IoSignalManagementView.vue'),
        meta: { requiresAuth: true, title: 'IO信号管理' },
      },
      {
        path: 'sys-api-management',
        name: 'ApiManagement',
        component: () => import('@/views/ApiManagementView.vue'),
        meta: { requiresAuth: true, title: 'API管理' },
      },
      {
        path: 'user-management',
        name: 'UserManagement',
        component: () => import('@/views/UserManagementView.vue'),
        meta: { requiresAuth: true },
      },
      {
        path: 'setting',
        name: 'Setting',
        component: () => import('@/views/SettingView.vue'),
        meta: { requiresAuth: true },
      },
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/',
  },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

// 路由守卫
router.beforeEach((to, _from, next) => {
  const authStore = useAuthStore()
  const requiresAuth = to.matched.some((record) => record.meta.requiresAuth)

  if (requiresAuth && !authStore.isAuthenticated) {
    // 需要认证但未登录，重定向到登录页
    next('/login')
  } else if (to.path === '/login' && authStore.isAuthenticated) {
    // 已登录用户访问登录页，重定向到首页
    next('/')
  } else {
    next()
  }
})

export default router
