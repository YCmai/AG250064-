<template>
  <div class="pda-binding-page">
    <div class="pda-shell">
      <div class="page-head">
        <a-button type="link" class="back-button" @click="goBack">
          {{ t('pdaBinding.backToDashboard') }}
        </a-button>
      </div>

      <div class="hero">
        <div>
          <div class="eyebrow">{{ t('pdaBinding.eyebrow') }}</div>
          <h1>{{ t('pdaBinding.title') }}</h1>
          <p>{{ t('pdaBinding.subtitle') }}</p>
        </div>
      </div>

      <a-card class="binding-card" :bordered="false">
        <a-form layout="vertical">
          <a-form-item :label="t('pdaBinding.workOrderLabel')">
            <a-select
              v-model:value="formState.orderNumber"
              :options="workOrderOptions"
              :placeholder="t('pdaBinding.workOrderPlaceholder')"
              show-search
              :filter-option="filterWorkOrders"
              :loading="isLoadingOrders"
            />
          </a-form-item>

          <a-form-item :label="t('pdaBinding.palletNumberLabel')">
            <a-input
              ref="palletInputRef"
              v-model:value="formState.palletNumber"
              :placeholder="t('pdaBinding.palletNumberPlaceholder')"
              size="large"
              allow-clear
              @pressEnter="focusBarcodeInput"
            />
          </a-form-item>

          <a-form-item :label="t('pdaBinding.barcodeLabel')">
            <a-input
              ref="barcodeInputRef"
              v-model:value="formState.barcode"
              :placeholder="t('pdaBinding.barcodePlaceholder')"
              size="large"
              allow-clear
              @pressEnter="handleSubmit"
            />
          </a-form-item>

          <a-alert
            v-if="selectedWorkOrder"
            type="info"
            show-icon
            :message="selectedWorkOrder.materialName"
            :description="`${selectedWorkOrder.orderNumber} / ${selectedWorkOrder.materialNumber}`"
            style="margin-bottom: 16px"
          />

          <div class="action-row">
            <a-button size="large" @click="handleReset">
              {{ t('common.reset') }}
            </a-button>
            <a-button type="primary" size="large" :loading="isSubmitting" @click="handleSubmit">
              {{ t('pdaBinding.bindButton') }}
            </a-button>
          </div>
        </a-form>
      </a-card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import pdaBindingService, { PdaWorkOrderOption } from '@/services/pdaBinding'

const { t } = useI18n()
const router = useRouter()

const palletInputRef = ref()
const barcodeInputRef = ref()
const isLoadingOrders = ref(false)
const isSubmitting = ref(false)
const workOrders = ref<PdaWorkOrderOption[]>([])

const formState = reactive({
  orderNumber: '',
  palletNumber: '',
  barcode: '',
})

const workOrderOptions = computed(() =>
  workOrders.value.map(item => ({
    label: item.displayLabel,
    value: item.orderNumber,
  }))
)

const selectedWorkOrder = computed(() =>
  workOrders.value.find(item => item.orderNumber === formState.orderNumber) || null
)

onMounted(async () => {
  await loadWorkOrders()
  focusPalletInput()
})

const loadWorkOrders = async () => {
  isLoadingOrders.value = true
  try {
    const response = await pdaBindingService.getWorkOrders()
    if (response.success && response.data) {
      workOrders.value = response.data
    } else {
      message.error(response.message || t('pdaBinding.loadOrdersFailed'))
    }
  } catch (error: any) {
    message.error(error.message || t('pdaBinding.loadOrdersFailed'))
  } finally {
    isLoadingOrders.value = false
  }
}

const filterWorkOrders = (input: string, option: any) => {
  return String(option?.label || '')
    .toLowerCase()
    .includes(input.toLowerCase())
}

const handleReset = () => {
  formState.orderNumber = ''
  formState.palletNumber = ''
  formState.barcode = ''
  focusPalletInput()
}

const handleSubmit = () => {
  if (!formState.orderNumber) {
    message.warning(t('pdaBinding.workOrderRequired'))
    return
  }

  if (!formState.palletNumber) {
    message.warning(t('pdaBinding.palletNumberRequired'))
    return
  }

  if (!formState.barcode) {
    message.warning(t('pdaBinding.barcodeRequired'))
    return
  }

  Modal.confirm({
    title: t('pdaBinding.confirmTitle'),
    content: t('pdaBinding.confirmContent', {
      orderNumber: formState.orderNumber,
      palletNumber: formState.palletNumber,
      barcode: formState.barcode,
    }),
    okText: t('common.confirm'),
    cancelText: t('common.cancel'),
    async onOk() {
      await submitBinding()
    },
  })
}

const submitBinding = async () => {
  isSubmitting.value = true
  try {
    const response = await pdaBindingService.createBinding({
      orderNumber: formState.orderNumber,
      palletNumber: formState.palletNumber,
      barcode: formState.barcode,
    })

    if (response.success) {
      message.success(
        t('pdaBinding.bindSuccess', {
          requestCode: response.data?.requestCode || '-',
        })
      )
      handleReset()
      await loadWorkOrders()
    } else {
      message.error(response.message || t('pdaBinding.bindFailed'))
    }
  } catch (error: any) {
    message.error(error.message || t('pdaBinding.bindFailed'))
  } finally {
    isSubmitting.value = false
  }
}

const focusPalletInput = () => {
  nextTick(() => {
    palletInputRef.value?.focus?.()
  })
}

const focusBarcodeInput = () => {
  nextTick(() => {
    barcodeInputRef.value?.focus?.()
  })
}

const goBack = () => {
  router.push('/')
}
</script>

<style scoped>
.pda-binding-page {
  min-height: 100vh;
  display: flex;
  justify-content: center;
  align-items: flex-start;
  background:
    radial-gradient(circle at top left, rgba(214, 237, 255, 0.9), transparent 36%),
    linear-gradient(180deg, #f4f8f3 0%, #eef2f7 100%);
  padding: 20px 12px 40px;
}

.pda-shell {
  width: min(100%, 560px);
}

.page-head {
  display: flex;
  justify-content: flex-start;
  margin-bottom: 10px;
}

.back-button {
  padding-inline: 0;
  color: #1f5d7a;
}

.hero {
  background: linear-gradient(135deg, #19456b 0%, #2c7a7b 100%);
  color: #fff;
  border-radius: 24px;
  padding: 24px 22px;
  box-shadow: 0 20px 45px rgba(25, 69, 107, 0.2);
  margin-bottom: 16px;
}

.hero h1 {
  margin: 6px 0 8px;
  font-size: clamp(28px, 5vw, 38px);
  line-height: 1.1;
  font-weight: 700;
}

.hero p {
  margin: 0;
  font-size: 14px;
  color: rgba(255, 255, 255, 0.84);
}

.eyebrow {
  font-size: 12px;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.7);
}

.binding-card {
  border-radius: 24px;
  box-shadow: 0 18px 40px rgba(36, 47, 78, 0.08);
}

.action-row {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  flex-wrap: wrap;
}

@media (max-width: 640px) {
  .pda-binding-page {
    padding: 12px 10px 24px;
  }

  .hero {
    padding: 20px 16px;
    border-radius: 18px;
  }

  .binding-card {
    border-radius: 18px;
  }

  .action-row {
    flex-direction: column-reverse;
  }

  .action-row :deep(.ant-btn) {
    width: 100%;
  }
}
</style>
