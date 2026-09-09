const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.join(__dirname, '..');
const component = fs.readFileSync(path.join(root, 'frontend', 'App.svelte'), 'utf8');
const entry = fs.readFileSync(path.join(root, 'frontend', 'main.js'), 'utf8');
const html = fs.readFileSync(path.join(root, 'Assets', 'shell.html'), 'utf8');
const css = fs.readFileSync(path.join(root, 'Assets', 'shell.css'), 'utf8');
const bundle = fs.readFileSync(path.join(root, 'Assets', 'shell.js'), 'utf8');

test('Svelte shell compiles as self-contained static assets', () => {
  assert.match(entry, /mount\(App/);
  assert.match(html, /id="bdvm-app"/);
  assert.ok(bundle.length > 10000);
  assert.doesNotMatch(html, /node_modules|frontend\//);
});

test('responsive amber and anthracite design system is present', () => {
  assert.match(component, /--amber-500:#f59e0b/);
  assert.match(component, /--coal-950:#0d0c0b/);
  assert.match(css, /@media\s*\((?:max-width:760px|width<=760px)\)/);
  assert.match(component, /prefers-reduced-motion/);
});

test('module labels remain escaped by Svelte and module ownership is forwarded', () => {
  assert.match(component, /\{item\.label\}/);
  assert.match(component, /moduleId: item\.ownerModuleId/);
  assert.doesNotMatch(component, /\{@html|innerHTML|insertAdjacentHTML/);
});

test('Dispatch and Management remain distinct destinations', () => {
  assert.match(component, />Dispatch</);
  assert.match(component, />Management</);
  assert.match(component, /Tracks, signals, switches/);
  assert.match(component, /Companies, finance, fleet/);
});
