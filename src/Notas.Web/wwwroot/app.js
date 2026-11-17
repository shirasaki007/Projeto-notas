window.notas = (function () {
  const fire = el => el.dispatchEvent(new Event('input', { bubbles: true }));
  const byId = id => document.getElementById(id);

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
