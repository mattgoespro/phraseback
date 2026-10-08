import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  root: 'src/renderer',
  base: './',
  plugins: [react(), tailwindcss()],
  build: { outDir: '../../dist/renderer', emptyOutDir: true },
  test: { root: '../..', environment: 'node', include: ['packages/ui/src/**/*.test.ts'] }
});
