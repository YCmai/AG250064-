<template>
  <div class="location-create-edit-container">
    <a-card :title="isEdit ? t('location.editTitle') : t('location.createTitle')">
      <a-form
        :model="formState"
        :rules="rules"
        layout="vertical"
        @finish="handleSubmit"
      >
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('location.name')" name="name">
              <a-input v-model:value="formState.name" :placeholder="t('location.inputName')" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('location.nodeRemark')" name="nodeRemark">
              <a-input v-model:value="formState.nodeRemark" :placeholder="t('location.inputNodeRemark')" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('location.group')" name="group">
              <a-input v-model:value="formState.group" :placeholder="t('location.inputGroup')" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('location.waitingNode')" name="wattingNode">
              <a-input v-model:value="formState.wattingNode" :placeholder="t('location.inputWaitingNode')" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item :label="t('location.liftingHeight')" name="liftingHeight">
              <a-input-number 
                v-model:value="formState.liftingHeight" 
                :placeholder="t('location.inputLiftingHeight')"
                :min="0"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('location.unloadHeight')" name="unloadHeight">
              <a-input-number 
                v-model:value="formState.unloadHeight" 
                :placeholder="t('location.inputUnloadHeight')"
                :min="0"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('location.depth')" name="depth">
              <a-input-number 
                v-model:value="formState.depth" 
                :placeholder="t('location.inputDepth')"
                :min="0"
                style="width: 100%"
              />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('location.isLocked')" name="lock">
              <a-switch v-model:checked="formState.lock" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('location.isEnabled')" name="enabled">
              <a-switch v-model:checked="formState.enabled" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-divider>{{ t('location.materialInfo') }}</a-divider>

        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('location.materialCode')" name="materialCode">
              <a-input v-model:value="formState.materialCode" :placeholder="t('location.inputMaterialCode')" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('location.palletId')" name="palletID">
              <a-input v-model:value="formState.palletID" :placeholder="t('location.inputPalletID')" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item :label="t('location.weight')" name="weight">
              <a-input v-model:value="formState.weight" :placeholder="t('location.inputWeight')" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('location.quantity')" name="quanitity">
              <a-input v-model:value="formState.quanitity" :placeholder="t('location.inputQuantity')" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('location.entryDate')" name="entryDate">
              <a-input v-model:value="formState.entryDate" :placeholder="t('location.inputEntryDate')" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-form-item>
          <a-space>
            <a-button type="primary" html-type="submit" :loading="isSubmitting">
              {{ isEdit ? t('location.update') : t('location.create') }}
            </a-button>
            <a-button @click="handleCancel">{{ t('common.cancel') }}</a-button>
          </a-space>
        </a-form-item>
      </a-form>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import locationService from '@/services/location'
import { message } from 'ant-design-vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const isSubmitting = ref(false)
const isEdit = ref(false)

const formState = reactive({
  name: '',
  nodeRemark: '',
  group: '',
  wattingNode: '',
  liftingHeight: 0,
  unloadHeight: 0,
  depth: 0,
  lock: false,
  enabled: true,
  materialCode: '',
  palletID: '0',
  weight: '0',
  quanitity: '0',
  entryDate: '',
})

const rules = computed(() => ({
  name: [{ required: true, message: t('location.nameRequired') }],
  nodeRemark: [{ required: true, message: t('location.nodeRemarkRequired') }],
  group: [{ required: true, message: t('location.groupRequired') }],
}))

onMounted(() => {
  const locationId = route.params.id as string
  if (locationId) {
    isEdit.value = true
    fetchLocationDetail(parseInt(locationId))
  }
})

const fetchLocationDetail = async (id: number) => {
  try {
    const response = await locationService.getLocationById(id)
    if (response.success && response.data) {
      const data = response.data
      formState.name = data.name || ''
      formState.nodeRemark = data.nodeRemark || ''
      formState.group = data.group || ''
      formState.wattingNode = data.wattingNode || ''
      formState.liftingHeight = data.liftingHeight || 0
      formState.unloadHeight = data.unloadHeight || 0
      formState.depth = data.depth || 0
      formState.lock = data.lock || false
      formState.enabled = data.enabled !== undefined ? data.enabled : true
      formState.materialCode = data.materialCode || ''
      formState.palletID = data.palletID || '0'
      formState.weight = data.weight || '0'
      formState.quanitity = data.quanitity || '0'
      formState.entryDate = data.entryDate || ''
    } else {
      message.error(response.message || t('common.fail'))
      router.back()
    }
  } catch (error: any) {
    message.error(error.message || t('common.fail'))
    router.back()
  }
}

const handleSubmit = async () => {
  isSubmitting.value = true
  try {
    let response
    if (isEdit.value) {
      const locationId = route.params.id as string
      response = await locationService.updateLocation(parseInt(locationId), formState)
    } else {
      response = await locationService.createLocation(formState)
    }

    if (response.success) {
      message.success(isEdit.value ? t('common.updateSuccess') : t('common.createSuccess'))
      router.push('/locations')
    } else {
      message.error(response.message || (isEdit.value ? t('common.updateFail') : t('common.createFail')))
    }
  } catch (error: any) {
    message.error(error.message || (isEdit.value ? t('common.updateFail') : t('common.createFail')))
  } finally {
    isSubmitting.value = false
  }
}

const handleCancel = () => {
  router.back()
}
</script>

<style scoped>
.location-create-edit-container {
  width: 100%;
  max-width: 1000px;
  margin: 0 auto;
}
</style>
