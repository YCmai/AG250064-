import api from './api'

export interface LogFile {
  filename: string
  size: number
  lastModified: string
}

const unwrapPayload = <T = any>(response: any): T => {
  if (response && response.data !== undefined) {
    return response.data as T
  }
  return response as T
}

export const logService = {
  getLogFiles: async (): Promise<LogFile[]> => {
    const response = await api.get('/log/files')
    return unwrapPayload<LogFile[]>(response)
  },

  getLogContent: async (filename: string, limitLines?: number): Promise<string[]> => {
    const response = await api.get('/log/content', {
      params: { filename, limitLines },
    })
    return unwrapPayload<string[]>(response)
  },
}
