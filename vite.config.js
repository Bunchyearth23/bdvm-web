import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig({
  plugins: [svelte()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: false,
    proxy: Object.fromEntries([
      '/api', '/updates', '/bdvm', '/bdvm-ui', '/legacy-dispatch', '/res',
      '/route', '/junction', '/junctionState', '/trainset',
      '/track', '/player', '/infrastructure', '/car'
    ].map(path => [path, {
      // RemoteDispatchLive currently binds the local game relay to IPv6 ::1.
      // `localhost` lets Node select that listener; 127.0.0.1 would be refused.
      target: process.env.BDVM_GAME_ORIGIN || 'http://localhost:7245',
      // RemoteDispatch requires a Basic identity even with a blank local password.
      // Override both values when the host uses a non-default account/password.
      auth: `${process.env.BDVM_GAME_USER || 'bunchy'}:${process.env.BDVM_GAME_PASSWORD || ''}`,
      changeOrigin: true,
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
