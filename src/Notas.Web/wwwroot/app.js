window.notas = (function () {
  const fire = el => el.dispatchEvent(new Event('input', { bubbles: true }));
  const byId = id => document.getElementById(id);
  const sidebarWidthKey = 'notas.sidebar.width';
  const sidebarCollapsedKey = 'notas.sidebar.collapsed';
  const sidebarOrganizationKey = 'notas.sidebar.organization';
  const sidebarMinWidth = 220;
  const sidebarMaxWidth = 420;
  let sidebarCleanup;

  function wrap(el, mark, placeholder) {
    const s = el.selectionStart, e = el.selectionEnd;
    const sel = el.value.slice(s, e) || placeholder;
    el.setRangeText(mark + sel + mark, s, e, 'end');
    el.setSelectionRange(s + mark.length, s + mark.length + sel.length);
  }

  function prefixLines(el, prefixFn) {
    const s = el.selectionStart, e = el.selectionEnd;
    const start = el.value.lastIndexOf('\n', s - 1) + 1;
    const block = el.value.slice(start, e);
    const out = block.split('\n').map((l, i) => prefixFn(i) + l).join('\n');
    el.setRangeText(out, start, e, 'end');
    el.setSelectionRange(start, start + out.length);
  }

  function format(id, kind) {
    const el = byId(id); if (!el) return;
    el.focus();
    switch (kind) {
      case 'bold': wrap(el, '**', 'texto'); break;
      case 'italic': wrap(el, '*', 'texto'); break;
      case 'code': wrap(el, '`', 'código'); break;
      case 'h2': prefixLines(el, () => '## '); break;
      case 'ul': prefixLines(el, () => '- '); break;
      case 'ol': prefixLines(el, i => (i + 1) + '. '); break;
      case 'task': prefixLines(el, () => '- [ ] '); break;
      case 'quote': prefixLines(el, () => '> '); break;
      case 'link': {
        const s = el.selectionStart, e = el.selectionEnd;
        const sel = el.value.slice(s, e) || 'texto';
        el.setRangeText('[' + sel + '](https://)', s, e, 'end');
        break;
      }
    }
    fire(el);
  }

  function insert(id, text) {
    const el = byId(id); if (!el) return;
    el.focus();
    const s = el.selectionStart, e = el.selectionEnd;
    el.setRangeText('\n' + text + '\n', s, e, 'end');
    fire(el);
  }

  function getSidebarMaxWidth(app) {
    const available = app?.clientWidth || window.innerWidth;
    if (available <= 760) return sidebarMaxWidth;
    const listWidth = available <= 1100
      ? Math.max(210, Math.min(340, available * .3))
      : 340;
    return Math.max(sidebarMinWidth, Math.min(sidebarMaxWidth, available - listWidth - 240));
  }

  function currentSidebarWidth() {
    const value = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--sidebar-w'));
    return Number.isFinite(value) ? value : 272;
  }

  function setSidebarWidth(value, app, handle) {
    const max = getSidebarMaxWidth(app);
    const width = Math.round(Math.max(sidebarMinWidth, Math.min(max, Number(value) || 272)));
    document.documentElement.style.setProperty('--sidebar-w', `${width}px`);
    if (handle) {
      handle.setAttribute('aria-valuenow', String(width));
      handle.setAttribute('aria-valuemax', String(max));
    }
    return width;
  }

  function loadSidebarPreferences() {
    const storedWidth = Number.parseInt(localStorage.getItem(sidebarWidthKey), 10);
    const requestedWidth = Number.isFinite(storedWidth) && storedWidth >= sidebarMinWidth && storedWidth <= sidebarMaxWidth
      ? storedWidth
      : 272;
    const app = document.querySelector('.app');
    const handle = document.querySelector('.sidebar-resizer');
    const width = setSidebarWidth(requestedWidth, app, handle);
    return { width, collapsed: localStorage.getItem(sidebarCollapsedKey) === 'true' };
  }

  function saveSidebarPreferences(collapsed) {
    const app = document.querySelector('.app');
    const handle = document.querySelector('.sidebar-resizer');
    const width = setSidebarWidth(currentSidebarWidth(), app, handle);
    localStorage.setItem(sidebarWidthKey, String(width));
    localStorage.setItem(sidebarCollapsedKey, collapsed ? 'true' : 'false');
  }

  function loadSidebarOrganization() {
    const saved = localStorage.getItem(sidebarOrganizationKey);
    if (!saved) return { folderOrder: [], folderIcons: {}, folderColors: {}, pinnedRecentNoteIds: [] };
    try {
      const value = JSON.parse(saved);
      if (!value || typeof value !== 'object') throw new SyntaxError('Formato inválido.');
      return {
        folderOrder: Array.isArray(value.folderOrder) ? value.folderOrder : [],
        folderIcons: value.folderIcons && typeof value.folderIcons === 'object' ? value.folderIcons : {},
        folderColors: value.folderColors && typeof value.folderColors === 'object' ? value.folderColors : {},
        pinnedRecentNoteIds: Array.isArray(value.pinnedRecentNoteIds) ? value.pinnedRecentNoteIds : []
      };
    } catch (error) {
      if (!(error instanceof SyntaxError)) throw error;
      console.warn('Preferências da sidebar inválidas; usando os padrões.', error);
      localStorage.removeItem(sidebarOrganizationKey);
      return { folderOrder: [], folderIcons: {}, folderColors: {}, pinnedRecentNoteIds: [] };
    }
  }

  function saveSidebarOrganization(value) {
    localStorage.setItem(sidebarOrganizationKey, JSON.stringify(value));
  }

  function initSidebarInteractions(dotNet) {
    if (sidebarCleanup) sidebarCleanup();

    const app = document.querySelector('.app');
    const handle = document.querySelector('.sidebar-resizer');
    if (!app || !handle) return;

    setSidebarWidth(currentSidebarWidth(), app, handle);
    let dragging = false;
    let startX = 0;
    let startWidth = 0;

    const resizeTo = width => setSidebarWidth(width, app, handle);
    const finishResize = () => {
      if (!dragging) return;
      dragging = false;
      document.documentElement.classList.remove('sidebar-resizing');
      saveSidebarPreferences(app.classList.contains('sidebar-collapsed'));
    };
    const onPointerDown = event => {
      if (event.button !== 0 || app.classList.contains('sidebar-collapsed') || window.innerWidth <= 760) return;
      event.preventDefault();
      dragging = true;
      startX = event.clientX;
      startWidth = currentSidebarWidth();
      handle.setPointerCapture(event.pointerId);
      document.documentElement.classList.add('sidebar-resizing');
    };
    const onPointerMove = event => {
      if (dragging) resizeTo(startWidth + event.clientX - startX);
    };
    const onResizeKeyDown = event => {
      if ((event.key !== 'ArrowLeft' && event.key !== 'ArrowRight')
          || app.classList.contains('sidebar-collapsed') || window.innerWidth <= 760) return;
      event.preventDefault();
      resizeTo(currentSidebarWidth() + (event.key === 'ArrowRight' ? 12 : -12));
      saveSidebarPreferences(false);
    };
    const onShortcut = event => {
      const target = event.target;
      if (!(event.ctrlKey || event.metaKey) || event.altKey || event.key.toLowerCase() !== 'b'
          || (target instanceof Element && target.closest('[data-md-editor]'))) return;
      event.preventDefault();
      if (window.innerWidth <= 760) {
        dotNet.invokeMethodAsync('SetDrawerOpenFromShortcutAsync', !app.classList.contains('drawer-open'));
        return;
      }
      const collapsed = !app.classList.contains('sidebar-collapsed');
      dotNet.invokeMethodAsync('SetSidebarCollapsedFromShortcutAsync', collapsed)
        .then(() => saveSidebarPreferences(collapsed));
    };
    const onWindowResize = () => {
      const storedWidth = Number.parseInt(localStorage.getItem(sidebarWidthKey), 10);
      resizeTo(Number.isFinite(storedWidth) ? storedWidth : currentSidebarWidth());
    };

    handle.addEventListener('pointerdown', onPointerDown);
    handle.addEventListener('keydown', onResizeKeyDown);
    window.addEventListener('pointermove', onPointerMove);
    window.addEventListener('pointerup', finishResize);
    window.addEventListener('pointercancel', finishResize);
    window.addEventListener('resize', onWindowResize);
    document.addEventListener('keydown', onShortcut);

    sidebarCleanup = () => {
      finishResize();
      handle.removeEventListener('pointerdown', onPointerDown);
      handle.removeEventListener('keydown', onResizeKeyDown);
      window.removeEventListener('pointermove', onPointerMove);
      window.removeEventListener('pointerup', finishResize);
      window.removeEventListener('pointercancel', finishResize);
      window.removeEventListener('resize', onWindowResize);
      document.removeEventListener('keydown', onShortcut);
      document.documentElement.classList.remove('sidebar-resizing');
      sidebarCleanup = undefined;
    };
  }

  function disposeSidebarInteractions() {
    if (sidebarCleanup) sidebarCleanup();
  }

  // Atalhos: Ctrl/Cmd+B, Ctrl/Cmd+I no editor; Ctrl/Cmd+K foca a busca.
  document.addEventListener('keydown', ev => {
    const mod = ev.ctrlKey || ev.metaKey;
    if (!mod) return;
    const key = ev.key.toLowerCase();
    if (key === 'k') {
      const s = byId('search-input');
      if (s) { ev.preventDefault(); s.focus(); s.select(); }
    } else if ((key === 'b' || key === 'i') && ev.target && ev.target.hasAttribute && ev.target.hasAttribute('data-md-editor')) {
      ev.preventDefault();
      format(ev.target.id, key === 'b' ? 'bold' : 'italic');
    }
  });

  return {
    format, insert,
    focus: id => { const el = byId(id); if (el) el.focus(); },
    tzOffset: () => -new Date().getTimezoneOffset(),
    loadSidebarPreferences,
    saveSidebarPreferences,
    loadSidebarOrganization,
    saveSidebarOrganization,
    initSidebarInteractions,
    disposeSidebarInteractions,
    toggleTheme: () => {
      const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
      document.documentElement.dataset.theme = next;
      try { localStorage.setItem('notas.theme', next); } catch (e) {}
      return next;
    },
    // Avisa ao fechar a aba com alterações ainda não salvas.
    setDirty: flag => {
      window.onbeforeunload = flag ? ev => { ev.preventDefault(); ev.returnValue = ''; } : null;
    }
  };
})();
