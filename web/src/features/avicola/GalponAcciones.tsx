import { Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import type { Galpon } from '../../lib/tipos';
import { actualizarGalpon, ajustarInventarioGalpon, desactivarGalpon } from './api';
import { useFuncionalidad } from '../auth/useFuncionalidad';
export function GalponAcciones({ galpon }: { galpon: Galpon }) {
  const puede = useFuncionalidad('Galpones'); const qc = useQueryClient();
  const [dialogo, setDialogo] = useState<'editar'|'inventario'|'desactivar'|null>(null);
  const [numero,setNumero]=useState(galpon.numero),[descripcion,setDescripcion]=useState(galpon.descripcion??''),[capacidad,setCapacidad]=useState(String(galpon.capacidadMaxima)),[gallinas,setGallinas]=useState(String(galpon.gallinasActuales));
  const cerrar=()=>setDialogo(null); const refrescar=()=>{void qc.invalidateQueries({queryKey:['avicola','galpones']});cerrar();};
  const editar=useMutation({mutationFn:()=>actualizarGalpon(galpon.id,{numero,descripcion:descripcion||null,capacidadMaxima:Number(capacidad)}),onSuccess:refrescar});
  const inventario=useMutation({mutationFn:()=>ajustarInventarioGalpon(galpon.id,Number(gallinas)),onSuccess:refrescar});
  const desactivar=useMutation({mutationFn:()=>desactivarGalpon(galpon.id),onSuccess:refrescar});
  if(!puede)return null;
  return <><Button onClick={()=>setDialogo('editar')}>Editar</Button><Button onClick={()=>setDialogo('inventario')}>Inventario</Button><Button onClick={()=>setDialogo('desactivar')}>Desactivar</Button>
  <Dialog open={dialogo==='editar'} onClose={cerrar}><DialogTitle>Editar galpón</DialogTitle><DialogContent><TextField label="Número" value={numero} onChange={e=>setNumero(e.target.value)}/><TextField label="Descripción" value={descripcion} onChange={e=>setDescripcion(e.target.value)}/><TextField label="Capacidad máxima" value={capacidad} onChange={e=>setCapacidad(e.target.value)}/></DialogContent><DialogActions><Button onClick={cerrar}>Cancelar</Button><Button onClick={()=>editar.mutate()}>Guardar</Button></DialogActions></Dialog>
  <Dialog open={dialogo==='inventario'} onClose={cerrar}><DialogTitle>Ajustar inventario</DialogTitle><DialogContent><TextField label="Gallinas actuales" value={gallinas} onChange={e=>setGallinas(e.target.value)}/></DialogContent><DialogActions><Button onClick={cerrar}>Cancelar</Button><Button onClick={()=>inventario.mutate()}>Guardar</Button></DialogActions></Dialog>
  <Dialog open={dialogo==='desactivar'} onClose={cerrar}><DialogTitle>Desactivar galpón</DialogTitle><DialogContent>¿Confirmás la desactivación?</DialogContent><DialogActions><Button onClick={cerrar}>Cancelar</Button><Button onClick={()=>desactivar.mutate()}>Confirmar</Button></DialogActions></Dialog></>;
}
