(function () {
    'use strict';

    function setBusy(button, busy) {
        if (!button) return;
        button.classList.toggle('is-busy', busy);
        button.disabled = busy;
        button.setAttribute('aria-busy', busy ? 'true' : 'false');
    }

    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        if (!form.closest('.admin-reading-page')) return;
        if (form.dataset.skipBusy === 'true') return;

        prepareQuestionsForSubmit(form);

        var submitter = event.submitter;
        if (!(submitter instanceof HTMLButtonElement)) {
            submitter = form.querySelector('button[type="submit"]');
        }
        if (!(submitter instanceof HTMLButtonElement)) return;

        setBusy(submitter, true);
    });

    document.querySelectorAll('.admin-reading-page [data-reading-delete-target]').forEach(function (trigger) {
        trigger.addEventListener('click', function () {
            var modalId = trigger.getAttribute('data-reading-delete-target');
            var title = trigger.getAttribute('data-reading-title') || '';
            var modal = modalId ? document.getElementById(modalId) : null;
            if (!modal) return;
            var label = modal.querySelector('[data-reading-delete-title]');
            if (label) label.textContent = title;
        });
    });

    if (window.TctImageUpload && document.getElementById('readingImageUploadModal')) {
        window.TctImageUpload.bind({
            modalId: 'readingImageUploadModal',
            fileInput: '#ImageFile',
            urlInput: '[data-reading-image-url]',
            removeInput: '[data-reading-image-remove]',
            preview: '[data-reading-image-preview]',
            openSelector: '[data-tct-image-upload-open]',
            removeTrigger: '[data-tct-image-upload-remove]'
        });
    }

    var root = document.querySelector('[data-reading-questions-root]');
    if (!root || root.hasAttribute('data-readonly')) return;

    var list = root.querySelector('[data-reading-questions-list]');
    var template = document.getElementById('reading-question-template');

    function shapeForType(type) {
        if (type === 'GapFill' || type === 'ShortAnswer') return 'text';
        if (type === 'Matching') return 'matching';
        return 'choice';
    }

    function setBlockEnabled(block, enabled) {
        if (!block) return;
        block.classList.toggle('d-none', !enabled);
        block.querySelectorAll('input, select, textarea, button').forEach(function (el) {
            if (enabled) {
                el.removeAttribute('disabled');
                if (el.dataset.savedName) {
                    el.setAttribute('name', el.dataset.savedName);
                    delete el.dataset.savedName;
                }
            } else {
                if (el.name && !el.dataset.savedName) {
                    el.dataset.savedName = el.name;
                    el.removeAttribute('name');
                }
                if (el.tagName !== 'BUTTON') el.setAttribute('disabled', 'disabled');
            }
        });
    }

    function applyQuestionType(card) {
        var select = card.querySelector('[data-reading-q-type]');
        var type = select ? select.value : 'MultipleChoice';
        var shape = shapeForType(type);
        setBlockEnabled(card.querySelector('[data-reading-block="choice"]'), shape === 'choice');
        setBlockEnabled(card.querySelector('[data-reading-block="text"]'), shape === 'text');
        setBlockEnabled(card.querySelector('[data-reading-block="matching"]'), shape === 'matching');

        if (shape === 'choice') {
            var optionsHost = card.querySelector('[data-reading-options]');
            if (optionsHost && !optionsHost.querySelector('[data-reading-option]')) {
                addOption(card);
                addOption(card);
                var firstRadio = card.querySelector('[data-reading-correct-radio]');
                if (firstRadio) firstRadio.checked = true;
            }
            if (type === 'TrueFalse') {
                ensureTrueFalseOptions(card);
            }
        }
        if (shape === 'matching') {
            var pairs = card.querySelector('[data-reading-pairs]');
            if (pairs && !pairs.querySelector('[data-reading-pair]')) {
                addPair(card);
            }
        }
        syncCorrectFlags(card);
    }

    function ensureTrueFalseOptions(card) {
        var options = card.querySelector('[data-reading-options]');
        if (!options) return;
        var rows = options.querySelectorAll('[data-reading-option]');
        while (rows.length > 2) {
            rows[rows.length - 1].remove();
            rows = options.querySelectorAll('[data-reading-option]');
        }
        while (rows.length < 2) {
            addOption(card);
            rows = options.querySelectorAll('[data-reading-option]');
        }
        var texts = ['True', 'False'];
        rows.forEach(function (row, i) {
            var input = row.querySelector('input[name*=".OptionText"]');
            if (input && (!input.value || input.value === 'True' || input.value === 'False')) {
                input.value = texts[i] || '';
            }
        });
        reindexOptions(card);
    }

    function syncCorrectFlags(card) {
        var options = card.querySelectorAll('[data-reading-option]');
        var checked = card.querySelector('[data-reading-correct-radio]:checked');
        var checkedVal = checked ? checked.value : '0';
        options.forEach(function (row, i) {
            var hidden = row.querySelector('[data-reading-is-correct]');
            if (hidden) hidden.value = String(i) === String(checkedVal) ? 'true' : 'false';
            var radio = row.querySelector('[data-reading-correct-radio]');
            if (radio) radio.value = String(i);
        });
    }

    function reindexQuestions() {
        if (!list) return;
        list.querySelectorAll('[data-reading-question]').forEach(function (card, qi) {
            card.dataset.qIndex = String(qi);
            var label = card.querySelector('[data-reading-q-label]');
            if (label) label.textContent = String(qi + 1);
            var order = card.querySelector('[data-reading-order]');
            if (order) order.value = String(qi + 1);

            card.querySelectorAll('[name]').forEach(function (el) {
                el.name = el.name
                    .replace(/Questions\[\d+]/g, 'Questions[' + qi + ']')
                    .replace(/correct_q_\d+/g, 'correct_q_' + qi);
            });
            card.querySelectorAll('[data-saved-name]').forEach(function (el) {
                if (el.dataset.savedName) {
                    el.dataset.savedName = el.dataset.savedName
                        .replace(/Questions\[\d+]/g, 'Questions[' + qi + ']');
                }
            });

            reindexOptions(card);
            reindexPairs(card);
            applyQuestionType(card);
        });
    }

    function reindexOptions(card) {
        var qi = card.dataset.qIndex || '0';
        card.querySelectorAll('[data-reading-option]').forEach(function (row, oi) {
            row.querySelectorAll('[name]').forEach(function (el) {
                el.name = el.name.replace(/Options\[\d+]/g, 'Options[' + oi + ']');
            });
            var order = row.querySelector('input[name*=".OptionOrder"]');
            if (order) order.value = String(oi + 1);
            var radio = row.querySelector('[data-reading-correct-radio]');
            if (radio) {
                radio.name = 'correct_q_' + qi;
                radio.value = String(oi);
            }
        });
        syncCorrectFlags(card);
    }

    function reindexPairs(card) {
        var qi = card.dataset.qIndex || '0';
        var optionIndex = 0;
        card.querySelectorAll('[data-reading-pair]').forEach(function (pair, pi) {
            var key = pair.dataset.matchKey || ('p' + (pi + 1));
            pair.dataset.matchKey = key;
            var inputs = pair.querySelectorAll('input');
            // Left then Right: two option slots
            var sides = [
                { side: 'Left', offset: 0 },
                { side: 'Right', offset: 1 }
            ];
            sides.forEach(function (s) {
                var oi = optionIndex + s.offset;
                // remap by MatchSide hidden
            });
            var leftText = pair.querySelector('input[name*="OptionText"]');
            var allNamed = pair.querySelectorAll('input[name]');
            // Rebuild names for left (even) and right (odd) relative to pair
            var leftIdx = pi * 2;
            var rightIdx = pi * 2 + 1;
            var leftGroup = [];
            var rightGroup = [];
            allNamed.forEach(function (el) {
                if (el.name.indexOf('MatchSide') >= 0 && el.value === 'Right') rightGroup.push(el);
                else if (el.name.indexOf('MatchSide') >= 0 && el.value === 'Left') leftGroup.push(el);
            });
            // Simpler: query by MatchSide value siblings
            pair.querySelectorAll('input').forEach(function (el) {
                if (!el.name && !el.dataset.savedName) return;
                var sideEl = el.closest('.col-md-5, .col-md-2');
            });

            var cols = pair.querySelectorAll('.col-md-5');
            if (cols.length >= 2) {
                rewritePairColumn(cols[0], qi, leftIdx, key, 'Left');
                rewritePairColumn(cols[1], qi, rightIdx, key, 'Right');
            }
            optionIndex += 2;
        });
    }

    function rewritePairColumn(col, qi, oi, key, side) {
        col.querySelectorAll('input').forEach(function (el) {
            var base = el.name || el.dataset.savedName || '';
            if (!base) return;
            var field = 'OptionText';
            if (base.indexOf('MatchKey') >= 0) field = 'MatchKey';
            else if (base.indexOf('MatchSide') >= 0) field = 'MatchSide';
            else if (base.indexOf('OptionOrder') >= 0) field = 'OptionOrder';
            else if (base.indexOf('IsCorrect') >= 0) field = 'IsCorrect';
            else if (base.indexOf('OptionText') >= 0) field = 'OptionText';
            else if (base.indexOf('.Id') >= 0) field = 'Id';
            else return;
            var name = 'Questions[' + qi + '].Options[' + oi + '].' + field;
            if (el.name) el.name = name;
            if (el.dataset.savedName) el.dataset.savedName = name;
            if (field === 'MatchKey') el.value = key;
            if (field === 'MatchSide') el.value = side;
            if (field === 'OptionOrder') el.value = String(oi + 1);
            if (field === 'IsCorrect') el.value = 'false';
        });
    }

    function addOption(card) {
        var options = card.querySelector('[data-reading-options]');
        if (!options) return;
        var qi = card.dataset.qIndex || '0';
        var oi = options.querySelectorAll('[data-reading-option]').length;
        var row = document.createElement('div');
        row.className = 'admin-reading-option-row row g-2 align-items-center mb-2';
        row.setAttribute('data-reading-option', '');
        row.innerHTML =
            '<div class="col-auto">' +
            '<input class="form-check-input" type="radio" name="correct_q_' + qi + '" value="' + oi + '" data-reading-correct-radio title="Đáp án đúng" />' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + oi + '].IsCorrect" value="false" data-reading-is-correct />' +
            '</div>' +
            '<div class="col">' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + oi + '].OptionOrder" value="' + (oi + 1) + '" />' +
            '<input class="form-control" name="Questions[' + qi + '].Options[' + oi + '].OptionText" placeholder="Nội dung lựa chọn" />' +
            '</div>' +
            '<div class="col-auto">' +
            '<button type="button" class="btn btn-outline-danger btn-sm" data-reading-remove-option aria-label="Xóa lựa chọn"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button>' +
            '</div>';
        options.appendChild(row);
        reindexOptions(card);
    }

    function addPair(card) {
        var pairs = card.querySelector('[data-reading-pairs]');
        if (!pairs) return;
        var qi = card.dataset.qIndex || '0';
        var pi = pairs.querySelectorAll('[data-reading-pair]').length;
        var key = 'p' + (pi + 1);
        var leftIdx = pi * 2;
        var rightIdx = pi * 2 + 1;
        var row = document.createElement('div');
        row.className = 'admin-reading-pair-row row g-2 align-items-end mb-2';
        row.setAttribute('data-reading-pair', '');
        row.dataset.matchKey = key;
        row.innerHTML =
            '<div class="col-md-5">' +
            '<label class="form-label">Trái (Left)</label>' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + leftIdx + '].MatchKey" value="' + key + '" data-match-key-input />' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + leftIdx + '].MatchSide" value="Left" />' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + leftIdx + '].OptionOrder" value="' + (leftIdx + 1) + '" />' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + leftIdx + '].IsCorrect" value="false" />' +
            '<input class="form-control" name="Questions[' + qi + '].Options[' + leftIdx + '].OptionText" placeholder="Vế trái" />' +
            '</div>' +
            '<div class="col-md-5">' +
            '<label class="form-label">Phải (Right)</label>' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + rightIdx + '].MatchKey" value="' + key + '" data-match-key-input />' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + rightIdx + '].MatchSide" value="Right" />' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + rightIdx + '].OptionOrder" value="' + (rightIdx + 1) + '" />' +
            '<input type="hidden" name="Questions[' + qi + '].Options[' + rightIdx + '].IsCorrect" value="false" />' +
            '<input class="form-control" name="Questions[' + qi + '].Options[' + rightIdx + '].OptionText" placeholder="Vế phải" />' +
            '</div>' +
            '<div class="col-md-2">' +
            '<button type="button" class="btn btn-outline-danger btn-sm w-100" data-reading-remove-pair>Xóa</button>' +
            '</div>';
        pairs.appendChild(row);
        reindexPairs(card);
    }

    function prepareQuestionsForSubmit(form) {
        if (!form.querySelector('[data-reading-questions-root]')) return;
        form.querySelectorAll('[data-reading-question]').forEach(function (card) {
            syncCorrectFlags(card);
            applyQuestionType(card);
            // Strip empty matching option texts so binder gets clean pairs only
            var shape = shapeForType(card.querySelector('[data-reading-q-type]')?.value || '');
            if (shape === 'matching') {
                card.querySelectorAll('[data-reading-pair]').forEach(function (pair) {
                    var texts = pair.querySelectorAll('input[name*="OptionText"]');
                    var empty = Array.prototype.every.call(texts, function (t) { return !t.value.trim(); });
                    if (empty) {
                        pair.querySelectorAll('input').forEach(function (el) {
                            if (el.name) {
                                el.dataset.savedName = el.name;
                                el.removeAttribute('name');
                            }
                        });
                    }
                });
                reindexPairs(card);
            }
            if (shape === 'choice') {
                // drop blank options except keep at least structure
                card.querySelectorAll('[data-reading-option]').forEach(function (row) {
                    var text = row.querySelector('input[name*="OptionText"]');
                    if (text && !text.value.trim() && card.querySelectorAll('[data-reading-option]').length > 2) {
                        row.remove();
                    }
                });
                reindexOptions(card);
            }
        });
        reindexQuestions();
    }

    root.addEventListener('change', function (e) {
        var t = e.target;
        if (!(t instanceof HTMLElement)) return;
        if (t.matches('[data-reading-q-type]')) {
            applyQuestionType(t.closest('[data-reading-question]'));
        }
        if (t.matches('[data-reading-correct-radio]')) {
            syncCorrectFlags(t.closest('[data-reading-question]'));
        }
    });

    root.addEventListener('click', function (e) {
        var t = e.target;
        if (!(t instanceof HTMLElement)) return;
        var btn = t.closest('button');
        if (!btn) return;

        if (btn.matches('[data-reading-add-question]')) {
            if (!template || !list) return;
            var qi = list.querySelectorAll('[data-reading-question]').length;
            var html = template.innerHTML
                .replace(/__QI__/g, String(qi))
                .replace(/__LABEL__/g, String(qi + 1));
            var wrap = document.createElement('div');
            wrap.innerHTML = html.trim();
            var card = wrap.firstElementChild;
            list.appendChild(card);
            applyQuestionType(card);
            reindexQuestions();
            return;
        }

        if (btn.matches('[data-reading-remove-question]')) {
            var cardRm = btn.closest('[data-reading-question]');
            if (cardRm) cardRm.remove();
            reindexQuestions();
            return;
        }

        if (btn.matches('[data-reading-add-option]')) {
            addOption(btn.closest('[data-reading-question]'));
            return;
        }

        if (btn.matches('[data-reading-remove-option]')) {
            var optCard = btn.closest('[data-reading-question]');
            var row = btn.closest('[data-reading-option]');
            if (optCard && row && optCard.querySelectorAll('[data-reading-option]').length > 2) {
                row.remove();
                reindexOptions(optCard);
            }
            return;
        }

        if (btn.matches('[data-reading-add-pair]')) {
            addPair(btn.closest('[data-reading-question]'));
            return;
        }

        if (btn.matches('[data-reading-remove-pair]')) {
            var pairCard = btn.closest('[data-reading-question]');
            var pair = btn.closest('[data-reading-pair]');
            if (pairCard && pair) {
                pair.remove();
                if (!pairCard.querySelector('[data-reading-pair]')) addPair(pairCard);
                else reindexPairs(pairCard);
            }
        }
    });

    // Initial apply so inactive blocks do not post
    list.querySelectorAll('[data-reading-question]').forEach(applyQuestionType);
})();
