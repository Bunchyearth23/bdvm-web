import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig({
  plugins: [svelte()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: false,
    proxy: Object.fromEntries([
      '/api', '/updates', '/bdvm', '/bdvm-ui', '/legacy-dispatch',
      '/route', '/junction', '/junctionState', '/trainset'
    ].map(path => [path, {
      target: process.env.BDVM_GAME_ORIGIN || 'http://127.0.0.1:7245',
      changeOrigin: false,
      secure: false
    }]))
  },
  build: {
    outDir: 'Assets',
    emptyOutDir: false,
    cssCodeSplit: false,
    lib: { entry: 'frontend/main.js', formats: ['iife'], name: 'BdvmWebApp' },
    rollupOptions: { output: { entryFileNames: 'shell.js', assetFileNames: 'shell.css' } }
  }
});
