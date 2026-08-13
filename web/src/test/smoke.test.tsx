import { render, screen } from '@testing-library/react';
import App from '../App';

test('la app monta sin romper', () => {
  render(<App />);
  expect(screen.getByRole('heading', { name: 'Icarus' })).toBeInTheDocument();
});
