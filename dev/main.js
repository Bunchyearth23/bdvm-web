const preview = new URLSearchParams(location.search).get('preview') === '1';
if (preview) await import('./preview.js');
await import('../frontend/main.js');
await import('../../BDVM.Management/Assets/app.js');
await import('../../BDVM.Dispatch/Assets/app.js');
// Wait until Svelte has mounted its module outlet.
await new Promise(resolve => requestAnimationFrame(resolve));
await import('../Assets/bootstrap.js');
if (preview) {
  const banner = document.createElement('aside');
  banner.className = 'development-banner';
  banner.style.cssText = 'position:fixed;bottom:0;left:0;right:0;z-index:50;padding:10px 16px;background:#382b13;color:#ffe0a0;border-top:1px solid #e59a1a;display:flex;flex-wrap:wrap;gap:12px;font:14px system-ui';
  const label = document.createElement('strong');
  label.textContent = 'Preview · simulated data · no game commands';
  const selector = document.createElement('select');
  selector.setAttribute('aria-label', 'Preview scenario');
  for (const [value, text] of [['populated','Populated'],['empty','Empty'],['offline','Offline'],['refused','Command refused'],['conflict','Version conflict'],['timeout','Lost command response']]) {
    const option = document.createElement('option'); option.value = value; option.textContent = text; selector.append(option);
  }
  selector.value = new URLSearchParams(location.search).get('scenario') || 'populated';
  selector.onchange = () => { const url = new URL(location.href); url.searchParams.set('scenario', selector.value); location.href = url.href; };
  const live = document.createElement('a'); live.href = '/'; live.textContent = 'Open live connection'; live.style.color = 'inherit';
  banner.append(label, selector, live); document.body.append(banner); document.body.style.paddingBottom = '100px';
} else {
  const link = document.createElement('a');link.href = '/?preview=1';link.textContent = 'Open simulated preview';
  link.style.cssText = 'position:fixed;bottom:12px;right:16px;z-index:30;padding:10px;background:#242424;color:#ffc15a;border:1px solid #6b5228';
  document.body.append(link);
}
