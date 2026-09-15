import { render, screen } from '@testing-library/react';
import svgFavicon from '../../public/favicon.svg?raw';
import configuracionVite from '../../vite.config.ts?raw';
import { MarcaIcarus } from './MarcaIcarus';

describe('marca Icarus', () => {
  test('el favicon gradiente está limpio y tiene una sola silueta', () => {
    expect(svgFavicon).toContain('viewBox="0 0 800 800"');
    expect(svgFavicon.match(/<path\b/g)).toHaveLength(1);
    expect(svgFavicon).toContain('id="icarus-gradient"');
    expect(svgFavicon).not.toContain('b-y8l9ktuq7y');
    expect(svgFavicon.toLowerCase()).not.toContain('darkreader');
  });

  test('el manifest declara los iconos PWA generados', () => {
    expect(configuracionVite).toContain('pwa/pwa-192x192.png');
    expect(configuracionVite).toContain('pwa/pwa-512x512.png');
    expect(configuracionVite).toContain('pwa/pwa-maskable-192x192.png');
    expect(configuracionVite).toContain('pwa/pwa-maskable-512x512.png');
  });

  test('la marca inline es decorativa, monocroma y de una sola silueta', () => {
    render(<MarcaIcarus />);
    const svg = screen.getByTestId('marca-icarus');
    expect(svg).toHaveAttribute('viewBox', '0 0 800 800');
    expect(svg).toHaveAttribute('aria-hidden', 'true');
    expect(svg.querySelectorAll('path')).toHaveLength(1);
    expect(svg.querySelector('g')).toHaveAttribute('fill', 'currentColor');
    expect(svg.outerHTML).not.toContain('b-y8l9ktuq7y');
    expect(svg.outerHTML.toLowerCase()).not.toContain('darkreader');
  });
});
