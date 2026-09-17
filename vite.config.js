import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const asset = path => fileURLToPath(new URL(path, import.meta.url));
const gameOrigin = process.env.BDVM_GAME_ORIGIN || 'http://localhost:18080';
const routes = ['/api', '/legacy-dispatch', '/res', '/track', '/trainset', '/player', '/infrastructure', '/car', '/junction', '/route', '/job', '/updates', '/bdvm'];
const localAssets = {
  '/bdvm-ui/bootstrap.js': asset('./Assets/bootstrap.js'),
  '/bdvm-ui/modules/bdvm.management/app.js': asset('../BDVM.Management/Assets/app.js'),
  '/bdvm-ui/modules/bdvm.management/app.css': asset('../BDVM.Management/Assets/app.css'),
  '/bdvm-ui/modules/bdvm.dispatch/app.js': asset('../BDVM.Dispatch/Assets/app.js'),
  '/bdvm-ui/modules/bdvm.dispatch/app.css': asset('../BDVM.Dispatch/Assets/app.css')
};
const developmentEntry = {
  name: 'bdvm-development-entry',
  configureServer(server) {
    server.middlewares.use(async (request, response, next) => {
      const path = new URL(request.url, 'http://localhost').pathname;
      if (!['GET','HEAD','OPTIONS'].includes(request.method)) {
        const origin = request.headers.origin;
        if (request.headers['sec-fetch-site'] === 'cross-site' || (origin && origin !== `http://${request.headers.host}`)) {
          response.writeHead(403, {'Content-Type':'application/json'});
          response.end(JSON.stringify({error:'Cross-origin development request refused.'}));
          return;
        }
      }
      if (localAssets[path]) {
        response.setHeader('Content-Type', path.endsWith('.css') ? 'text/css' : 'text/javascript');
        response.setHeader('Cache-Control', 'no-store');
        response.end(readFileSync(localAssets[path]));
        return;
      }
      if (['/', '/management', '/dispatch'].includes(path)) {
        const html = readFileSync(asset('./dev/index.html'), 'utf8');
        response.setHeader('Content-Type', 'text/html');
        response.end(await server.transformIndexHtml(request.url, html));
        return;
      }
      next();
    });
  }
};

export default defineConfig({
  plugins: [svelte(), developmentEntry],
  server: {
    port: 5173,
    strictPort: true,
    proxy: Object.fromEntries(routes.map(route => [route, {
      target: gameOrigin, changeOrigin: true, ws: route === '/updates',
      configure(proxy) {
        proxy.on('proxyReq', (proxyRequest, request) => {
          if (request.headers.origin === `http://${request.headers.host}`) proxyRequest.setHeader('Origin', new URL(gameOrigin).origin);
        });
      },
      ...(process.env.BDVM_GAME_USER ? { auth: `${process.env.BDVM_GAME_USER}:${process.env.BDVM_GAME_PASSWORD || ''}` } : {})
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
