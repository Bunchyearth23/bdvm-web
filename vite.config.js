import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig({
  plugins: [svelte()],
  build: {
    outDir: 'Assets',
    emptyOutDir: false,
    cssCodeSplit: false,
    lib: { entry: 'frontend/main.js', formats: ['iife'], name: 'BdvmWebApp' },
    rollupOptions: { output: { entryFileNames: 'shell.js', assetFileNames: 'shell.css' } }
  }
});
