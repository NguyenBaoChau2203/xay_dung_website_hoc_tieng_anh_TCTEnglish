/**
 * TCT English - Listening lesson editor (admin).
 *
 * Add / move / remove rows in the transcript, quiz and vocabulary tabs, keeping the posted field names
 * contiguous (lines[0], lines[1]...), the unsaved-changes bar, the YouTube preview and the "replace the
 * existing transcript?" confirmation of the import. Needs admin-content.js (pure helpers) and SweetAlert2.
 * Rows are cloned from a <template>: no HTML string is ever built from data.
 */
(function () {
  'use strict';

  var AC = window.AdminContent;
  if (!AC) return;

  var PLACEHOLDER = '__i__';

  function qsa(root, selector) { return Array.prototype.slice.call(root.querySelectorAll(selector)); }

  function rowsOf(container) {
    return Array.prototype.slice.call(container.children).filter(function (el) { return el.hasAttribute('data-ac-row'); });
  }

  function notifyChanged(form) {
    if (form) form.dispatchEvent(new Event('ac:changed'));
  }

  /** Renumbers every row: field names, ids, label targets, aria references, the visible number and action labels. */
  function reindex(container) {
    var rows = rowsOf(container);
    var radios = qsa(container, 'input[type="radio"]').map(function (r) { return [r, r.checked]; });

    rows.forEach(function (row, index) {
      qsa(row, '[name]').forEach(function (el) { el.setAttribute('name', AC.reindexName(el.getAttribute('name'), index)); });
      qsa(row, '[id]').forEach(function (el) { el.id = AC.reindexId(el.id, index); });
      qsa(row, 'label[for]').forEach(function (el) { el.setAttribute('for', AC.reindexId(el.getAttribute('for'), index)); });
      qsa(row, '[aria-describedby]').forEach(function (el) {
        el.setAttribute('aria-describedby', el.getAttribute('aria-describedby').split(/\s+/).map(function (id) { return AC.reindexId(id, index); }).join(' '));
      });
      qsa(row, '[data-ac-row-number]').forEach(function (el) {
        el.textContent = (el.getAttribute('data-ac-number-format') || '{n}').replace('{n}', String(index + 1));
      });
      qsa(row, '[data-ac-aria-template]').forEach(function (el) {
        el.setAttribute('aria-label', el.getAttribute('data-ac-aria-template').replace('{n}', String(index + 1)));
      });
    });

    // Renaming can briefly merge two radio groups; put every selection back.
    radios.forEach(function (pair) { pair[0].checked = pair[1]; });
  }

  function buildRow(template, index) {
    var fragment = template.content.cloneNode(true);
    var row = fragment.firstElementChild;
    var all = [row].concat(qsa(row, '*'));
    all.forEach(function (el) {
      Array.prototype.slice.call(el.attributes).forEach(function (attr) {
        if (attr.value.indexOf(PLACEHOLDER) >= 0) attr.value = attr.value.split(PLACEHOLDER).join(String(index));
      });
    });
    return row;
  }

  function firstField(row) {
    return row.querySelector('input:not([type="radio"]):not([type="hidden"]), textarea, select');
  }

  function initRepeater(container) {
    var scope = container.closest('fieldset') || container.parentNode;
    var form = container.closest('form');
    var template = scope.querySelector('template[data-ac-row-template]');
    var addButton = scope.querySelector('[data-ac-add-row]');
    var empty = scope.querySelector('[data-ac-empty-rows]');
    if (!template) return;

    function refresh() {
      reindex(container);
      if (empty) empty.hidden = rowsOf(container).length > 0;
      notifyChanged(form);
    }

    if (addButton) {
      addButton.addEventListener('click', function () {
        var row = buildRow(template, rowsOf(container).length);
        container.appendChild(row);
        refresh();
        var field = firstField(row);
        if (field) field.focus();
        if (row.scrollIntoView) row.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      });
    }

    container.addEventListener('click', function (event) {
      var button = event.target.closest('button');
      if (!button || !container.contains(button)) return;
      var row = button.closest('[data-ac-row]');
      if (!row) return;

      if (button.hasAttribute('data-ac-remove-row')) {
        var neighbour = row.nextElementSibling || row.previousElementSibling;
        container.removeChild(row);
        refresh();
        var target = neighbour && neighbour.hasAttribute('data-ac-row') ? firstField(neighbour) : addButton;
        if (target) target.focus();
        return;
      }

      var moveUp = button.hasAttribute('data-ac-move-up');
      var moveDown = button.hasAttribute('data-ac-move-down');
      if (!moveUp && !moveDown) return;
      var rows = rowsOf(container);
      var from = rows.indexOf(row);
      var to = AC.moveItem(rows.map(function (_, i) { return i; }), from, moveUp ? from - 1 : from + 1);
      if (to.join() === rows.map(function (_, i) { return i; }).join()) return;
      var order = to.map(function (i) { return rows[i]; });
      order.forEach(function (el) { container.appendChild(el); });
      refresh();
      button.focus();
    });

    // A vocabulary image preview follows the typed link, but only for a safe one.
    container.addEventListener('input', function (event) {
      var input = event.target;
      if (!input || !input.name || !/\.ImageUrl$/.test(input.name)) return;
      var row = input.closest('[data-ac-row]');
      var img = row ? row.querySelector('[data-ac-vocab-img]') : null;
      if (!img) return;
      var url = input.value.trim();
      var show = !!url && AC.isSafeImageUrl(url);
      if (show) img.src = url;
      img.hidden = !show;
    });

    reindex(container);
  }

  function initYoutubePreview(doc) {
    qsa(doc, '[data-ac-youtube-input]').forEach(function (input) {
      var holder = input.closest('.ac-youtube');
      if (!holder) return;
      var img = holder.querySelector('[data-ac-youtube-img]');
      var icon = holder.querySelector('[data-ac-youtube-icon]');
      var hint = holder.querySelector('[data-ac-youtube-hint]');
      var idleHint = hint ? hint.textContent : '';
      var update = function () {
        var raw = input.value.trim();
        var id = AC.parseYoutubeId(raw);
        if (img) {
          if (id) img.src = AC.youtubeThumbnailUrl(id);
          img.hidden = !id;
        }
        if (icon) icon.hidden = !!id;
        if (hint) {
          hint.textContent = id ? 'Mã video hợp lệ: ' + id : (raw ? 'Chưa nhận ra mã hoặc liên kết YouTube này.' : idleHint);
        }
      };
      input.addEventListener('input', update);
      if (img) {
        img.addEventListener('error', function () {
          img.hidden = true;
          if (icon) icon.hidden = false;
        });
      }
    });
  }

  function initImportConfirm(doc) {
    qsa(doc, 'form[data-ac-import-replace]').forEach(function (form) {
      var existing = parseInt(form.getAttribute('data-ac-import-replace'), 10) || 0;
      var flag = form.querySelector('[data-ac-import-flag]');
      var button = form.querySelector('button[type="submit"]');

      var send = function () {
        if (flag) flag.value = existing > 0 ? 'true' : 'false';
        form.setAttribute('data-ac-submitting', 'true');
        if (button) {
          button.setAttribute('aria-busy', 'true');
          button.disabled = true;
        }
        form.submit();
      };

      // Capture phase: decide before the generic pending-state handler marks the form as sent.
      form.addEventListener('submit', function (event) {
        if (form.getAttribute('data-ac-submitting') === 'true') {
          event.preventDefault();
          return;
        }
        event.preventDefault();
        if (existing <= 0) {
          send();
          return;
        }
        var message = 'Thay ' + existing + ' dòng hiện có bằng lời thoại từ YouTube?';
        if (window.Swal) {
          window.Swal.fire({
            title: 'Nhập lời thoại từ YouTube',
            text: message,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Thay thế',
            cancelButtonText: 'Hủy',
            returnFocus: true
          }).then(function (result) { if (result && result.isConfirmed) send(); });
        } else if (window.confirm(message)) {
          send();
        }
      }, true);
    });
  }

  function init(doc) {
    qsa(doc, '[data-ac-rows]').forEach(initRepeater);
    initYoutubePreview(doc);
    initImportConfirm(doc);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { init(document); });
  } else {
    init(document);
  }
})();
