import { render, screen } from '@testing-library/react';
import App from '../App';

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })));
});

test('la app monta sin romper', () => {
  render(<App />);
  expect(screen.getByRole('heading', { name: 'Icarus' })).toBeInTheDocument();
});
