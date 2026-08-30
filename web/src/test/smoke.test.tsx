import 'fake-indexeddb/auto';
import { render, screen } from '@testing-library/react';
import App from '../App';

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })));
});

test('la app monta y muestra el inicio de sesión para una sesión anónima', async () => {
  render(<App />);
  expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument();
});
