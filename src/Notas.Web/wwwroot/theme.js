// Executa no <head>, antes da renderização, para evitar "flash" de tema errado.
(function () {
  try {
    var saved = localStorage.getItem('notas.theme');
    var theme = saved || 'dark';
    document.documentElement.dataset.theme = theme;
  } catch (e) { document.documentElement.dataset.theme = 'dark'; }
})();
