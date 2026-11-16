(() => {
  'use strict';
  if (window !== window.top || !['muse.ai', 'www.muse.ai'].includes(location.hostname) || location.protocol !== 'https:') return;
  if (window.__museBubbleTheme) return;
  let root = document.documentElement;
  let observer;
  function markComposer() {
    const fields = document.querySelectorAll('textarea,[contenteditable="true"][role="textbox"],div[contenteditable="true"]');
    for (const field of fields) {
      if (!field.getClientRects().length) continue;
      let box = field.parentElement;
      for (let depth = 0; box && depth < 5; depth++, box = box.parentElement) {
        const rect = box.getBoundingClientRect();
        if (rect.height >= 36 && rect.height <= 180 && box.querySelector('button') &&
            rect.width >= 200 && box.querySelectorAll('textarea,[contenteditable="true"]').length === 1) {
          box.setAttribute('data-musebubble-composer', '');
          break;
        }
      }
    }
  }
  let scheduled = false;
  function schedule() {
    if (scheduled) return;
    scheduled = true;
    requestAnimationFrame(() => { scheduled = false; markComposer(); });
  }
  function mount() {
    root = document.documentElement;
    if (!root || !document.head) return;
    if (!document.getElementById('musebubble-theme')) {
      const style = document.createElement('style');
      style.id = 'musebubble-theme';
      style.textContent = __MUSEBUBBLE_CSS__;
      document.head.appendChild(style);
    }
    if (!observer) {
      observer = new MutationObserver(schedule);
      observer.observe(document.body || root, {childList: true, subtree: true});
    }
    schedule();
  }
  window.__museBubbleTheme = (theme, enabled) => {
    root = document.documentElement;
    if (!root) return;
    if (enabled) root.setAttribute('data-musebubble-theme', theme === 'light' ? 'light' : 'dark');
    else root.removeAttribute('data-musebubble-theme');
    mount();
  };
  document.addEventListener('DOMContentLoaded', mount, {once: true});
  mount();
})();
