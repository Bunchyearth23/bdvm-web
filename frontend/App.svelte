<script>
  import { onMount } from 'svelte';

  /** @typedef {{label?:string,path:string,ownerModuleId:string}} NavigationItem */
  /** @typedef {{connectionState?:string,displayName?:string,principalId?:string,modules?:Array<{id:string}>,navigation?:NavigationItem[]}} ShellSnapshot */
  /** @type {ShellSnapshot} */
  let snapshot = { connectionState: 'Loading', displayName: '', principalId: '', modules: [], navigation: [] };
  let activePath = location.pathname;
  let menuOpen = false;
  let error = '';
  let activityStatus = { state: '', message: '' };
  let loading = false;
  export function principal() { return snapshot.principalId || ''; }
  /** @type {Record<string,string>} */
  const descriptions = { '/dispatch': 'Locate rolling stock, inspect cargo and control routes on the live map.', '/management': 'Manage companies, wallets, rolling stock and industrial deliveries.' };

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
    if (loading) return;
    loading = true;
    error = '';
    activityStatus = { state: '', message: '' };
    snapshot = { ...snapshot, connectionState: 'Loading' };
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 15000);
    try {
      const response = await fetch('/api/web/shell', { signal: controller.signal, cache: 'no-store', credentials: 'same-origin', headers: { Accept: 'application/json' } });
      if (!response.ok) throw new Error(response.status === 401 ? 'Sign in to the game host, then reconnect.' : response.status === 403 ? 'This account does not have access.' : 'The game host is unavailable. Load a world, then reconnect.');
      render(await response.json());
      window.dispatchEvent(new CustomEvent('bdvm:connected'));
    } catch (reason) {
      render({ connectionState: 'Offline', modules: [], navigation: [] });
      window.dispatchEvent(new CustomEvent('bdvm:disconnected'));
      error = `Web interface unavailable: ${reason instanceof Error ? reason.message : 'unknown error'}`;
    } finally { clearTimeout(timer); loading = false; }
  }

  /** @param {NavigationItem} item */
  function navigate(item) {
    const target = new URL(item.path, location.origin);for (const key of ['preview','scenario']) { const value = new URLSearchParams(location.search).get(key); if (value) target.searchParams.set(key, value); }history.pushState({}, '', target);
    activePath = item.path;
    menuOpen = false;
    activityStatus = { state: '', message: '' };
    window.dispatchEvent(new CustomEvent('bdvm:navigate', { detail: { path: item.path, moduleId: item.ownerModuleId } }));
  }

  function onPopState() { activePath = location.pathname; }
  /** @param {Event} event */
  function onActivityStatus(event) {
    const detail = /** @type {CustomEvent<{state?:string,message?:string}>} */ (event).detail || {};
    activityStatus = { state: String(detail.state || ''), message: String(detail.message || '') };
  }
  onMount(() => {
    window.addEventListener('popstate', onPopState);
    window.addEventListener('bdvm:status', onActivityStatus);
    load();
    return () => {
      window.removeEventListener('popstate', onPopState);
      window.removeEventListener('bdvm:status', onActivityStatus);
    };
  });
</script>

<svelte:head>
  <meta name="theme-color" content="#151515" />
</svelte:head>

