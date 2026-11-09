/* Card detail fields (examples, image, info, note) shared by the composer drawer and the edit-card page.
   Mount on an element rendered by Views/Set/_CardDetailFields.cshtml. */
(function () {
    'use strict';

    var T = window.TctSets;
    if (!T) { return; }

    var LIMITS = { examples: 6, synonyms: 10, collocations: 8, wordFamily: 8, tag: 80 };

    function emptyData() {
        return {
            phonetic: '', phoneticUk: '', meaningEn: '', partsOfSpeech: [], topic: '', emoji: '', cefr: '', personalNote: '',
            imageUrl: '', imageSource: null, examples: [], synonyms: [], antonyms: [], collocations: [], wordFamily: [],
            verbForms: { past: '', pastParticiple: '', ing: '', thirdPerson: '' }, plural: '', aiGenerated: false
        };
    }

    function clone(value) { return JSON.parse(JSON.stringify(value)); }

    function mount(root, options) {
        options = options || {};
        var urls = options.urls || {};
        var named = options.named === true; // edit page: give inputs form names for model binding
        var data = emptyData();
        var baseline = JSON.stringify(data);
        var changeHandlers = [];

        function q(selector) { return root.querySelector(selector); }
        function qa(selector) { return Array.prototype.slice.call(root.querySelectorAll(selector)); }

        var refs = {
            examples: q('[data-ts-examples]'), addExample: q('[data-ts-add-example]'),
            imageCurrent: q('[data-ts-image-current]'), imageUpload: q('[data-ts-image-upload]'),
            imageRemove: q('[data-ts-image-remove]'), imageError: q('[data-ts-image-error]'),
            imageSearch: q('[data-ts-image-search]'), imageQuery: q('[data-ts-image-query]'),
            imageResults: q('[data-ts-image-results]'), imageLocked: q('[data-ts-image-locked]'),
            posBox: q('[data-ts-pos]'), topic: q('[data-ts-topic]'), emoji: q('[data-ts-emoji]'), cefr: q('[data-ts-cefr]'),
            phonetic: q('[data-ts-ipa-us]'), phoneticUk: q('[data-ts-ipa-uk]'), meaningEn: q('[data-ts-meaning-en]'),
            collocations: q('[data-ts-collocations]'), addCollocation: q('[data-ts-add-collocation]'),
            synonyms: q('[data-ts-tags="synonyms"]'), antonyms: q('[data-ts-tags="antonyms"]'),
            wordFamily: q('[data-ts-wordfamily]'), addWordFamily: q('[data-ts-add-wordfamily]'),
            verbBox: q('[data-ts-verbforms]'), verbPast: q('[data-ts-verb-past]'), verbPp: q('[data-ts-verb-pp]'),
            verbIng: q('[data-ts-verb-ing]'), verbThird: q('[data-ts-verb-third]'),
            pluralBox: q('[data-ts-plural-box]'), plural: q('[data-ts-plural]'),
            note: q('[data-ts-note]'), noteCounter: q('[data-ts-note-counter]'),
            imageUrlField: q('[data-ts-image-url-field]'), aiField: q('[data-ts-ai-field]')
        };

        function emit() {
            updateTabLabels();
            changeHandlers.forEach(function (handler) { handler(); });
        }

        // ------------------------------------------------------------- examples

        function renderExamples() {
            refs.examples.textContent = '';
            data.examples.forEach(function (example, index) {
                var row = T.el('div', 'ts-example-row');
                var fields = T.el('div', 'ts-example-fields');

                var en = document.createElement('textarea');
                en.className = 'form-control';
                en.rows = 2;
                en.maxLength = 300;
                en.lang = 'en';
                en.value = example.text || '';
                en.required = true;
                en.setAttribute('aria-label', 'Câu ví dụ tiếng Anh ' + (index + 1));
                en.placeholder = 'Câu ví dụ tiếng Anh';
                if (named) { en.name = 'Examples[' + index + '].Text'; }
                en.addEventListener('input', function () { example.text = en.value; emit(); });

                var vi = document.createElement('input');
                vi.type = 'text';
                vi.className = 'form-control';
                vi.maxLength = 300;
                vi.value = example.vi || '';
                vi.setAttribute('aria-label', 'Bản dịch tiếng Việt ' + (index + 1));
                vi.placeholder = 'Bản dịch tiếng Việt';
                if (named) { vi.name = 'Examples[' + index + '].Vi'; }
                vi.addEventListener('input', function () { example.vi = vi.value; emit(); });

                fields.appendChild(en);
                fields.appendChild(vi);
                row.appendChild(fields);

                var actions = T.el('div', 'ts-row-actions');
                actions.appendChild(iconButton('fa-solid fa-arrow-up', 'Đưa lên', function () { move(data.examples, index, -1); renderExamples(); emit(); }, index === 0));
                actions.appendChild(iconButton('fa-solid fa-arrow-down', 'Đưa xuống', function () { move(data.examples, index, 1); renderExamples(); emit(); }, index === data.examples.length - 1));
                actions.appendChild(iconButton('fa-regular fa-trash-can', 'Xóa ví dụ ' + (index + 1), function () { data.examples.splice(index, 1); renderExamples(); emit(); }));
                row.appendChild(actions);
                refs.examples.appendChild(row);
            });
            refs.addExample.disabled = data.examples.length >= LIMITS.examples;
        }

        function iconButton(iconClass, label, handler, disabled) {
            var button = T.el('button', 'btn btn-sm tct-btn-icon');
            button.type = 'button';
            button.setAttribute('aria-label', label);
            button.appendChild(T.icon(iconClass));
            button.disabled = !!disabled;
            button.addEventListener('click', handler);
            return button;
        }

        function move(list, index, delta) {
            var target = index + delta;
            if (target < 0 || target >= list.length) { return; }
            var item = list.splice(index, 1)[0];
            list.splice(target, 0, item);
        }

        refs.addExample.addEventListener('click', function () {
            if (data.examples.length >= LIMITS.examples) { return; }
            data.examples.push({ text: '', vi: '' });
            renderExamples();
            var rows = refs.examples.querySelectorAll('textarea');
            if (rows.length) { rows[rows.length - 1].focus(); }
            emit();
        });

        // ------------------------------------------------------------- generic pair rows (collocations / word family)

        function renderCollocations() {
            refs.collocations.textContent = '';
            data.collocations.forEach(function (item, index) {
                var row = T.el('div', 'ts-pair-row');
                var text = field('text', item.text, 120, 'Cụm từ', 'Cụm từ ' + (index + 1), named ? 'Collocations[' + index + '].Text' : null, function (v) { item.text = v; });
                var vi = field('text', item.vi, 200, 'Nghĩa', 'Nghĩa cụm từ ' + (index + 1), named ? 'Collocations[' + index + '].Vi' : null, function (v) { item.vi = v; });
                row.appendChild(text); row.appendChild(vi);
                row.appendChild(iconButton('fa-regular fa-trash-can', 'Xóa cụm từ ' + (index + 1), function () { data.collocations.splice(index, 1); renderCollocations(); emit(); }));
                refs.collocations.appendChild(row);
            });
            refs.addCollocation.disabled = data.collocations.length >= LIMITS.collocations;
        }

        refs.addCollocation.addEventListener('click', function () {
            if (data.collocations.length >= LIMITS.collocations) { return; }
            data.collocations.push({ text: '', vi: '' });
            renderCollocations();
            emit();
        });

        function renderWordFamily() {
            refs.wordFamily.textContent = '';
            data.wordFamily.forEach(function (item, index) {
                var row = T.el('div', 'ts-pair-row ts-pair-row--three');
                row.appendChild(field('text', item.text, 80, 'Từ', 'Từ họ hàng ' + (index + 1), named ? 'WordFamily[' + index + '].Text' : null, function (v) { item.text = v; }));
                var select = document.createElement('select');
                select.className = 'form-select';
                select.setAttribute('aria-label', 'Loại từ ' + (index + 1));
                if (named) { select.name = 'WordFamily[' + index + '].Pos'; }
                var none = document.createElement('option'); none.value = ''; none.textContent = 'Loại từ'; select.appendChild(none);
                Object.keys(T.posLabels).forEach(function (code) {
                    var option = document.createElement('option'); option.value = code; option.textContent = T.posLabels[code]; select.appendChild(option);
                });
                select.value = item.pos || '';
                select.addEventListener('change', function () { item.pos = select.value; emit(); });
                row.appendChild(select);
                row.appendChild(field('text', item.vi, 200, 'Nghĩa', 'Nghĩa từ họ hàng ' + (index + 1), named ? 'WordFamily[' + index + '].Vi' : null, function (v) { item.vi = v; }));
                row.appendChild(iconButton('fa-regular fa-trash-can', 'Xóa từ họ hàng ' + (index + 1), function () { data.wordFamily.splice(index, 1); renderWordFamily(); emit(); }));
                refs.wordFamily.appendChild(row);
            });
            refs.addWordFamily.disabled = data.wordFamily.length >= LIMITS.wordFamily;
        }

        refs.addWordFamily.addEventListener('click', function () {
            if (data.wordFamily.length >= LIMITS.wordFamily) { return; }
            data.wordFamily.push({ text: '', pos: '', vi: '' });
            renderWordFamily();
            emit();
        });

        function field(type, value, max, placeholder, label, name, onInput) {
            var input = document.createElement('input');
            input.type = type;
            input.className = 'form-control';
            input.maxLength = max;
            input.value = value || '';
            input.placeholder = placeholder;
            input.setAttribute('aria-label', label);
            if (name) { input.name = name; }
            input.addEventListener('input', function () { onInput(input.value); emit(); });
            return input;
        }

        // ------------------------------------------------------------- tag inputs (synonyms / antonyms)

        function mountTags(box, key, labelText) {
            var list = box.querySelector('[data-ts-tag-list]');
            var input = box.querySelector('input[type="text"]');
            var hidden = box.querySelector('[data-ts-tag-hidden]');

            function render() {
                list.textContent = '';
                if (hidden) { hidden.textContent = ''; }
                data[key].forEach(function (tag, index) {
                    var chip = T.el('span', 'ts-tag');
                    chip.appendChild(T.el('span', 'ts-tag-text tct-break', tag));
                    var remove = T.el('button', 'ts-tag-remove');
                    remove.type = 'button';
                    remove.setAttribute('aria-label', 'Xóa ' + tag);
                    remove.appendChild(T.icon('fa-solid fa-xmark'));
                    remove.addEventListener('click', function () { data[key].splice(index, 1); render(); emit(); input.focus(); });
                    chip.appendChild(remove);
                    list.appendChild(chip);
                    if (named && hidden) {
                        var h = document.createElement('input');
                        h.type = 'hidden';
                        h.name = key === 'synonyms' ? 'Synonyms' : 'Antonyms';
                        h.value = tag;
                        hidden.appendChild(h);
                    }
                });
            }

            function add(raw) {
                var value = raw.trim();
                if (!value || data[key].length >= LIMITS.synonyms) { return; }
                if (data[key].some(function (t) { return t.toLowerCase() === value.toLowerCase(); })) { return; }
                data[key].push(value.slice(0, LIMITS.tag));
                render();
                emit();
            }

            input.addEventListener('keydown', function (event) {
                if (event.key === 'Enter' || event.key === ',') {
                    event.preventDefault();
                    add(input.value);
                    input.value = '';
                } else if (event.key === 'Backspace' && input.value === '' && data[key].length) {
                    data[key].pop(); render(); emit();
                }
            });
            input.addEventListener('blur', function () { if (input.value.trim()) { add(input.value); input.value = ''; } });
            return { render: render };
        }

        var synonymTags = mountTags(refs.synonyms, 'synonyms');
        var antonymTags = mountTags(refs.antonyms, 'antonyms');

        // ------------------------------------------------------------- simple fields

        function bind(el, key, transform) {
            if (!el) { return; }
            var handler = function () { data[key] = transform ? transform(el.value) : el.value; emit(); };
            el.addEventListener('input', handler);
            el.addEventListener('change', handler);
        }

        bind(refs.phonetic, 'phonetic');
        bind(refs.phoneticUk, 'phoneticUk');
        bind(refs.meaningEn, 'meaningEn');
        bind(refs.topic, 'topic');
        bind(refs.emoji, 'emoji');
        bind(refs.cefr, 'cefr');
        bind(refs.plural, 'plural');
        bind(refs.note, 'personalNote');
        refs.note.addEventListener('input', function () { refs.noteCounter.textContent = refs.note.value.length + '/1000'; });

        [['past', refs.verbPast], ['pastParticiple', refs.verbPp], ['ing', refs.verbIng], ['thirdPerson', refs.verbThird]].forEach(function (pair) {
            pair[1].addEventListener('input', function () { data.verbForms[pair[0]] = pair[1].value; emit(); });
        });

        function renderPos() {
            refs.posBox.querySelectorAll('input[type="checkbox"]').forEach(function (box) {
                box.checked = data.partsOfSpeech.indexOf(box.value) !== -1;
            });
            syncConditionalBoxes();
        }

        refs.posBox.addEventListener('change', function (event) {
            var box = event.target;
            if (!(box instanceof HTMLInputElement)) { return; }
            var list = data.partsOfSpeech;
            var at = list.indexOf(box.value);
            if (box.checked && at === -1) {
                if (list.length >= 6) { box.checked = false; T.toast('Chọn tối đa 6 loại từ.', { type: 'danger' }); return; }
                list.push(box.value);
            } else if (!box.checked && at !== -1) {
                list.splice(at, 1);
            }
            syncConditionalBoxes();
            emit();
        });

        function hasVerbForms() {
            var v = data.verbForms;
            return !!(v.past || v.pastParticiple || v.ing || v.thirdPerson);
        }

        function syncConditionalBoxes() {
            refs.verbBox.hidden = !(data.partsOfSpeech.indexOf('verb') !== -1 || hasVerbForms());
            refs.pluralBox.hidden = !(data.partsOfSpeech.indexOf('noun') !== -1 || !!data.plural);
        }

        // ------------------------------------------------------------- image

        function renderImage() {
            refs.imageCurrent.textContent = '';
            if (data.imageUrl) {
                var img = document.createElement('img');
                img.src = data.imageUrl;
                img.alt = '';
                img.className = 'ts-image-preview';
                refs.imageCurrent.appendChild(img);
                var source = data.imageSource;
                if (source && source.provider === 'pixabay') {
                    var credit = T.el('p', 'ts-image-credit');
                    var text = 'Ảnh: ' + (source.author || 'Pixabay') + ' / ';
                    credit.appendChild(document.createTextNode(text));
                    var link = T.el('a', null, 'Pixabay');
                    link.href = source.pageUrl && /^https:\/\//.test(source.pageUrl) ? source.pageUrl : 'https://pixabay.com';
                    link.target = '_blank';
                    link.rel = 'noopener';
                    credit.appendChild(link);
                    refs.imageCurrent.appendChild(credit);
                }
            }
            refs.imageRemove.hidden = !data.imageUrl;
            if (refs.imageUrlField) { refs.imageUrlField.value = data.imageUrl || ''; }
            syncImageSourceFields();
        }

        function syncImageSourceFields() {
            if (!named) { return; }
            var box = q('[data-ts-image-source-fields]');
            if (!box) { return; }
            box.textContent = '';
            var s = data.imageSource;
            if (!s) { return; }
            [['Provider', s.provider], ['PageUrl', s.pageUrl], ['Author', s.author]].forEach(function (pair) {
                if (!pair[1]) { return; }
                var h = document.createElement('input');
                h.type = 'hidden'; h.name = 'ImageSource.' + pair[0]; h.value = pair[1];
                box.appendChild(h);
            });
        }

        function imageError(message) {
            refs.imageError.textContent = message || '';
            refs.imageError.hidden = !message;
        }

        refs.imageUpload.addEventListener('change', function () {
            var file = refs.imageUpload.files && refs.imageUpload.files[0];
            if (!file) { return; }
            imageError('');
            if (file.size > 5 * 1024 * 1024) { imageError('Ảnh quá lớn (tối đa 5 MB).'); refs.imageUpload.value = ''; return; }
            var form = new FormData();
            form.append('file', file);
            refs.imageUpload.disabled = true;
            T.fetchJson(urls.cardImage, { method: 'POST', body: form }).then(function (result) {
                data.imageUrl = result.url;
                data.imageSource = { provider: 'upload' };
                renderImage();
                emit();
            }).catch(function (error) {
                imageError(error.message);
                T.toast(error.message, { type: 'danger' });
            }).then(function () { refs.imageUpload.disabled = false; refs.imageUpload.value = ''; });
        });

        refs.imageRemove.addEventListener('click', function () {
            data.imageUrl = '';
            data.imageSource = null;
            renderImage();
            emit();
        });

        function searchImages(query) {
            var term = (query || '').trim();
            if (!term || !options.isPremium) { return; }
            refs.imageResults.textContent = '';
            imageError('');
            refs.imageSearch.disabled = true;
            T.fetchJson(urls.images + '?q=' + encodeURIComponent(term)).then(function (hits) {
                if (!hits || hits.length === 0) { imageError('Không tìm thấy ảnh phù hợp.'); return; }
                hits.slice(0, 8).forEach(function (hit) {
                    var button = T.el('button', 'ts-image-choice');
                    button.type = 'button';
                    button.setAttribute('aria-pressed', String(data.imageUrl === hit.webformatUrl));
                    button.setAttribute('aria-label', 'Chọn ảnh của ' + (hit.author || 'Pixabay'));
                    var img = document.createElement('img');
                    img.src = hit.previewUrl; img.alt = ''; img.loading = 'lazy';
                    button.appendChild(img);
                    button.addEventListener('click', function () {
                        data.imageUrl = hit.webformatUrl;
                        data.imageSource = { provider: 'pixabay', pageUrl: hit.pageUrl || '', author: hit.author || '' };
                        refs.imageResults.querySelectorAll('button').forEach(function (b) { b.setAttribute('aria-pressed', 'false'); });
                        button.setAttribute('aria-pressed', 'true');
                        renderImage();
                        emit();
                    });
                    refs.imageResults.appendChild(button);
                });
            }).catch(function (error) { imageError(error.message); })
              .then(function () { refs.imageSearch.disabled = false; });
        }

        if (refs.imageSearch) {
            refs.imageSearch.addEventListener('click', function () { searchImages(refs.imageQuery.value); });
            refs.imageQuery.addEventListener('keydown', function (event) {
                if (event.key === 'Enter') { event.preventDefault(); searchImages(refs.imageQuery.value); }
            });
        }

        // ------------------------------------------------------------- tab labels

        function updateTabLabels() {
            var map = {
                examples: 'Ví dụ' + (data.examples.length ? ' ' + data.examples.length : ''),
                image: 'Ảnh', info: 'Thông tin', note: 'Ghi chú'
            };
            var has = {
                image: !!data.imageUrl,
                info: !!(data.partsOfSpeech.length || data.topic || data.emoji || data.cefr || data.phonetic || data.phoneticUk || data.meaningEn ||
                    data.synonyms.length || data.antonyms.length || data.collocations.length || data.wordFamily.length || data.plural || hasVerbForms()),
                note: !!data.personalNote
            };
            qa('[data-ts-tab-label]').forEach(function (label) {
                var key = label.getAttribute('data-ts-tab-label');
                label.textContent = map[key];
                var flag = label.parentNode.querySelector('.ts-tab-flag');
                if (flag) { flag.remove(); }
                if (has[key]) {
                    var mark = T.el('span', 'ts-tab-flag');
                    mark.appendChild(T.el('span', null, ' ✓')).setAttribute('aria-hidden', 'true');
                    mark.appendChild(T.el('span', 'visually-hidden', ' đã có'));
                    label.parentNode.appendChild(mark);
                }
            });
        }

        // ------------------------------------------------------------- public API

        function setData(next) {
            var d = emptyData();
            next = next || {};
            Object.keys(d).forEach(function (key) {
                if (next[key] !== undefined && next[key] !== null) { d[key] = clone(next[key]); }
            });
            d.verbForms = Object.assign({ past: '', pastParticiple: '', ing: '', thirdPerson: '' }, d.verbForms || {});
            data = d;

            refs.phonetic.value = d.phonetic || '';
            refs.phoneticUk.value = d.phoneticUk || '';
            refs.meaningEn.value = d.meaningEn || '';
            refs.topic.value = d.topic || '';
            refs.emoji.value = d.emoji || '';
            refs.cefr.value = d.cefr || '';
            refs.plural.value = d.plural || '';
            refs.note.value = d.personalNote || '';
            refs.noteCounter.textContent = (d.personalNote || '').length + '/1000';
            refs.verbPast.value = d.verbForms.past || '';
            refs.verbPp.value = d.verbForms.pastParticiple || '';
            refs.verbIng.value = d.verbForms.ing || '';
            refs.verbThird.value = d.verbForms.thirdPerson || '';
            if (refs.aiField) { refs.aiField.value = d.aiGenerated ? 'true' : 'false'; }
            renderExamples(); renderCollocations(); renderWordFamily(); synonymTags.render(); antonymTags.render(); renderPos(); renderImage();
            imageError('');
            refs.imageResults.textContent = '';
            if (refs.imageQuery) { refs.imageQuery.value = ''; }
            updateTabLabels();
            baseline = JSON.stringify(data);
        }

        function getData() {
            var d = clone(data);
            d.examples = d.examples.filter(function (e) { return (e.text || '').trim() !== ''; });
            d.collocations = d.collocations.filter(function (c) { return (c.text || '').trim() !== ''; });
            d.wordFamily = d.wordFamily.filter(function (w) { return (w.text || '').trim() !== ''; });
            return d;
        }

        function showTab(name) {
            var trigger = root.querySelector('[data-ts-tab="' + name + '"]');
            if (trigger && window.bootstrap) { window.bootstrap.Tab.getOrCreateInstance(trigger).show(); }
        }

        return {
            setData: setData,
            getData: getData,
            isDirty: function () { return JSON.stringify(data) !== baseline; },
            markClean: function () { baseline = JSON.stringify(data); },
            onChange: function (handler) { changeHandlers.push(handler); },
            showTab: showTab,
            focusFirstInvalid: function () {
                var invalid = root.querySelector(':invalid');
                if (!invalid) { return false; }
                var pane = invalid.closest('.tab-pane');
                if (pane && window.bootstrap) {
                    var trigger = root.querySelector('[data-bs-target="#' + pane.id + '"]');
                    if (trigger) { window.bootstrap.Tab.getOrCreateInstance(trigger).show(); }
                }
                invalid.focus();
                return true;
            },
            reindex: function () { renderExamples(); renderCollocations(); renderWordFamily(); }
        };
    }

    T.cardForm = { mount: mount, emptyData: emptyData };
})();
