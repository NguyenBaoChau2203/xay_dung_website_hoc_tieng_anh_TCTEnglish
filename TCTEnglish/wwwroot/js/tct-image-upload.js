/**
 * TCT Image Upload modal — stages file/URL into a parent form (no AJAX upload).
 * See docs/design-system/tct-image-upload.md
 */
(function (global) {
    "use strict";

    var ACCEPT = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    var MAX_BYTES = 5 * 1024 * 1024;

    function $(sel, root) {
        return (root || document).querySelector(sel);
    }

    function formatSize(bytes) {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KB";
        return (bytes / (1024 * 1024)).toFixed(1) + " MB";
    }

    function isAllowedFile(file) {
        if (!file) return false;
        var ext = "." + (file.name.split(".").pop() || "").toLowerCase();
        return ACCEPT.indexOf(ext) !== -1 && file.size > 0 && file.size <= MAX_BYTES;
    }

    function isAllowedUrl(value) {
        if (!value) return false;
        var v = value.trim();
        return (
            /^\/images\//i.test(v) ||
            /^\/uploads\//i.test(v) ||
            /^https?:\/\//i.test(v)
        );
    }

    function assignFile(input, file) {
        if (!input || !file) return;
        var dt = new DataTransfer();
        dt.items.add(file);
        input.files = dt.files;
    }

    function clearFile(input) {
        if (!input) return;
        input.value = "";
    }

    function createController(options) {
        var modalEl = document.getElementById(options.modalId);
        if (!modalEl || typeof bootstrap === "undefined" || !bootstrap.Modal) {
            return null;
        }

        var fileInput = $(options.fileInput);
        var urlInput = $(options.urlInput);
        var removeInput = $(options.removeInput);
        var pagePreview = $(options.preview);
        var dropzone = $("[data-tct-upload-dropzone]", modalEl);
        var picker = $("[data-tct-upload-picker]", modalEl);
        var fileRow = $("[data-tct-upload-file]", modalEl);
        var thumb = $("[data-tct-upload-thumb]", modalEl);
        var nameEl = $("[data-tct-upload-name]", modalEl);
        var sizeEl = $("[data-tct-upload-size]", modalEl);
        var bar = $("[data-tct-upload-bar]", modalEl);
        var pctEl = $("[data-tct-upload-pct]", modalEl);
        var doneEl = $("[data-tct-upload-done]", modalEl);
        var clearBtn = $("[data-tct-upload-clear]", modalEl);
        var urlField = $("[data-tct-upload-url]", modalEl);
        var urlBtn = $("[data-tct-upload-url-btn]", modalEl);
        var importBtn = $("[data-tct-upload-import]", modalEl);
        var cancelBtn = $("[data-tct-upload-cancel]", modalEl);
        var statusEl = $("[data-tct-upload-status]", modalEl);
        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        var lastTrigger = null;
        var objectUrl = null;
        var progressTimer = null;
        var stagedMode = null; // file | url

        function setStatus(text) {
            if (statusEl) statusEl.textContent = text || "";
        }

        function revokeObjectUrl() {
            if (objectUrl) {
                URL.revokeObjectURL(objectUrl);
                objectUrl = null;
            }
        }

        function setProgress(value, complete) {
            var pct = Math.max(0, Math.min(100, value | 0));
            if (bar) {
                bar.style.width = pct + "%";
                var track = bar.parentElement;
                if (track) track.setAttribute("aria-valuenow", String(pct));
            }
            if (pctEl) {
                pctEl.textContent = pct + "%";
                pctEl.classList.toggle("d-none", !!complete);
            }
            if (doneEl) doneEl.classList.toggle("d-none", !complete);
            if (clearBtn) clearBtn.classList.toggle("d-none", !!complete);
            if (fileRow) {
                fileRow.classList.toggle("is-uploading", !complete && pct < 100);
                fileRow.classList.toggle("is-complete", !!complete);
            }
            if (importBtn) importBtn.disabled = !complete && stagedMode !== "url";
            if (complete) setStatus("Đã sẵn sàng import.");
        }

        function showFileRow(file, previewSrc) {
            if (!fileRow) return;
            fileRow.classList.remove("is-hidden");
            if (nameEl) nameEl.textContent = file.name;
            if (sizeEl) sizeEl.textContent = formatSize(file.size);
            if (thumb) thumb.style.backgroundImage = previewSrc ? 'url("' + previewSrc + '")' : "";
            if (doneEl) doneEl.classList.add("d-none");
            if (clearBtn) clearBtn.classList.remove("d-none");
            setProgress(0, false);
        }

        function hideFileRow() {
            if (fileRow) fileRow.classList.add("is-hidden");
            setProgress(0, false);
            if (importBtn) importBtn.disabled = true;
        }

        function resetModalUi() {
            if (progressTimer) {
                clearInterval(progressTimer);
                progressTimer = null;
            }
            revokeObjectUrl();
            stagedMode = null;
            if (dropzone) dropzone.classList.remove("is-dragging");
            if (urlField) urlField.value = "";
            if (picker) picker.value = "";
            hideFileRow();
            setStatus("");
        }

        function runFakeProgress(done) {
            if (progressTimer) clearInterval(progressTimer);
            var value = 0;
            var reduced =
                window.matchMedia &&
                window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            if (reduced) {
                setProgress(100, true);
                if (done) done();
                return;
            }
            progressTimer = setInterval(function () {
                value += 12 + Math.floor(Math.random() * 10);
                if (value >= 100) {
                    clearInterval(progressTimer);
                    progressTimer = null;
                    setProgress(100, true);
                    if (done) done();
                } else {
                    setProgress(value, false);
                }
            }, 80);
        }

        function stageFile(file) {
            if (!isAllowedFile(file)) {
                setStatus("File không hợp lệ. Dùng PNG/JPG/JPEG/WEBP/GIF ≤ 5MB.");
                return;
            }
            stagedMode = "file";
            if (urlField) urlField.value = "";
            revokeObjectUrl();
            objectUrl = URL.createObjectURL(file);
            showFileRow(file, objectUrl);
            setStatus("Đang chuẩn bị tệp…");
            runFakeProgress(function () {
                assignFile(picker, file);
            });
        }

        function stageUrl(raw) {
            var value = (raw || "").trim();
            if (!isAllowedUrl(value)) {
                setStatus("URL phải bắt đầu bằng /images/, /uploads/ hoặc http(s)://");
                return;
            }
            stagedMode = "url";
            if (picker) clearFile(picker);
            revokeObjectUrl();
            if (fileRow) {
                fileRow.classList.remove("is-hidden");
                if (nameEl) nameEl.textContent = value.split("/").pop() || value;
                if (sizeEl) sizeEl.textContent = "URL";
                if (thumb) thumb.style.backgroundImage = 'url("' + value.replace(/"/g, '\\"') + '")';
            }
            setProgress(100, true);
            if (urlField) urlField.value = value;
            setStatus("URL hợp lệ — nhấn Import.");
        }

        function syncTriggerChrome(hasImage) {
            var openBtn = document.querySelector(options.openSelector || "[data-tct-image-upload-open]");
            var removeBtn = $(options.removeTrigger);
            if (openBtn) openBtn.textContent = hasImage ? "Thay ảnh" : "Thêm ảnh";
            if (removeBtn) removeBtn.classList.toggle("d-none", !hasImage);
        }

        function updatePagePreview(src) {
            if (!pagePreview) return;
            if (!src) {
                pagePreview.classList.add("is-empty");
                pagePreview.innerHTML =
                    '<div class="tct-image-upload__trigger-empty">' +
                    '<i class="fa-solid fa-image" aria-hidden="true"></i>' +
                    "<span>Chưa có ảnh</span></div>";
                syncTriggerChrome(false);
                return;
            }
            pagePreview.classList.remove("is-empty");
            pagePreview.innerHTML =
                '<img src="' + src.replace(/"/g, "&quot;") + '" alt="" data-reading-image-el />';
            syncTriggerChrome(true);
        }

        function applyImport() {
            if (stagedMode === "file") {
                var file = picker && picker.files && picker.files[0];
                if (!file) return;
                assignFile(fileInput, file);
                if (urlInput) urlInput.value = "";
                if (removeInput) removeInput.checked = false;
                revokeObjectUrl();
                objectUrl = URL.createObjectURL(file);
                updatePagePreview(objectUrl);
            } else if (stagedMode === "url") {
                var value = (urlField && urlField.value) || "";
                if (!isAllowedUrl(value)) return;
                clearFile(fileInput);
                if (urlInput) urlInput.value = value.trim();
                if (removeInput) removeInput.checked = false;
                updatePagePreview(value.trim());
            } else {
                return;
            }
            modal.hide();
        }

        function openFrom(trigger) {
            lastTrigger = trigger || null;
            resetModalUi();
            modal.show();
        }

        if (dropzone) {
            dropzone.addEventListener("click", function () {
                if (picker) picker.click();
            });
            dropzone.addEventListener("keydown", function (e) {
                if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    if (picker) picker.click();
                }
            });
            ["dragenter", "dragover"].forEach(function (evt) {
                dropzone.addEventListener(evt, function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    dropzone.classList.add("is-dragging");
                });
            });
            ["dragleave", "drop"].forEach(function (evt) {
                dropzone.addEventListener(evt, function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    dropzone.classList.remove("is-dragging");
                });
            });
            dropzone.addEventListener("drop", function (e) {
                var file = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
                if (file) stageFile(file);
            });
        }

        if (picker) {
            picker.addEventListener("change", function () {
                var file = picker.files && picker.files[0];
                if (file) stageFile(file);
            });
        }

        if (urlBtn) {
            urlBtn.addEventListener("click", function () {
                stageUrl(urlField && urlField.value);
            });
        }

        if (clearBtn) {
            clearBtn.addEventListener("click", function () {
                resetModalUi();
            });
        }

        if (importBtn) {
            importBtn.addEventListener("click", function (e) {
                e.preventDefault();
                applyImport();
            });
        }

        if (cancelBtn) {
            cancelBtn.addEventListener("click", function () {
                modal.hide();
            });
        }

        modalEl.addEventListener("hidden.bs.modal", function () {
            if (progressTimer) {
                clearInterval(progressTimer);
                progressTimer = null;
            }
            if (lastTrigger && typeof lastTrigger.focus === "function") {
                lastTrigger.focus();
            }
        });

        document.querySelectorAll(options.openSelector || "[data-tct-image-upload-open]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                openFrom(btn);
            });
        });

        var removeTrigger = $(options.removeTrigger);
        if (removeTrigger && removeInput) {
            removeTrigger.addEventListener("click", function () {
                clearFile(fileInput);
                if (urlInput) urlInput.value = "";
                removeInput.checked = true;
                updatePagePreview("");
            });
        }

        return { open: openFrom, modal: modal };
    }

    global.TctImageUpload = {
        bind: createController
    };
})(window);