<div class="app-shell">
  <header class="topbar">
    <button class="menu-button" type="button" aria-label="Toggle navigation" aria-expanded={menuOpen} onclick={() => menuOpen = !menuOpen}>☰</button>
    <div class="topbar-title">
      <a class="brand" href="/" onclick={(event) => { event.preventDefault(); navigate({ path: '/', ownerModuleId: 'BDVM.Web' }); }}>
        <span><strong>BDVM Operations</strong><small>Live railway control</small></span>
      </a>
      <span class="connection" data-state={snapshot.connectionState || 'Offline'}><i></i>{snapshot.connectionState || 'Offline'}</span>
      {#if activityStatus.message && (snapshot.modules || []).length && snapshot.connectionState !== 'Loading'}
        <span class="activity-status" data-state={activityStatus.state} role="status" aria-live="polite" title={activityStatus.message}>{activityStatus.message}</span>
      {/if}
    </div>
    <div class="session">
      <button class="reconnect" type="button" disabled={loading} onclick={load}>{loading ? 'Connecting…' : 'Reconnect'}</button>
      <span class="session-name" title={snapshot.principalId ? 'Authenticated account' : 'No authenticated session'}>{snapshot.displayName || 'Signed out'}</span>
    </div>
  </header>

  <div class="workspace">
    <aside class:open={menuOpen}>
      <div class="nav-heading">Operations</div>
      <nav aria-label="Modules">
        {#each snapshot.navigation || [] as item (item.path)}
          <button type="button" class:active={activePath === item.path} aria-current={activePath === item.path ? 'page' : undefined} data-module={item.ownerModuleId} onclick={() => navigate(item)}>
            <span class="nav-indicator"></span><span>{item.label}</span>
          </button>
        {/each}
      </nav>
    </aside>
    {#if menuOpen}<button class="nav-backdrop" aria-label="Close navigation" onclick={() => menuOpen = false}></button>{/if}

    <main id="module-space" class:management-page={activePath === '/management'}>
      {#if error}
        <div class="notice error" role="alert">{error}</div>
      {/if}
      {#if activePath === '/' && (snapshot.modules || []).length}
        <section class="overview">
          <p class="eyebrow">Your railway workspace</p>
          <h1>Operations overview</h1>
          <p>Choose a workspace. Changes are checked by the game host.</p>
          <div class="overview-links">
            {#each snapshot.navigation || [] as item (item.path)}
              <button onclick={() => navigate(item)}><strong>{item.label}</strong><span>{descriptions[item.path] || 'Open this workspace.'}</span><small>Open workspace →</small></button>
            {/each}
          </div>
        </section>
      {:else if !(snapshot.modules || []).length}
        <section class="empty-state">
          <h1>BDVM Control Center</h1>
          <p>Connect to the authoritative host to access Dispatch and Management.</p>
          <p>Open Derail Valley and load your career, then use Reconnect.</p>
        </section>
      {/if}
      <div id="module-content" hidden={activePath === '/' || !(snapshot.modules || []).length}></div>
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
  .topbar-title { display:flex; align-items:center; gap:1rem; min-width:0; }
  .brand { display:flex; align-items:center; color:var(--cream); text-decoration:none; letter-spacing:.04em; }
  .brand > span:last-child { display:grid; line-height:1.1; }
  .brand strong { font-size:1rem; }
  .brand small { margin-top:.22rem; color:var(--muted); font-size:.68rem; font-weight:600; letter-spacing:.08em; text-transform:uppercase; }
  .menu-button { display:none; border:1px solid var(--line); border-radius:.2rem; background:var(--coal-800); color:var(--amber-300); width:2.35rem; height:2.35rem; }
  .session { margin-left:auto; display:flex; align-items:center; gap:1rem; }
  .session-name { color:var(--muted); font-size:.85rem; }
  .reconnect { padding:.45rem .65rem; border:1px solid var(--line); background:var(--coal-800); color:var(--cream); cursor:pointer; }
  .reconnect:disabled { opacity:.5; cursor:wait; }
  .nav-backdrop { display:none; }
  .overview { padding:clamp(1rem,4vw,4rem); }
  .overview h1 { font-size:clamp(1.8rem,4vw,3rem); margin:.5rem 0; }
  .overview > p { color:var(--muted); line-height:1.6; }
  .eyebrow { text-transform:uppercase; letter-spacing:.15em; font-size:.75rem; }
  .overview-links { display:grid; grid-template-columns:repeat(auto-fit,minmax(min(100%,20rem),1fr)); gap:1rem; margin-top:2rem; }
  .overview-links button { display:grid; gap:1rem; padding:1.5rem; text-align:left; border:1px solid var(--line); background:var(--coal-850); color:var(--cream); cursor:pointer; }
  .overview-links button:hover { border-color:var(--amber-500); }
  .overview-links strong { font-size:1.4rem; }
  .overview-links span { color:var(--muted); line-height:1.5; }
  .overview-links small { color:var(--amber-300); }
  .connection { display:flex; align-items:center; gap:.45rem; border:1px solid var(--line); border-radius:.2rem; padding:.4rem .65rem; color:var(--muted); font-size:.72rem; font-weight:700; text-transform:uppercase; letter-spacing:.08em; }
  .connection i { width:.42rem; height:.42rem; background:currentColor; }
  .connection[data-state="Online"] { color:var(--success); }
  .connection[data-state="Offline"] { color:var(--danger); }
  .connection[data-state="Loading"] { color:var(--amber-400); }
  .activity-status { min-width:0; max-width:min(42vw,44rem); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; border:1px solid var(--line); border-radius:.2rem; padding:.4rem .65rem; color:var(--muted); font-size:.72rem; font-weight:700; letter-spacing:.02em; }
  .activity-status[data-state="Succeeded"] { color:var(--success); }
  .activity-status[data-state="Loading"], .activity-status[data-state="Pending"] { color:var(--amber-400); }
  .activity-status[data-state="Refused"], .activity-status[data-state="Conflict"], .activity-status[data-state="Timeout"] { color:var(--danger); }
  .workspace { display:grid; grid-template-columns:13rem minmax(0,1fr); min-height:calc(100vh - 3.75rem); }
  aside { display:flex; flex-direction:column; padding:1.15rem .75rem .75rem; border-right:1px solid var(--line); background:var(--coal-900); }
  .nav-heading { margin:0 .75rem .7rem; color:#817565; font-size:.68rem; font-weight:800; letter-spacing:.16em; text-transform:uppercase; }
  nav { display:grid; gap:.3rem; }
  nav button { display:flex; align-items:center; gap:.65rem; width:100%; padding:.68rem .7rem; border:1px solid transparent; border-radius:.2rem; background:transparent; color:var(--muted); text-align:left; cursor:pointer; }
  nav button:hover { background:var(--coal-800); color:var(--cream); }
  nav button.active { border-color:#6b5228; background:#2b2418; color:var(--amber-300); }
  .nav-indicator { width:.18rem; height:1.15rem; background:transparent; }
  nav button.active .nav-indicator { background:var(--amber-400); }
  main { width:100%; max-width:112rem; min-width:0; margin:auto; padding:clamp(.75rem,1.5vw,1.5rem); }
  main.management-page { max-width:none; min-height:calc(100vh - 3.75rem); display:flex; flex-direction:column; }
  :global(main.management-page > .bdvm-management) { flex:1; min-height:100%; }
  .notice { margin-bottom:1rem; padding:.75rem .9rem; border:1px solid var(--line); border-radius:.2rem; background:var(--coal-850); }
  .notice.error { border-color:#743b35; color:#ffaaa4; }
  .empty-state { max-width:42rem; margin:clamp(1rem,5vw,4rem) 0; padding:1.25rem; border-left:3px solid var(--amber-500); background:var(--coal-850); }
  .empty-state h1 { margin:0 0 .5rem; font-size:1.5rem; letter-spacing:-.02em; }
  .empty-state p { margin:0; color:var(--muted); line-height:1.55; }
  :global(.bdvm-management__panel), :global(.bdvm-dispatch) { border-color:var(--line)!important; background:var(--coal-850)!important; }
  :global(.bdvm-management) { align-content:start; }
  :global(.bdvm-management__tabs) { display:flex; flex-wrap:wrap; gap:.35rem; overflow:visible; padding-bottom:.5rem; }
  :global(.bdvm-management__tabs button) { min-width:0; min-height:3.25rem; height:auto; padding:.5rem .35rem; white-space:normal; text-align:center; display:flex; align-items:center; justify-content:center; }
  :global(.bdvm-management button), :global(.bdvm-dispatch button) { border:1px solid var(--line); border-radius:.2rem; background:var(--coal-800); color:var(--cream); }
  :global(.bdvm-management button:hover), :global(.bdvm-dispatch button:hover) { border-color:var(--amber-500); color:var(--amber-300); }
  :global(.bdvm-management__tabs button[aria-selected="true"]) { background:var(--amber-500); color:var(--coal-950); font-weight:800; }
  :global(.bdvm-management input), :global(.bdvm-management select) { border-color:var(--line)!important; background:var(--coal-950)!important; color:var(--cream)!important; }
  :global(.bdvm-dispatch-host) { height:calc(100vh - 6.75rem); min-height:34rem; }
  :global(.bdvm-dispatch-frame) { width:100%; height:100%; border:1px solid var(--line); background:var(--coal-950); }
  @media (max-width:760px) {
    .topbar { height:3.75rem; padding:0 .75rem; }
    .menu-button { display:block; }
    .brand small,.session-name { display:none; }
    .topbar-title { gap:.65rem; }
    .activity-status { display:none; }
    .brand strong { font-size:.85rem; }
    .topbar { gap:.5rem; }
    .connection { padding:.35rem; font-size:.6rem; letter-spacing:0; }
    .reconnect { font-size:.72rem; }
    .workspace { grid-template-columns:1fr; min-height:calc(100vh - 3.75rem); }
    aside { position:fixed; inset:3.75rem auto 0 0; z-index:15; width:min(17rem,86vw); visibility:hidden; transform:translateX(-105%); transition:transform .15s ease; }
    aside.open { visibility:visible; transform:translateX(0); }
    .nav-backdrop { display:block; position:fixed; inset:3.75rem 0 0; border:0; background:#0008; z-index:14; }
    main { padding:.65rem; }
  }
  @media (prefers-reduced-motion:reduce) { aside,nav button { transition:none; } }
</style>
