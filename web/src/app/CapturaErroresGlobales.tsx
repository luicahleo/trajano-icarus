import { useEffect } from 'react';
import { instalarCapturaGlobal, type ReporteroDiagnostico } from '../lib/diagnosticos';

export function CapturaErroresGlobales({
  reportero,
}: {
  reportero?: ReporteroDiagnostico;
} = {}) {
  useEffect(() => instalarCapturaGlobal(reportero), [reportero]);

  return null;
}
