import { Outlet } from 'react-router-dom';
import { BotonDiagnostico } from './BotonDiagnostico';
import { CapturaFlujoNavegacion } from './CapturaFlujoNavegacion';

export function RaizAplicacion() {
  return (
    <>
      <CapturaFlujoNavegacion />
      <Outlet />
      <BotonDiagnostico />
    </>
  );
}
