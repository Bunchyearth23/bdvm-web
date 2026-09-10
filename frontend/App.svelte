<script>
  import { onMount } from 'svelte';

  /** @typedef {{label?:string,path:string,ownerModuleId:string}} NavigationItem */
  /** @typedef {{connectionState?:string,displayName?:string,principalId?:string,modules?:Array<{id:string}>,navigation?:NavigationItem[]}} ShellSnapshot */
  /** @type {ShellSnapshot} */
  let snapshot = { connectionState: 'Loading', displayName: '', principalId: '', modules: [], navigation: [] };
  let activePath = location.pathname;
  let menuOpen = false;
  let error = '';

  /** Accept the original PascalCase wire shape while the host migrates to camelCase.
   * @param {unknown} value
   * @returns {any}
   */
  function normalizeKeys(value) {
    if (Array.isArray(value)) return value.map(normalizeKeys);
    if (!value || typeof value !== 'object') return value;
    return Object.fromEntries(Object.entries(value).map(([key, item]) => [key.charAt(0).toLowerCase() + key.slice(1), normalizeKeys(item)]));
  }

  /** @param {ShellSnapshot} value */
  export function render(value) {
    snapshot = normalizeKeys(value) || { connectionState: 'Offline', modules: [], navigation: [] };
  }

  export async function load() {
    error = '';
    snapshot = { ...snapshot, connectionState: 'Loading' };
    try {
      const response = await fetch('/api/web/shell', { cache: 'no-store', credentials: 'same-origin', headers: { Accept: 'application/json' } });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      render(await response.json());
    } catch (reason) {
      render({ connectionState: 'Offline', modules: [], navigation: [] });
      error = `Web interface unavailable: ${reason instanceof Error ? reason.message : 'unknown error'}`;
    }
  }

  /** @param {NavigationItem} item */
  function navigate(item) {
    history.pushState({}, '', item.path);
    activePath = item.path;
    menuOpen = false;
    window.dispatchEvent(new CustomEvent('bdvm:navigate', { detail: { path: item.path, moduleId: item.ownerModuleId } }));
  }

  function onPopState() { activePath = location.pathname; }
  onMount(() => { window.addEventListener('popstate', onPopState); load(); return () => window.removeEventListener('popstate', onPopState); });
</script>

<svelte:head>
  <meta name="theme-color" content="#151515" />
</svelte:head>

