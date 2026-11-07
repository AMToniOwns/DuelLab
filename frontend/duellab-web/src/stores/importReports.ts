import { reactive } from 'vue'
import { defineStore } from 'pinia'
import type { ImportReport } from '@/api/types'

/** The latest import report per version, shown once in the editor after an import. */
export const useImportReportStore = defineStore('importReports', () => {
  const reports = reactive<Record<string, ImportReport & { fileName: string }>>({})

  function set(versionId: string, fileName: string, report: ImportReport): void {
    reports[versionId] = { ...report, fileName }
  }

  function dismiss(versionId: string): void {
    delete reports[versionId]
  }

  return { reports, set, dismiss }
})
