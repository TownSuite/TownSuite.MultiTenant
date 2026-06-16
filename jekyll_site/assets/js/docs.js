// TownSuite.MultiTenant docs — interactions ported from the Claude Design
// prototype's <x-dc> component: syntax highlighting, copy buttons, tabbed code
// panels, and sidebar smooth-scroll + scrollspy.
(function () {
  function ready(fn) {
    if (document.readyState !== 'loading') fn();
    else document.addEventListener('DOMContentLoaded', fn);
  }

  function highlight(codeEl) {
    if (codeEl.dataset.hl) return;
    codeEl.dataset.hl = '1';
    var text = codeEl.textContent;
    var C = { com: '#5e7591', str: '#86c98a', key: '#79b8ff', num: '#f0a85c', kw: '#c79bf0', type: '#5fd6c4', def: '#cdd9e6' };
    var kw = new Set(['var', 'await', 'async', 'new', 'return', 'public', 'private', 'protected', 'class', 'void', 'string', 'int', 'using', 'namespace', 'this', 'true', 'false', 'null', 'foreach', 'for', 'in', 'while', 'const', 'static', 'override', 'readonly', 'if', 'else', 'bool', 'double', 'long']);
    var re = /(\/\/[^\n]*)|("(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*')|(-?\b\d+(?:\.\d+)?\b)|([A-Za-z_$][\w$]*)|(\s+)|([^\s])/g;
    var frag = document.createDocumentFragment();
    var m;
    while ((m = re.exec(text))) {
      var val = m[0];
      var color = C.def;
      if (m[1]) color = C.com;
      else if (m[2]) { var rest = text.slice(re.lastIndex); color = /^\s*:/.test(rest) ? C.key : C.str; }
      else if (m[3]) color = C.num;
      else if (m[4]) { color = kw.has(m[4]) ? C.kw : (/^[A-Z]/.test(m[4]) ? C.type : C.def); }
      else if (m[5]) { frag.appendChild(document.createTextNode(val)); continue; }
      var span = document.createElement('span');
      span.textContent = val;
      span.style.color = color;
      frag.appendChild(span);
    }
    codeEl.textContent = '';
    codeEl.appendChild(frag);
  }

  function enhance() {
    var root = document.getElementById('tsmt-root');
    if (!root) return;

    // syntax highlight
    root.querySelectorAll('pre code').forEach(function (c) { highlight(c); });

    // copy buttons
    root.querySelectorAll('[data-copy]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var fig = btn.closest('figure');
        var code = fig && fig.querySelector('code');
        var txt = code ? code.textContent : '';
        if (navigator.clipboard) navigator.clipboard.writeText(txt).catch(function () {});
        var lbl = btn.querySelector('[data-lbl]') || btn;
        var old = lbl.textContent;
        lbl.textContent = 'Copied';
        btn.style.color = 'var(--green)';
        btn.style.borderColor = 'var(--green)';
        setTimeout(function () { lbl.textContent = old; btn.style.color = ''; btn.style.borderColor = ''; }, 1300);
      });
    });

    // tabs
    root.querySelectorAll('[data-tabs]').forEach(function (group) {
      var btns = [].slice.call(group.querySelectorAll('[data-tab]'));
      var panels = [].slice.call(group.querySelectorAll('[data-panel]'));
      var sel = function (i) {
        btns.forEach(function (b) {
          var on = b.dataset.tab === String(i);
          b.style.color = on ? 'var(--brand)' : '#8a93a3';
          b.style.borderBottomColor = on ? 'var(--brand)' : 'transparent';
          b.style.fontWeight = on ? '600' : '500';
        });
        panels.forEach(function (p) { p.style.display = p.dataset.panel === String(i) ? '' : 'none'; });
      };
      btns.forEach(function (b) { b.addEventListener('click', function () { sel(b.dataset.tab); }); });
      sel('0');
    });

    // nav: smooth scroll + scrollspy
    var links = [].slice.call(root.querySelectorAll('[data-link]'));
    links.forEach(function (l) {
      l.addEventListener('click', function (e) {
        var href = l.getAttribute('href') || '';
        if (href.charAt(0) !== '#') return;
        e.preventDefault();
        var el = document.getElementById(href.slice(1));
        if (el) { var y = el.getBoundingClientRect().top + window.scrollY - 22; window.scrollTo({ top: y, behavior: 'smooth' }); }
      });
    });
    var setActive = function (id) {
      links.forEach(function (l) {
        var on = l.getAttribute('href') === '#' + id;
        l.style.color = on ? 'var(--brand)' : 'var(--ink-2)';
        l.style.background = on ? 'color-mix(in srgb, var(--brand) 9%, transparent)' : 'transparent';
        l.style.fontWeight = on ? '600' : '500';
      });
    };
    var targets = links.map(function (l) { return (l.getAttribute('href') || '').slice(1); }).filter(Boolean);
    var obs = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) { if (en.isIntersecting) { setActive(en.target.id); } });
    }, { rootMargin: '-8% 0px -78% 0px', threshold: 0 });
    targets.forEach(function (id) { var el = document.getElementById(id); if (el) obs.observe(el); });
  }

  ready(enhance);
})();
