import { Box, Typography } from '@mui/material';
import { AppProviders } from './app/providers';

export default function App() {
  return (
    <AppProviders>
      <Box sx={{ p: 4 }}>
        <Typography variant="h4">Icarus</Typography>
      </Box>
    </AppProviders>
  );
}
