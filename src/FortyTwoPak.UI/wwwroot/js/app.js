/* 42pak – UI Controller */
(function () {
  'use strict';

  // ── C# bridge ──
  const B = () => window.chrome.webview.hostObjects.sync.bridge;

  // ── DOM refs ──
  const $ = (s, ctx) => (ctx || document).querySelector(s);
  const $$ = (s, ctx) => (ctx || document).querySelectorAll(s);

  // ── Navigation ──
  const viewTitles = {
    create: 'Create Archive',
    manage: 'Manage Archive',
    convert: 'Convert EIX/EPK',
    settings: 'Settings',
    help: 'Help'
  };

  document.addEventListener('DOMContentLoaded', () => {
    $$('.nav-item[data-view]').forEach(n => n.addEventListener('click', () => switchView(n.dataset.view)));
    bindCreate();
    bindManage();
    bindConvert();
    bindSettings();
    loadPrefs();
  });

  function switchView(id) {
    $$('.nav-item[data-view]').forEach(n => n.classList.toggle('active', n.dataset.view === id));
    $$('.view').forEach(v => v.classList.toggle('active', v.id === 'v-' + id));
    $('#view-title').textContent = viewTitles[id] || id;
    $('#search-wrap').style.display = id === 'manage' ? '' : 'none';
  }

  // ── CREATE ──
  function bindCreate() {
    $('#c-enc').addEventListener('change', () => {
      $('#c-pass-fields').style.display = $('#c-enc').checked ? '' : 'none';
    });
    $('#c-comp').addEventListener('input', () => {
      $('#c-comp-val').textContent = $('#c-comp').value;
    });
  }

  window.pickSourceFolder = function () {
    const path = B().PickFolder();
    if (path) $('#c-src').value = path;
  };

  window.pickOutputFile = function () {
    const path = B().PickSaveFile('VPK Archive (*.vpk)|*.vpk', '.vpk');
    if (path) $('#c-out').value = path;
  };

  window.buildVpk = function () {
    const src = $('#c-src').value.trim();
    const out = $('#c-out').value.trim();
    if (!src || !out) return modal('Missing Fields', 'Select both a source folder and output path.');

    const enc = $('#c-enc').checked;
    if (enc) {
      const p1 = $('#c-pass').value;
      const p2 = $('#c-pass2').value;
      if (p1.length < 8) return modal('Weak Passphrase', 'Passphrase must be at least 8 characters.');
      if (p1 !== p2) return modal('Mismatch', 'Passphrases do not match.');
    }

    const opts = JSON.stringify({
      passphrase: enc ? $('#c-pass').value : null,
      compressionLevel: parseInt($('#c-comp').value, 10),
      compressionAlgorithm: $('#c-algo').value,
      mangleFileNames: $('#c-mangle').checked,
      author: $('#c-author').value.trim() || null,
      comment: $('#c-comment').value.trim() || null
    });

    const btn = $('#btn-build');
    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Building...';

    showProgress();

    setTimeout(() => {
      try {
        const r = JSON.parse(B().BuildVpk(src, out, opts));
        hideProgress();
        if (r.success) {
          modal('Build Complete', r.message);
        } else {
          modal('Build Failed', r.message);
        }
      } catch (e) {
        hideProgress();
        modal('Error', e.message || String(e));
      } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-hammer"></i> Build Archive';
      }
    }, 50);
  };

  function showProgress() {
    const el = $('#c-progress');
    el.style.display = '';
    $('#c-progress-fill').style.width = '0%';
    $('#c-progress-txt').textContent = '';
    animateProgress();
  }

  function hideProgress() {
    const el = $('#c-progress');
    $('#c-progress-fill').style.width = '100%';
    $('#c-progress-txt').textContent = '100%';
    setTimeout(() => { el.style.display = 'none'; }, 400);
  }

  let _progTimer = null;
  function animateProgress() {
    let pct = 0;
    clearInterval(_progTimer);
    _progTimer = setInterval(() => {
      pct = Math.min(pct + Math.random() * 8, 90);
      $('#c-progress-fill').style.width = pct + '%';
      $('#c-progress-txt').textContent = Math.round(pct) + '%';
    }, 200);
  }

  // ── MANAGE ──
  let _allEntries = [];

  function bindManage() {
    $('#search-input').addEventListener('input', filterEntries);
  }

  window.openVpkFile = function () {
    const path = B().PickFile('VPK Archive (*.vpk)|*.vpk');
    if (!path) return;

    const pass = prompt('Enter passphrase (leave blank if none):') || '';
    try {
      const r = JSON.parse(B().OpenVpk(path, pass));
      if (!r.success) return modal('Open Failed', r.message);
      displayArchive(r);
    } catch (e) {
      modal('Error', e.message || String(e));
    }
  };

  function displayArchive(data) {
    const h = data.header;
    const meta = $('#archive-meta');
    meta.style.display = '';
    meta.innerHTML = [
      mp('Version', h.Version),
      mp('Files', h.EntryCount),
      mp('Encrypted', h.IsEncrypted ? 'Yes' : 'No'),
      mp('Compression', h.CompressionAlgorithm || 'LZ4'),
      mp('Level', h.CompressionLevel),
      mp('Mangled', h.FileNamesMangled ? 'Yes' : 'No'),
      mp('Author', h.Author || '—'),
      mp('Created', new Date(h.CreatedAt).toLocaleDateString())
    ].join('');

    _allEntries = data.entries || [];
    renderEntries(_allEntries);

    $('#btn-extract').disabled = false;
    $('#btn-validate').disabled = false;
  }

  function mp(label, val) {
    return '<span><span class="m-label">' + label + '</span><span class="m-val">' + escapeHtml(String(val)) + '</span></span>';
  }

  function renderEntries(entries) {
    const list = $('#file-list');
    if (!entries.length) {
      list.innerHTML = '<div class="empty-msg"><i class="fa-solid fa-box-open"></i><p>No files in archive.</p></div>';
      return;
    }
    list.innerHTML = entries.map(e => {
      const tags = [];
      if (e.IsEncrypted) tags.push('<span class="f-tag enc">ENC</span>');
      if (e.IsCompressed) tags.push('<span class="f-tag lz4">LZ4</span>');
      return '<div class="file-row">' +
        '<span class="f-icon"><i class="' + getFileIcon(e.FileName) + '"></i></span>' +
        '<span class="f-name" title="' + escapeHtml(e.FileName) + '">' + escapeHtml(e.FileName) + '</span>' +
        tags.join('') +
        '<span class="f-size">' + formatSize(e.OriginalSize) + '</span>' +
        '</div>';
    }).join('');
  }

  function filterEntries() {
    const q = $('#search-input').value.toLowerCase();
    if (!q) return renderEntries(_allEntries);
    renderEntries(_allEntries.filter(e => e.FileName.toLowerCase().includes(q)));
  }

  window.extractAllFiles = function () {
    const dir = B().PickFolder();
    if (!dir) return;
    const pass = prompt('Passphrase (blank if none):') || '';
    try {
      const r = JSON.parse(B().ExtractAll(dir, pass));
      modal(r.success ? 'Extracted' : 'Error', r.message);
    } catch (e) {
      modal('Error', e.message || String(e));
    }
  };

  window.validateArchive = function () {
    const pass = prompt('Passphrase (blank if none):') || '';
    try {
      const r = JSON.parse(B().ValidateVpk(pass));
      if (!r.success) return modal('Error', r.message);
      if (r.isValid) {
        modal('Valid', 'Archive integrity verified. ' + r.validFiles + ' files OK.');
      } else {
        modal('Integrity Issues', 'Errors found:\n' + (r.errors || []).join('\n'));
      }
    } catch (e) {
      modal('Error', e.message || String(e));
    }
  };

  // ── CONVERT ──
  function bindConvert() {
    $('#cv-enc').addEventListener('change', () => {
      $('#cv-pass-row').style.display = $('#cv-enc').checked ? '' : 'none';
    });
  }

  window.pickEixFile = function () {
    const path = B().PickFile('EIX Index (*.eix)|*.eix');
    if (!path) return;
    $('#cv-eix').value = path;
    loadEixPreview(path);
  };

  function loadEixPreview(eixPath) {
    try {
      const r = JSON.parse(B().ReadEixListing(eixPath));
      if (!r.success) return;
      const grid = $('#eix-grid');
      $('#eix-preview').style.display = '';
      grid.innerHTML = r.entries.map(e => {
        const tags = [];
        if (!e.canExtract) tags.push('<span class="f-tag warn">ENCRYPTED</span>');
        return '<div class="file-row">' +
          '<span class="f-icon"><i class="' + getFileIcon(e.FileName) + '"></i></span>' +
          '<span class="f-name" title="' + escapeHtml(e.FileName) + '">' + escapeHtml(e.FileName) + '</span>' +
          tags.join('') +
          '<span class="f-size">' + formatSize(e.RealDataSize) + '</span>' +
          '</div>';
      }).join('');
    } catch (_) {}
  }

  window.pickConvertOutput = function () {
    const path = B().PickSaveFile('VPK Archive (*.vpk)|*.vpk', '.vpk');
    if (path) $('#cv-out').value = path;
  };

  window.convertPak = function () {
    const eix = $('#cv-eix').value.trim();
    const out = $('#cv-out').value.trim();
    if (!eix || !out) return modal('Missing Fields', 'Select both .eix input and .vpk output.');

    const opts = JSON.stringify({
      passphrase: $('#cv-enc').checked ? $('#cv-pass').value : null,
      compressionLevel: parseInt($('#c-comp').value, 10)
    });

    const btn = $('#btn-convert');
    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Converting...';

    const log = $('#cv-log');
    log.style.display = '';
    $('#cv-log-txt').textContent = 'Starting conversion...\n';

    setTimeout(() => {
      try {
        const r = JSON.parse(B().ConvertEixEpk(eix, out, opts));
        if (r.success) {
          appendLog('Converted: ' + r.convertedFiles + '/' + r.totalEntries + ' files');
          if (r.skippedFiles > 0) appendLog('Skipped: ' + r.skippedFiles + ' (encrypted)');
          if (r.errors && r.errors.length) r.errors.forEach(e => appendLog('ERR: ' + e));
          appendLog('Done.');
          modal('Conversion Complete', r.convertedFiles + ' of ' + r.totalEntries + ' files converted.');
        } else {
          appendLog('FAILED: ' + r.message);
          modal('Error', r.message);
        }
      } catch (e) {
        appendLog('EXCEPTION: ' + e.message);
        modal('Error', e.message || String(e));
      } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-right-left"></i> Convert';
      }
    }, 50);
  };

  function appendLog(msg) {
    const el = $('#cv-log-txt');
    el.textContent += msg + '\n';
    el.scrollTop = el.scrollHeight;
  }

  // ── SETTINGS ──
  function bindSettings() {
    $('#s-comp').addEventListener('input', () => {
      $('#s-comp-val').textContent = $('#s-comp').value;
    });
  }

  function loadPrefs() {
    try {
      const p = JSON.parse(localStorage.getItem('42pak_prefs') || '{}');
      if (p.darkMode === false) {
        document.body.classList.add('light');
        $('#s-dark').checked = false;
      }
      if (p.compression != null) {
        $('#s-comp').value = p.compression;
        $('#s-comp-val').textContent = p.compression;
        $('#c-comp').value = p.compression;
        $('#c-comp-val').textContent = p.compression;
      }
      if (p.encryptByDefault === false) {
        $('#s-enc').checked = false;
        $('#c-enc').checked = false;
        $('#c-enc').dispatchEvent(new Event('change'));
      }
    } catch (_) {}
  }

  function savePrefs() {
    localStorage.setItem('42pak_prefs', JSON.stringify({
      darkMode: !document.body.classList.contains('light'),
      compression: parseInt($('#s-comp').value, 10),
      encryptByDefault: $('#s-enc').checked
    }));
  }

  window.toggleTheme = function () {
    document.body.classList.toggle('light');
    savePrefs();
  };

  // ── MODAL ──
  function modal(title, msg) {
    $('#modal-title').textContent = title;
    $('#modal-body').textContent = msg;
    $('#modal-bg').classList.add('open');
  }

  window.closeModal = function () {
    $('#modal-bg').classList.remove('open');
  };

  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') window.closeModal();
  });

  // ── TOGGLE PASSWORD ──
  window.toggleVis = function (id) {
    const inp = document.getElementById(id);
    inp.type = inp.type === 'password' ? 'text' : 'password';
  };

  // ── UTILS ──
  function escapeHtml(s) {
    const d = document.createElement('div');
    d.appendChild(document.createTextNode(s));
    return d.innerHTML;
  }

  function formatSize(bytes) {
    if (bytes == null || bytes === 0) return '0 B';
    const k = 1024;
    const u = ['B', 'KB', 'MB', 'GB'];
    const i = Math.min(Math.floor(Math.log(bytes) / Math.log(k)), u.length - 1);
    return (bytes / Math.pow(k, i)).toFixed(i ? 1 : 0) + ' ' + u[i];
  }

  function getFileIcon(name) {
    const ext = (name.split('.').pop() || '').toLowerCase();
    const map = {
      png: 'fa-solid fa-image', jpg: 'fa-solid fa-image', bmp: 'fa-solid fa-image',
      tga: 'fa-solid fa-image', dds: 'fa-solid fa-image',
      wav: 'fa-solid fa-volume-high', mp3: 'fa-solid fa-music', ogg: 'fa-solid fa-music',
      mse: 'fa-solid fa-clapperboard', msm: 'fa-solid fa-clapperboard',
      gr2: 'fa-solid fa-cube', msa: 'fa-solid fa-person-running',
      py: 'fa-solid fa-code', lua: 'fa-solid fa-code', txt: 'fa-solid fa-file-lines',
      cfg: 'fa-solid fa-sliders', ini: 'fa-solid fa-sliders',
      fnt: 'fa-solid fa-font', ttf: 'fa-solid fa-font',
    };
    return map[ext] || 'fa-solid fa-file';
  }
})();
