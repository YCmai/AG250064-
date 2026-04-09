import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface Location {
  id: number
  name: string
  nodeRemark: string
  group: string
  materialCode: string
  palletID: string
  isEmpty: boolean
  lock: boolean
  enabled: boolean
}

export const useLocationStore = defineStore('location', () => {
  const locations = ref<Location[]>([])
  const total = ref(0)
  const page = ref(1)
  const pageSize = ref(20)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const setLocations = (newLocations: Location[], newTotal: number) => {
    locations.value = newLocations
    total.value = newTotal
  }

  const setPage = (newPage: number) => {
    page.value = newPage
  }

  const setPageSize = (newPageSize: number) => {
    pageSize.value = newPageSize
  }

  const setLoading = (loading: boolean) => {
    isLoading.value = loading
  }

  const setError = (err: string | null) => {
    error.value = err
  }

  const addLocation = (location: Location) => {
    locations.value.push(location)
  }

  const updateLocation = (location: Location) => {
    const index = locations.value.findIndex((l) => l.id === location.id)
    if (index !== -1) {
      locations.value[index] = location
    }
  }

  const removeLocation = (id: number) => {
    locations.value = locations.value.filter((l) => l.id !== id)
  }

  return {
    locations,
    total,
    page,
    pageSize,
    isLoading,
    error,
    setLocations,
    setPage,
    setPageSize,
    setLoading,
    setError,
    addLocation,
    updateLocation,
    removeLocation,
  }
})
