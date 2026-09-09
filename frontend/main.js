import { mount } from 'svelte';
import App from './App.svelte';

const target = document.getElementById('bdvm-app');
if (!target) throw new Error('BDVM Web mount point is missing.');
const app = mount(App, { target });
(/** @type {any} */ (window)).BdvmWebShell = Object.freeze({
  /** @param {Parameters<typeof app.render>[0]} snapshot */
  render: snapshot => app.render(snapshot),
  load: () => app.load()
});
