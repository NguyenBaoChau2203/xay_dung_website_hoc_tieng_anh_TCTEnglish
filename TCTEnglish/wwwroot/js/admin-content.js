/**
 * TCT English - Admin content kit (vocabulary sets, listening lessons, payments, notifications).
 *
 * Two layers:
 *  1. Pure helpers (no DOM) - unit-tested in scripts/tests/test_admin_content.cjs. They mirror the server
 *     rules in AdminInputRules.cs for instant feedback only; the server stays authoritative.
 *  2. Small DOM glue, started by init() on DOMContentLoaded: character counters, type-the-name delete
 *     confirmation (SweetAlert2), unsaved-changes guard, pending state on submit, auto-submit selects.
 *
 * No HTML is built from data anywhere in this file: text goes through textContent / SweetAlert2 `text`.
 */
(function (root, factory) {
  'use strict';
  var api = factory();
  if (typeof module === 'object' && module.exports) {
    module.exports = api;
  } else {
    root.AdminContent = api;
    if (typeof document !== 'undefined') {
      if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { api.init(document); });
      } else {
        api.init(document);
      }
    }
  }
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  var YOUTUBE_ID = /^[A-Za-z0-9_-]{11}$/;
  var YOUTUBE_HOSTS = [
    'youtube.com', 'www.youtube.com', 'm.youtube.com', 'music.youtube.com',
    'youtu.be', 'www.youtu.be', 'youtube-nocookie.com', 'www.youtube-nocookie.com'
  ];
  var SCHEMELESS = [
    'youtu.be/', 'youtube.com/', 'www.youtube.com/', 'm.youtube.com/', 'music.youtube.com/',
    'youtube-nocookie.com/', 'www.youtube-nocookie.com/'
  ];
  var PATH_PREFIXES = ['embed', 'shorts', 'live', 'v'];
  var DURATION = /^(?:(\d{1,2}):([0-5]\d):([0-5]\d)|(\d{1,3}):([0-5]\d))$/;
  var CEFR = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'];

  // ---------------------------------------------------------------- pure helpers

  function parseYoutubeId(input) {
    if (typeof input !== 'string' || !input.trim()) return null;
    var value = input.trim();
    if (YOUTUBE_ID.test(value)) return value;

    var lower = value.toLowerCase();
    if (value.indexOf('://') < 0 && SCHEMELESS.some(function (p) { return lower.indexOf(p) === 0; })) {
      value = 'https://' + value;
    }

    var url;
    try { url = new URL(value); } catch (e) { return null; }
    if (url.protocol !== 'https:' && url.protocol !== 'http:') return null;
    if (YOUTUBE_HOSTS.indexOf(url.hostname.toLowerCase()) < 0) return null;

    var segments = url.pathname.split('/').filter(Boolean);
    var candidate = null;
    if (/youtu\.be$/i.test(url.hostname)) {
      candidate = segments[0] || null;
    } else if (segments.length === 1 && segments[0].toLowerCase() === 'watch') {
      candidate = url.searchParams.get('v');
    } else if (segments.length >= 2 && PATH_PREFIXES.indexOf(segments[0].toLowerCase()) >= 0) {
      candidate = segments[1];
    }
    return candidate && YOUTUBE_ID.test(candidate) ? candidate : null;
  }

  function youtubeThumbnailUrl(id) {
    return id ? 'https://img.youtube.com/vi/' + id + '/hqdefault.jpg' : null;
  }

  function isSafeImageUrl(url) {
    if (url == null || !String(url).trim()) return true;
    var value = String(url);
    if (/[\s\u0000-\u001f\u007f]/.test(value)) return false;
    if (value.charAt(0) === '/') {
      if (value.charAt(1) === '/' || value.charAt(1) === '\\' || value.indexOf('\\') >= 0) return false;
      return value.split('/').every(function (s) { return s !== '..' && s !== '.'; });
    }
    if (value.toLowerCase().indexOf('https://') !== 0 || value.length === 8 || value.charAt(8) === '/') return false;
    try {
      var parsed = new URL(value);
      return parsed.protocol === 'https:' && !!parsed.hostname && !parsed.username && !parsed.password;
    } catch (e) {
      return false;
    }
  }

  /** Returns {ok, value}; empty input is ok with value null; zero length is not ok. */
  function normalizeDuration(input) {
    if (input == null || !String(input).trim()) return { ok: true, value: null };
    var m = DURATION.exec(String(input).trim());
    if (!m) return { ok: false, value: null };
    var pad = function (n) { return (n < 10 ? '0' : '') + n; };
    var total;
    var text;
    if (m[1] !== undefined) {
      var h = parseInt(m[1], 10), mi = parseInt(m[2], 10), s = parseInt(m[3], 10);
      total = h * 3600 + mi * 60 + s;
      text = h + ':' + pad(mi) + ':' + pad(s);
    } else {
      var mm = parseInt(m[4], 10), ss = parseInt(m[5], 10);
      total = mm * 60 + ss;
      text = pad(mm) + ':' + pad(ss);
    }
    return total === 0 ? { ok: false, value: null } : { ok: true, value: text };
  }

  function isCefrLevel(level) { return CEFR.indexOf(level) >= 0; }

  /** "ok" up to 85% of max, "near" up to max, "over" beyond. */
  function counterState(length, max) {
    if (!(max > 0)) return 'ok';
    if (length > max) return 'over';
    return length >= Math.floor(max * 0.85) ? 'near' : 'ok';
  }

  function counterText(length, max) { return length + '/' + max; }

  /** Exact match after trimming what the admin typed; the expected name is never trimmed or case-folded. */
  function confirmNameMatches(typed, expected) {
    if (typeof typed !== 'string' || typeof expected !== 'string' || !expected) return false;
    return typed.trim() === expected;
  }

  /** "Lines[3].Text" + 0 -> "Lines[0].Text". Names without an index are returned unchanged. */
  function reindexName(name, index) {
    return String(name).replace(/\[\d+\]/, '[' + index + ']');
  }

  /** Same, also for ids and labels that carry the index after a dash ("line-3-text"). */
  function reindexId(id, index) {
    return String(id).replace(/-\d+(?=-|$)/, '-' + index);
  }

  /** New array with the item moved; out-of-range moves return an unchanged copy. */
  function moveItem(list, from, to) {
    var copy = list.slice();
    if (from < 0 || from >= copy.length || to < 0 || to >= copy.length || from === to) return copy;
    var item = copy.splice(from, 1)[0];
    copy.splice(to, 0, item);
    return copy;
  }

  /** Flat {name: value} snapshots; dirty when any key differs. */
  function isDirty(initial, current) {
    var keys = {};
    Object.keys(initial || {}).forEach(function (k) { keys[k] = true; });
    Object.keys(current || {}).forEach(function (k) { keys[k] = true; });
    return Object.keys(keys).some(function (k) { return (initial || {})[k] !== (current || {})[k]; });
  }

  /** Snapshot of a form-like list of {name, type, value, checked, disabled} entries. */
  function snapshotFields(fields) {
    var out = {};
    fields.forEach(function (f, i) {
      if (!f.name || f.disabled || f.type === 'file' || f.type === 'submit' || f.type === 'button') return;
      if ((f.type === 'checkbox' || f.type === 'radio') && !f.checked) return;
      if (f.name === '__RequestVerificationToken') return;
      var key = f.name;
      if (Object.prototype.hasOwnProperty.call(out, key)) key = key + '#' + i;
      out[key] = String(f.value == null ? '' : f.value);
    });
    return out;
  }

  // ---------------------------------------------------------------- DOM glue

  function qsa(rootEl, selector) { return Array.prototype.slice.call(rootEl.querySelectorAll(selector)); }

  function initCounters(doc) {
    qsa(doc, '[data-ac-max]').forEach(function (input) {
      var max = parseInt(input.getAttribute('data-ac-max'), 10);
      var target = input.id ? doc.querySelector('[data-ac-counter-for="' + input.id + '"]') : null;
      if (!target || !(max > 0)) return;
      var update = function () {
        var length = input.value.length;
        target.textContent = counterText(length, max);
        target.setAttribute('data-state', counterState(length, max));
      };
      input.addEventListener('input', update);
      update();
    });
  }

  function initAutoSubmit(doc) {
    qsa(doc, '[data-ac-autosubmit]').forEach(function (select) {
      select.addEventListener('change', function () {
        var formId = select.getAttribute('form');
        var form = formId ? doc.getElementById(formId) : select.form;
        if (!form) return;
        if (typeof form.requestSubmit === 'function') form.requestSubmit(); else form.submit();
      });
    });
  }

  function initPendingState(doc) {
    qsa(doc, 'form[data-ac-pending]').forEach(function (form) {
      form.addEventListener('submit', function (event) {
        if (event.defaultPrevented) return;
        if (form.getAttribute('data-ac-submitting') === 'true') {
          event.preventDefault();
          return;
        }
        form.setAttribute('data-ac-submitting', 'true');
        qsa(form, 'button[type="submit"]').forEach(function (button) {
          button.setAttribute('aria-busy', 'true');
          button.classList.add('is-pending');
        });
      });
    });
  }

  function initConfirmDelete(doc, win) {
    qsa(doc, 'form[data-ac-confirm-name]').forEach(function (form) {
      form.addEventListener('submit', function (event) {
        if (form.getAttribute('data-ac-confirmed') === 'true') return;
        event.preventDefault();
        var expected = form.getAttribute('data-ac-confirm-name') || '';
        var title = form.getAttribute('data-ac-confirm-title') || 'Xóa?';
        var impact = form.getAttribute('data-ac-confirm-impact') || '';
        var confirmText = form.getAttribute('data-ac-confirm-button') || 'Xóa';
        var input = form.querySelector('[data-ac-confirm-input]');
        var swal = win && win.Swal;
        if (!swal) {
          var typed = win && win.prompt ? win.prompt(title + '\n' + impact + '\nGõ lại "' + expected + '" để xác nhận:') : null;
          if (confirmNameMatches(typed || '', expected)) {
            if (input) input.value = typed.trim();
            form.setAttribute('data-ac-confirmed', 'true');
            form.submit();
          }
          return;
        }
        swal.fire({
          title: title,
          text: impact,
          icon: 'warning',
          input: 'text',
          inputLabel: 'Gõ lại "' + expected + '" để xác nhận',
          inputAttributes: { autocomplete: 'off', 'aria-label': 'Gõ lại tên để xác nhận xóa' },
          showCancelButton: true,
          confirmButtonText: confirmText,
          cancelButtonText: 'Hủy',
          focusCancel: false,
          returnFocus: true,
          inputValidator: function (value) {
            return confirmNameMatches(value, expected) ? undefined : 'Tên chưa khớp. Hãy gõ đúng như trên.';
          }
        }).then(function (result) {
          if (!result || !result.isConfirmed) return;
          if (input) input.value = String(result.value || '').trim();
          form.setAttribute('data-ac-confirmed', 'true');
          form.submit();
        });
      });
    });
  }

  function formFields(form) {
    return Array.prototype.slice.call(form.elements).map(function (el) {
      return { name: el.name, type: el.type, value: el.value, checked: el.checked, disabled: el.disabled };
    });
  }

  function initDirtyGuard(doc, win) {
    qsa(doc, 'form[data-ac-dirty]').forEach(function (form) {
      var initial = snapshotFields(formFields(form));
      var bar = doc.querySelector('[data-ac-dirty-bar]');
      var dirty = false;
      var submitting = false;

      var refresh = function () {
        dirty = isDirty(initial, snapshotFields(formFields(form)));
        if (bar) bar.hidden = !dirty;
        form.setAttribute('data-ac-is-dirty', dirty ? 'true' : 'false');
      };
      form.addEventListener('input', refresh);
      form.addEventListener('change', refresh);
      form.addEventListener('ac:changed', refresh);
      form.addEventListener('submit', function () { submitting = true; });
      form.addEventListener('reset', function () { win.setTimeout(refresh, 0); });

      qsa(doc, '[data-ac-dirty-cancel]').forEach(function (button) {
        button.addEventListener('click', function () { form.reset(); refresh(); });
      });

      win.addEventListener('beforeunload', function (event) {
        if (!dirty || submitting) return;
        event.preventDefault();
        event.returnValue = '';
      });

      qsa(doc, 'a[data-ac-tab-link]').forEach(function (link) {
        link.addEventListener('click', function (event) {
          if (dirty && !win.confirm('Bạn có thay đổi chưa lưu. Rời trang?')) event.preventDefault();
        });
      });
      refresh();
    });
  }

  /** What the small picture box shows when there is no image: the emoji, else the first letter of the term. */
  function previewInitial(term, emoji) {
    var e = typeof emoji === 'string' ? emoji.trim() : '';
    if (e) return e;
    var t = typeof term === 'string' ? term.trim() : '';
    return t ? Array.from(t)[0].toUpperCase() : '?';
  }

  /** Live preview of a form: [data-ac-bind="Field"] mirrors the field text, [data-ac-bind-pic] mirrors image/emoji. */
  function initPreviews(doc) {
    qsa(doc, '[data-ac-preview]').forEach(function (preview) {
      var form = doc.getElementById(preview.getAttribute('data-ac-preview-form') || '');
      if (!form) return;
      var field = function (name) { return form.elements[name] ? String(form.elements[name].value) : ''; };
      var refresh = function () {
        qsa(preview, '[data-ac-bind]').forEach(function (el) {
          var value = field(el.getAttribute('data-ac-bind')).trim();
          el.textContent = value || el.getAttribute('data-ac-empty') || '';
          el.classList.toggle('is-empty', !value);
        });
        qsa(preview, '[data-ac-bind-pic]').forEach(function (box) {
          var img = box.querySelector('[data-ac-pic-img]');
          var text = box.querySelector('[data-ac-pic-text]');
          var url = field('ImageUrl').trim();
          var showImage = !!url && isSafeImageUrl(url);
          if (img) {
            if (showImage) { img.src = url; }
            img.hidden = !showImage;
          }
          if (text) {
            text.hidden = showImage;
            text.textContent = previewInitial(field('Term'), field('Emoji'));
          }
        });
      };
      form.addEventListener('input', refresh);
      form.addEventListener('change', refresh);
      qsa(preview, '[data-ac-pic-img]').forEach(function (img) {
        img.addEventListener('error', function () {
          img.hidden = true;
          qsa(preview, '[data-ac-pic-text]').forEach(function (t) { t.hidden = false; });
        });
      });
      refresh();
    });
  }

  /** A broken table picture falls back to the initial given in data-ac-initial (text only). */
  function initImageFallbacks(doc) {
    var swap = function (img) {
      var holder = img.parentNode;
      if (!holder || holder.getAttribute('data-ac-swapped') === 'true') return;
      holder.setAttribute('data-ac-swapped', 'true');
      var span = doc.createElement('span');
      span.className = 'ac-word-initial';
      span.textContent = img.getAttribute('data-ac-initial') || '?';
      img.hidden = true;
      holder.appendChild(span);
    };
    qsa(doc, 'img[data-ac-fallback]').forEach(function (img) {
      img.addEventListener('error', function () { swap(img); });
      if (img.complete && img.naturalWidth === 0 && img.getAttribute('src')) swap(img);
    });
  }

  function init(doc) {
    var win = doc.defaultView || (typeof window !== 'undefined' ? window : null);
    if (!win) return;
    initCounters(doc);
    initAutoSubmit(doc);
    initPendingState(doc);
    initConfirmDelete(doc, win);
    initDirtyGuard(doc, win);
    initPreviews(doc);
    initImageFallbacks(doc);
  }

  return {
    parseYoutubeId: parseYoutubeId,
    youtubeThumbnailUrl: youtubeThumbnailUrl,
    isSafeImageUrl: isSafeImageUrl,
    normalizeDuration: normalizeDuration,
    isCefrLevel: isCefrLevel,
    counterState: counterState,
    counterText: counterText,
    confirmNameMatches: confirmNameMatches,
    reindexName: reindexName,
    reindexId: reindexId,
    moveItem: moveItem,
    isDirty: isDirty,
    snapshotFields: snapshotFields,
    previewInitial: previewInitial,
    init: init
  };
});