<div class="app-shell">
  <header class="topbar">
    <button class="menu-button" type="button" aria-label="Toggle navigation" aria-expanded={menuOpen} onclick={() => menuOpen = !menuOpen}>☰</button>
    <a class="brand" href="/" onclick={(event) => { event.preventDefault(); navigate({ path: '/', ownerModuleId: 'BDVM.Web' }); }}>
      <span class="brand-mark">B</span>
      <span><strong>BDVM</strong><small>Rail operations suite</small></span>
    </a>
    <div class="session">
      <span class="session-name" title={snapshot.principalId ? 'Authenticated account' : 'No authenticated session'}>{snapshot.displayName || 'Signed out'}</span>
      <span class="connection" data-state={snapshot.connectionState || 'Offline'}><i></i>{snapshot.connectionState || 'Offline'}</span>
    </div>
  </header>

  <div class="workspace">
    <aside class:open={menuOpen}>
      <div class="nav-heading">Workspace</div>
      <nav aria-label="Modules">
        {#each snapshot.navigation || [] as item (item.path)}
          <button type="button" class:active={activePath === item.path} data-module={item.ownerModuleId} onclick={() => navigate(item)}>
            <span class="nav-indicator"></span><span>{item.label}</span>
          </button>
        {/each}
      </nav>
      <div class="sidebar-footer"><span>Host authoritative</span><small>Snapshots and validated intents</small></div>
    </aside>

    <main id="module-space">
      {#if error}
        <div class="notice error" role="alert">{error}</div>
      {/if}
      {#if !(snapshot.modules || []).length}
        <section class="empty-state">
          <h1>BDVM Control Center</h1>
          <p>Connect to the authoritative host to access Dispatch and Management.</p>
        </section>
      {/if}
    </main>
  </div>
</div>

<style>
  :global(:root) { color-scheme:dark; --coal-950:#101010; --coal-900:#151515; --coal-850:#1b1b1b; --coal-800:#242424; --coal-700:#303030; --amber-500:#e59a1a; --amber-400:#f2aa2b; --amber-300:#ffc15a; --cream:#f2eee7; --muted:#aaa39a; --line:#3b3731; --success:#67ad82; --danger:#e36b63; font-family:Inter,"Segoe UI",system-ui,sans-serif; }
  :global(*) { box-sizing:border-box; }
  :global(body) { margin:0; min-width:20rem; min-height:100vh; background:var(--coal-950); color:var(--cream); }
  :global(button), :global(input), :global(select) { font:inherit; }
  :global(button), :global(a) { -webkit-tap-highlight-color:transparent; }
  :global(:focus-visible) { outline:2px solid var(--amber-400); outline-offset:3px; }
  .app-shell { min-height:100vh; background:var(--coal-950); }
  .topbar { height:3.75rem; display:flex; align-items:center; gap:1rem; padding:0 1.25rem; position:sticky; top:0; z-index:20; border-bottom:1px solid var(--line); background:var(--coal-900); }
  .brand { display:flex; align-items:center; gap:.75rem; color:var(--cream); text-decoration:none; letter-spacing:.04em; }
  .brand-mark { display:grid; place-items:center; width:2.15rem; aspect-ratio:1; border-radius:.2rem; background:var(--amber-500); color:var(--coal-950); font-size:1.15rem; font-weight:900; }
  .brand > span:last-child { display:grid; line-height:1.1; }
  .brand strong { font-size:1rem; }
  .brand small { margin-top:.22rem; color:var(--muted); font-size:.68rem; font-weight:600; letter-spacing:.08em; text-transform:uppercase; }
  .menu-button { display:none; border:1px solid var(--line); border-radius:.2rem; background:var(--coal-800); color:var(--amber-300); width:2.35rem; height:2.35rem; }
  .session { margin-left:auto; display:flex; align-items:center; gap:1rem; }
  .session-name { color:var(--muted); font-size:.85rem; }
  .connection { display:flex; align-items:center; gap:.45rem; border:1px solid var(--line); border-radius:.2rem; padding:.4rem .65rem; color:var(--muted); font-size:.72rem; font-weight:700; text-transform:uppercase; letter-spacing:.08em; }
  .connection i { width:.42rem; height:.42rem; background:currentColor; }
  .connection[data-state="Online"] { color:var(--success); }
  .connection[data-state="Offline"] { color:var(--danger); }
  .connection[data-state="Loading"] { color:var(--amber-400); }
  .workspace { display:grid; grid-template-columns:14.5rem minmax(0,1fr); min-height:calc(100vh - 3.75rem); }
  aside { display:flex; flex-direction:column; padding:1.15rem .75rem .75rem; border-right:1px solid var(--line); background:var(--coal-900); }
  .nav-heading { margin:0 .75rem .7rem; color:#817565; font-size:.68rem; font-weight:800; letter-spacing:.16em; text-transform:uppercase; }
  nav { display:grid; gap:.3rem; }
  nav button { display:flex; align-items:center; gap:.65rem; width:100%; padding:.68rem .7rem; border:1px solid transparent; border-radius:.2rem; background:transparent; color:var(--muted); text-align:left; cursor:pointer; }
  nav button:hover { background:var(--coal-800); color:var(--cream); }
  nav button.active { border-color:#6b5228; background:#2b2418; color:var(--amber-300); }
  .nav-indicator { width:.18rem; height:1.15rem; background:transparent; }
  nav button.active .nav-indicator { background:var(--amber-400); }
  .sidebar-footer { display:grid; gap:.25rem; margin-top:auto; padding:1rem .75rem .25rem; border-top:1px solid var(--line); color:var(--amber-400); font-size:.72rem; font-weight:700; text-transform:uppercase; letter-spacing:.08em; }
  .sidebar-footer small { color:#817565; font-size:.62rem; font-weight:600; }
  main { width:100%; max-width:112rem; min-width:0; margin:auto; padding:clamp(1rem,2vw,2rem); }
  .notice { margin-bottom:1rem; padding:.75rem .9rem; border:1px solid var(--line); border-radius:.2rem; background:var(--coal-850); }
  .notice.error { border-color:#743b35; color:#ffaaa4; }
  .empty-state { max-width:42rem; margin:clamp(1rem,5vw,4rem) 0; padding:1.25rem; border-left:3px solid var(--amber-500); background:var(--coal-850); }
  .empty-state h1 { margin:0 0 .5rem; font-size:1.5rem; letter-spacing:-.02em; }
  .empty-state p { margin:0; color:var(--muted); line-height:1.55; }
  :global(.bdvm-management__panel), :global(.bdvm-dispatch) { border-color:var(--line)!important; background:var(--coal-850)!important; }
  :global(.bdvm-management button), :global(.bdvm-dispatch button) { border:1px solid var(--line); border-radius:.2rem; background:var(--coal-800); color:var(--cream); }
  :global(.bdvm-management button:hover), :global(.bdvm-dispatch button:hover) { border-color:var(--amber-500); color:var(--amber-300); }
  :global(.bdvm-management__tabs button[aria-selected="true"]) { background:var(--amber-500); color:var(--coal-950); font-weight:800; }
  :global(.bdvm-management input), :global(.bdvm-management select) { border-color:var(--line)!important; background:var(--coal-950)!important; color:var(--cream)!important; }
  @media (max-width:760px) {
    .topbar { height:3.75rem; padding:0 .75rem; }
    .menu-button { display:block; }
    .brand small,.session-name { display:none; }
    .workspace { grid-template-columns:1fr; min-height:calc(100vh - 3.75rem); }
    aside { position:fixed; inset:3.75rem auto 0 0; z-index:15; width:min(17rem,86vw); transform:translateX(-105%); transition:transform .15s ease; }
    aside.open { transform:translateX(0); }
    main { padding:1.2rem; }
  }
  @media (prefers-reduced-motion:reduce) { aside,nav button { transition:none; } }
</style>
