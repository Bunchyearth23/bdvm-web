<script>
  import { onMount } from 'svelte';

  /** @typedef {{label?:string,path:string,ownerModuleId:string}} NavigationItem */
  /** @typedef {{connectionState?:string,displayName?:string,principalId?:string,modules?:Array<{id:string}>,navigation?:NavigationItem[]}} ShellSnapshot */
  /** @type {ShellSnapshot} */
  let snapshot = { connectionState: 'Loading', displayName: '', principalId: '', modules: [], navigation: [] };
  let activePath = location.pathname;
  let menuOpen = false;
  let error = '';

  /** @param {ShellSnapshot} value */
  export function render(value) {
    snapshot = value || { connectionState: 'Offline', modules: [], navigation: [] };
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
  <meta name="theme-color" content="#171513" />
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
        <section class="hero">
          <span class="eyebrow">CONTROL CENTER</span>
          <h1>Your railway.<br><em>One clear view.</em></h1>
          <p>Connect to an authoritative BDVM host, then choose Dispatch for railway operations or Management for your company and fleet.</p>
          <div class="hero-grid">
            <article><span>01</span><h2>Dispatch</h2><p>Tracks, signals, switches, consists and live railway status.</p></article>
            <article><span>02</span><h2>Management</h2><p>Companies, finance, fleet, market, contracts and industry.</p></article>
          </div>
        </section>
      {/if}
    </main>
  </div>
</div>

<style>
  :global(:root) { color-scheme: dark; --coal-950:#0d0c0b; --coal-900:#171513; --coal-850:#1e1b18; --coal-800:#27231f; --coal-700:#3a342d; --amber-500:#f59e0b; --amber-400:#fbbf24; --amber-300:#fcd34d; --cream:#fff8e7; --muted:#b8aa96; --line:#453d33; --success:#65c48d; --danger:#ff746c; font-family:Inter,"Segoe UI",system-ui,sans-serif; }
  :global(*) { box-sizing:border-box; }
  :global(body) { margin:0; min-width:20rem; min-height:100vh; background:var(--coal-950); color:var(--cream); }
  :global(button), :global(input), :global(select) { font:inherit; }
  :global(button), :global(a) { -webkit-tap-highlight-color:transparent; }
  :global(:focus-visible) { outline:2px solid var(--amber-400); outline-offset:3px; }
  .app-shell { min-height:100vh; background:radial-gradient(circle at 78% 8%,rgba(245,158,11,.1),transparent 30rem),linear-gradient(145deg,#0d0c0b,#171513 55%,#12100e); }
  .topbar { height:4.75rem; display:flex; align-items:center; gap:1rem; padding:0 clamp(1rem,3vw,2.25rem); position:sticky; top:0; z-index:20; border-bottom:1px solid var(--line); background:rgba(23,21,19,.94); backdrop-filter:blur(16px); }
  .brand { display:flex; align-items:center; gap:.75rem; color:var(--cream); text-decoration:none; letter-spacing:.04em; }
  .brand-mark { display:grid; place-items:center; width:2.35rem; aspect-ratio:1; border-radius:.55rem; background:linear-gradient(145deg,var(--amber-300),var(--amber-500)); color:var(--coal-950); font-size:1.25rem; font-weight:900; box-shadow:0 0 1.5rem rgba(245,158,11,.18); }
  .brand > span:last-child { display:grid; line-height:1.1; }
  .brand strong { font-size:1rem; }
  .brand small { margin-top:.22rem; color:var(--muted); font-size:.68rem; font-weight:600; letter-spacing:.08em; text-transform:uppercase; }
  .menu-button { display:none; border:1px solid var(--line); border-radius:.45rem; background:var(--coal-800); color:var(--amber-300); width:2.5rem; height:2.5rem; }
  .session { margin-left:auto; display:flex; align-items:center; gap:1rem; }
  .session-name { color:var(--muted); font-size:.85rem; }
  .connection { display:flex; align-items:center; gap:.45rem; border:1px solid var(--line); border-radius:999px; padding:.42rem .7rem; color:var(--muted); font-size:.72rem; font-weight:700; text-transform:uppercase; letter-spacing:.08em; }
  .connection i { width:.45rem; height:.45rem; border-radius:50%; background:currentColor; box-shadow:0 0 .6rem currentColor; }
  .connection[data-state="Online"] { color:var(--success); }
  .connection[data-state="Offline"] { color:var(--danger); }
  .connection[data-state="Loading"] { color:var(--amber-400); }
  .workspace { display:grid; grid-template-columns:16.5rem minmax(0,1fr); min-height:calc(100vh - 4.75rem); }
  aside { display:flex; flex-direction:column; padding:1.5rem 1rem 1rem; border-right:1px solid var(--line); background:rgba(23,21,19,.8); }
  .nav-heading { margin:0 .75rem .7rem; color:#817565; font-size:.68rem; font-weight:800; letter-spacing:.16em; text-transform:uppercase; }
  nav { display:grid; gap:.3rem; }
  nav button { display:flex; align-items:center; gap:.7rem; width:100%; padding:.78rem .75rem; border:1px solid transparent; border-radius:.5rem; background:transparent; color:var(--muted); text-align:left; cursor:pointer; transition:background .16s,border-color .16s,color .16s; }
  nav button:hover { background:var(--coal-800); color:var(--cream); }
  nav button.active { border-color:#5f4b28; background:linear-gradient(90deg,rgba(245,158,11,.16),rgba(245,158,11,.04)); color:var(--amber-300); }
  .nav-indicator { width:.22rem; height:1.25rem; border-radius:1rem; background:transparent; }
  nav button.active .nav-indicator { background:var(--amber-400); box-shadow:0 0 .7rem rgba(251,191,36,.55); }
  .sidebar-footer { display:grid; gap:.25rem; margin-top:auto; padding:1rem .75rem .25rem; border-top:1px solid var(--line); color:var(--amber-400); font-size:.72rem; font-weight:700; text-transform:uppercase; letter-spacing:.08em; }
  .sidebar-footer small { color:#817565; font-size:.62rem; font-weight:600; }
  main { width:100%; max-width:112rem; min-width:0; margin:auto; padding:clamp(1.25rem,4vw,4rem); }
  .notice { margin-bottom:1rem; padding:.8rem 1rem; border:1px solid var(--line); border-radius:.5rem; background:var(--coal-850); }
  .notice.error { border-color:#743b35; color:#ffaaa4; }
  .hero { max-width:72rem; padding:clamp(1rem,3vw,2rem) 0; }
  .eyebrow { color:var(--amber-400); font-size:.72rem; font-weight:900; letter-spacing:.22em; }
  h1 { max-width:14ch; margin:.8rem 0 1.2rem; font-size:clamp(2.8rem,7vw,6.8rem); line-height:.91; letter-spacing:-.055em; }
  h1 em { color:var(--amber-400); font-style:normal; }
  .hero > p { max-width:42rem; color:var(--muted); font-size:clamp(1rem,2vw,1.2rem); line-height:1.65; }
  .hero-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:1rem; margin-top:clamp(2rem,5vw,4rem); }
  article { padding:clamp(1.2rem,3vw,2rem); border:1px solid var(--line); border-radius:.75rem; background:linear-gradient(145deg,var(--coal-850),rgba(39,35,31,.65)); box-shadow:0 1rem 3rem rgba(0,0,0,.16); }
  article > span { color:var(--amber-500); font:700 .7rem ui-monospace,monospace; }
  article h2 { margin:1.2rem 0 .5rem; font-size:1.4rem; }
  article p { margin:0; color:var(--muted); line-height:1.55; }
  :global(.bdvm-management__panel), :global(.bdvm-dispatch) { border-color:var(--line)!important; background:var(--coal-850)!important; }
  :global(.bdvm-management button), :global(.bdvm-dispatch button) { border:1px solid var(--line); border-radius:.4rem; background:var(--coal-800); color:var(--cream); }
  :global(.bdvm-management button:hover), :global(.bdvm-dispatch button:hover) { border-color:var(--amber-500); color:var(--amber-300); }
  :global(.bdvm-management__tabs button[aria-selected="true"]) { background:var(--amber-500); color:var(--coal-950); font-weight:800; }
  :global(.bdvm-management input), :global(.bdvm-management select) { border-color:var(--line)!important; background:var(--coal-950)!important; color:var(--cream)!important; }
  @media (max-width:760px) {
    .topbar { height:4.25rem; padding:0 .8rem; }
    .menu-button { display:block; }
    .brand small,.session-name { display:none; }
    .workspace { grid-template-columns:1fr; min-height:calc(100vh - 4.25rem); }
    aside { position:fixed; inset:4.25rem auto 0 0; z-index:15; width:min(18rem,86vw); transform:translateX(-105%); transition:transform .2s ease; box-shadow:1rem 0 3rem rgba(0,0,0,.4); }
    aside.open { transform:translateX(0); }
    main { padding:1.2rem; }
    .hero-grid { grid-template-columns:1fr; }
  }
  @media (prefers-reduced-motion:reduce) { aside,nav button { transition:none; } }
</style>
