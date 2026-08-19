import {Alert} from '@mui/material'; import {useConexion} from './useConexion';
export function BannerSinConexion(){if(useConexion())return null;return <Alert severity="warning" sx={{borderRadius:0}}>Sin conexión: los datos pueden estar desactualizados y no se pueden guardar registros.</Alert>;}
