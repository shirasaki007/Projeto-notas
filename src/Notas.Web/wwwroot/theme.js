// Executa no <head>, antes da renderização, para evitar "flash" de tema errado.
(function () {
  try {
    var saved = localStorage.getItem('notas.theme');
    var theme = saved || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    document.documentElement.dataset.theme = theme;
  } catch (e) { document.documentElement.dataset.theme = 'light'; }
})();
