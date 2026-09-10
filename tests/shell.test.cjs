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
const bootstrap = fs.readFileSync(path.join(root, 'Assets', 'bootstrap.js'), 'utf8');

test('Svelte shell compiles as self-contained static assets', () => {
  assert.match(entry, /mount\(App/);
  assert.match(html, /id="bdvm-app"/);
  assert.ok(bundle.length > 10000);
  assert.doesNotMatch(html, /node_modules|frontend\//);
  assert.match(html, /\/bdvm-ui\/bootstrap\.js/);
  assert.match(html, /\/bdvm-ui\/modules\/bdvm\.management\/app\.js/);
  assert.match(bootstrap, /\/api\/modules\/bdvm\.management\/snapshot/);
  assert.match(bootstrap, /\/api\/modules\/bdvm\.management\/intent/);
  assert.match(component, /normalizeKeys/);
  assert.match(bootstrap, /normalizeKeys/);
});

test('runtime serializes shell and Management contracts as browser camelCase', () => {
  const runtime = fs.readFileSync(path.join(root, '..', 'BDVM.Full', 'Main.cs'), 'utf8');
  assert.match(runtime, /CamelCasePropertyNamesContractResolver/);
  assert.match(runtime, /SerializeObject\(shell, webJsonSettings\)/);
  assert.match(runtime, /SerializeObject\(snapshot, webJsonSettings\)/);
  assert.match(runtime, /SerializeObject\(payload, webJsonSettings\)/);
});

test('responsive amber and anthracite design system is present', () => {
  assert.match(component, /--amber-500:#e59a1a/);
  assert.match(component, /--coal-950:#101010/);
  assert.match(css, /@media\s*\((?:max-width:760px|width<=760px)\)/);
  assert.match(component, /prefers-reduced-motion/);
  assert.doesNotMatch(component, /linear-gradient|radial-gradient|box-shadow|backdrop-filter/);
});

test('module labels remain escaped by Svelte and module ownership is forwarded', () => {
  assert.match(component, /\{item\.label\}/);
  assert.match(component, /moduleId: item\.ownerModuleId/);
  assert.doesNotMatch(component, /\{@html|innerHTML|insertAdjacentHTML/);
});

test('Dispatch and Management remain distinct destinations', () => {
  assert.match(component, /ownerModuleId/);
  assert.match(bootstrap, /path==='\/dispatch'/);
  assert.match(bootstrap, /path!=='\/management'/);
});

test('browser-facing shell sources remain English-only', () => {
  const visibleSources = [component, html, bootstrap];
  const french = /[àâçéèêëîïôùûüÿœ]|\b(?:actualiser|ouvrir|fermer|indisponible|itinéraire|trafic|joueur|compagnie|matériel|ferroviaire|déconnexion|connecté|hors ligne|rafraîchir|aucun|erreur|chargement|serveur|gare|voie|portefeuille|gestion|annuler|acheter|vendre|conducteur|affectation)\b/i;
  for (const source of visibleSources) assert.doesNotMatch(source, french);
});
