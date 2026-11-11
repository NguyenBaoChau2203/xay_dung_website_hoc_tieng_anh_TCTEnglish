/**
 * TCT English - Account UI (AU05 - AU06)
 * Owned script for Profile and Settings views.
 */
(function (global) {
    'use strict';

    function $(id) {
        return document.getElementById(id);
    }

    function initAccountUi() {
        var root = document.querySelector('.account-ui');
        if (!root || root.dataset.accountInitialized === 'true') return;
        root.dataset.accountInitialized = 'true';

        var sidebar = $('accountSidebar');
        var sidebarToggle = $('accountSidebarToggle');
        var sidebarToggleIcon = $('accountSidebarToggleIcon');
        var mobileToggle = $('sidebarMobileToggle');
        var overlay = $('sidebarOverlay');

        var mobileMql = window.matchMedia ? window.matchMedia('(max-width: 992px)') : { matches: false };
        var offcanvasInstance = null;
        var hideAfterShown = false;

        // Shell elements to inert when mobile drawer is open
        var inertTargets = ['.header', '.tct-footer', '[data-ai-launcher]'];
        var previousInertStates = new Map();

        function setShellInert(enable) {
            inertTargets.forEach(function (sel) {
                var el = document.querySelector(sel);
                if (!el) return;
                if (enable) {
                    if (!previousInertStates.has(el)) {
                        previousInertStates.set(el, el.inert || false);
                    }
                    el.inert = true;
                } else if (previousInertStates.has(el)) {
                    el.inert = previousInertStates.get(el);
                    previousInertStates.delete(el);
                }
            });
        }

        function cleanupDesktopState() {
            if (!sidebar) return;
            sidebar.inert = false;
            sidebar.classList.remove('offcanvas', 'offcanvas-start', 'open', 'show', 'showing', 'hiding');
            sidebar.removeAttribute('tabindex');
            sidebar.removeAttribute('role');
            sidebar.removeAttribute('aria-modal');

            if (offcanvasInstance) {
                offcanvasInstance.dispose();
                offcanvasInstance = null;
            }

            // Bootstrap restores its own scroll lock before hidden.bs.offcanvas.
            // Other dialogs may still own body styles, backdrops and inert state.
            setShellInert(false);
            if (overlay) {
                overlay.hidden = true;
                overlay.classList.remove('show');
            }
            if (mobileToggle) {
                mobileToggle.setAttribute('aria-expanded', 'false');
            }

            try {
                var collapsed = localStorage.getItem('accountSidebarCollapsed') === 'true';
                sidebar.classList.toggle('sidebar-collapsed', collapsed);
                if (sidebarToggle) {
                    sidebarToggle.setAttribute('aria-expanded', String(!collapsed));
                }
                if (sidebarToggleIcon) {
                    sidebarToggleIcon.style.transform = collapsed ? 'rotate(180deg)' : '';
                }
            } catch (e) {}
        }

        function closeDrawer() {
            if (!sidebar) return;
            if (sidebar.classList.contains('showing')) {
                hideAfterShown = true;
                return;
            }
            if (sidebar.classList.contains('hiding')) return;
            if (offcanvasInstance) {
                offcanvasInstance.hide();
            } else {
                sidebar.classList.remove('open', 'show');
                sidebar.inert = mobileMql.matches;
                if (overlay) {
                    overlay.hidden = true;
                    overlay.classList.remove('show');
                }
                if (mobileToggle) mobileToggle.setAttribute('aria-expanded', 'false');
                setShellInert(false);
            }
        }

        // Sync drawer state based on viewport
        function syncDrawer() {
            if (!sidebar) return;
            if (mobileMql.matches) {
                sidebar.classList.remove('sidebar-collapsed');
                sidebar.classList.add('offcanvas', 'offcanvas-start');
                sidebar.tabIndex = -1;
                var isOpen = sidebar.classList.contains('show') || sidebar.classList.contains('open') ||
                             sidebar.classList.contains('showing');
                sidebar.inert = !isOpen;
                if (global.bootstrap && global.bootstrap.Offcanvas && !offcanvasInstance) {
                    offcanvasInstance = global.bootstrap.Offcanvas.getOrCreateInstance(sidebar, {
                        backdrop: false,
                        scroll: false,
                        keyboard: true
                    });
                }
            } else {
                // Do not overlap Bootstrap transitions or dispose a live instance.
                // The completion handlers re-evaluate the latest breakpoint.
                if (sidebar.classList.contains('showing') || sidebar.classList.contains('hiding')) return;
                if (offcanvasInstance && (sidebar.classList.contains('show') || sidebar.classList.contains('open'))) {
                    closeDrawer();
                } else {
                    cleanupDesktopState();
                }
            }
        }

        if (sidebarToggle) {
            sidebarToggle.addEventListener('click', function () {
                if (mobileMql.matches) {
                    closeDrawer();
                } else {
                    var isCollapsed = sidebar.classList.toggle('sidebar-collapsed');
                    if (sidebarToggle) {
                        sidebarToggle.setAttribute('aria-expanded', String(!isCollapsed));
                    }
                    if (sidebarToggleIcon) {
                        sidebarToggleIcon.style.transform = isCollapsed ? 'rotate(180deg)' : '';
                    }
                    try {
                        localStorage.setItem('accountSidebarCollapsed', String(isCollapsed));
                    } catch (e) {}
                }
            });
        }

        if (mobileToggle) {
            mobileToggle.addEventListener('click', function () {
                if (!mobileMql.matches || !sidebar || sidebar.classList.contains('showing') || sidebar.classList.contains('hiding')) return;
                if (sidebar) sidebar.inert = false;
                if (offcanvasInstance) {
                    offcanvasInstance.show();
                } else if (sidebar) {
                    sidebar.classList.add('open', 'show');
                    if (overlay) {
                        overlay.hidden = false;
                        overlay.classList.add('show');
                    }
                    mobileToggle.setAttribute('aria-expanded', 'true');
                    setShellInert(true);
                }
            });
        }

        if (overlay) {
            overlay.addEventListener('click', closeDrawer);
        }

        if (sidebar) {
            sidebar.addEventListener('show.bs.offcanvas', function () {
                sidebar.inert = false;
                if (overlay) {
                    overlay.hidden = false;
                    overlay.classList.add('show');
                }
                if (mobileToggle) {
                    mobileToggle.setAttribute('aria-expanded', 'true');
                }
                setShellInert(true);
            });

            sidebar.addEventListener('shown.bs.offcanvas', function () {
                if (hideAfterShown || !mobileMql.matches) {
                    hideAfterShown = false;
                    closeDrawer();
                    return;
                }
                sidebar.classList.add('open');
                var firstFocusable = sidebar.querySelector('nav a, nav button, button');
                if (firstFocusable && typeof firstFocusable.focus === 'function') {
                    firstFocusable.focus();
                }
            });

            sidebar.addEventListener('hidden.bs.offcanvas', function () {
                if (mobileMql.matches && mobileToggle && typeof mobileToggle.focus === 'function') {
                    mobileToggle.focus();
                }
                sidebar.inert = mobileMql.matches;
                sidebar.classList.remove('open');
                if (overlay) {
                    overlay.classList.remove('show');
                    overlay.hidden = true;
                }
                if (mobileToggle) {
                    mobileToggle.setAttribute('aria-expanded', 'false');
                }
                setShellInert(false);
                hideAfterShown = false;
                syncDrawer();
            });
        }

        if (mobileMql.addEventListener) {
            mobileMql.addEventListener('change', syncDrawer);
        }

        syncDrawer();

        // ── Tab Management (Profile AU05) ──────────────────────────
        function getNavItems() {
            var items = [];
            var sItems = document.querySelectorAll('.sidebar-nav-item');
            if (sItems && sItems.forEach) {
                sItems.forEach(function (el) { items.push(el); });
            }
            var pItems = document.querySelectorAll('.preview-nav-item');
            if (pItems && pItems.forEach) {
                pItems.forEach(function (el) {
                    if (!items.includes(el)) items.push(el);
                });
            }
            return items;
        }

        function getTabPanes() {
            var panes = [];
            ['profile', 'security', 'subscription'].forEach(function (t) {
                var p = $('tab-' + t);
                if (p) panes.push(p);
            });
            if (panes.length === 0) {
                var all = document.querySelectorAll('.account-tab-pane');
                if (all && all.forEach) {
                    all.forEach(function (el) { panes.push(el); });
                }
            }
            return panes;
        }

        function activateTab(tabId, moveFocus) {
            if (!tabId) tabId = 'profile';
            var validTabs = ['profile', 'security', 'subscription'];
            if (!validTabs.includes(tabId)) tabId = 'profile';

            var tabPanes = getTabPanes();
            tabPanes.forEach(function (pane) {
                var isActive = pane.id === 'tab-' + tabId;
                pane.hidden = !isActive;
                pane.classList.toggle('active', isActive);
            });

            var navItems = getNavItems();
            navItems.forEach(function (item) {
                var itemTab = item.getAttribute('data-tab');
                if (itemTab) {
                    var isActive = itemTab === tabId;
                    item.classList.toggle('active', isActive);
                    if (isActive) {
                        item.setAttribute('aria-current', 'page');
                    } else {
                        item.removeAttribute('aria-current');
                    }
                }
            });

            // Update URL without reload
            try {
                var url = new URL(window.location.href);
                if (tabId === 'profile') {
                    url.searchParams.delete('tab');
                } else {
                    url.searchParams.set('tab', tabId);
                }
                window.history.replaceState({}, '', url.toString());
            } catch (e) {}

            if (moveFocus) {
                var activePane = $('tab-' + tabId);
                if (activePane) {
                    var heading = activePane.querySelector('h2, h3, [tabindex="-1"]');
                    if (heading) {
                        heading.tabIndex = -1;
                        heading.focus();
                    }
                }
            }
        }

        // Initialize tab from URL
        try {
            var params = new URLSearchParams(window.location.search);
            var initTab = params.get('tab') || 'profile';
            activateTab(initTab, false);
        } catch (e) {
            activateTab('profile', false);
        }

        // Click delegate for tab changes and deep links
        document.addEventListener('click', function (e) {
            var tabTrigger = e.target.closest('[data-tab], [data-profile-tab]');
            if (tabTrigger) {
                var tabId = tabTrigger.dataset.tab || tabTrigger.dataset.profileTab;
                if (tabId) {
                    e.preventDefault();
                    if (sidebar && (sidebar.classList.contains('show') || sidebar.classList.contains('showing')) && offcanvasInstance) {
                        sidebar.addEventListener('hidden.bs.offcanvas', function onHidden() {
                            sidebar.removeEventListener('hidden.bs.offcanvas', onHidden);
                            activateTab(tabId, true);
                        });
                        closeDrawer();
                    } else {
                        activateTab(tabId, true);
                    }
                    return;
                }
            }

            // Password toggles
            var pwdBtn = e.target.closest('[data-password], .toggle-pwd');
            if (pwdBtn) {
                e.preventDefault();
                var targetId = pwdBtn.dataset.password;
                var input = targetId ? $(targetId) : pwdBtn.closest('.input-group')?.querySelector('input');
                if (input) {
                    var willShow = input.type === 'password';
                    input.type = willShow ? 'text' : 'password';
                    pwdBtn.setAttribute('aria-pressed', String(willShow));
                    var label = pwdBtn.getAttribute('aria-label') || '';
                    if (willShow) {
                        pwdBtn.setAttribute('aria-label', label.replace('Hiện', 'Ẩn'));
                    } else {
                        pwdBtn.setAttribute('aria-label', label.replace('Ẩn', 'Hiện'));
                    }
                    var icon = pwdBtn.querySelector('i');
                    if (icon) {
                        icon.className = willShow ? 'fa-solid fa-eye-slash' : 'fa-solid fa-eye';
                    }
                }
                return;
            }

            // Settings section nav
            var sectionBtn = e.target.closest('[data-section]');
            if (sectionBtn) {
                e.preventDefault();
                var secId = sectionBtn.dataset.section;
                var targetSection = $('section-' + secId);
                if (targetSection) {
                    if (typeof targetSection.scrollIntoView === 'function') {
                        targetSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
                    }
                    var h = targetSection.querySelector('h2, h3');
                    if (h && typeof h.focus === 'function') {
                        h.tabIndex = -1;
                        h.focus();
                    }
                }
                return;
            }
        });

        // ── Avatar Preview (Profile AU05) ──────────────────────────
        var avatarClickTarget = $('avatarClickTarget');
        var avatarFileInput = $('Avatar');
        var avatarPreview = $('avatarPreview');
        var avatarFallback = $('avatarFallback');
        var avatarStatus = $('avatar-status');
        var profileCancel = $('profile-cancel');
        var fullNameInput = $('FullName');

        var initialFullName = fullNameInput ? fullNameInput.value : '';
        var initialAvatarSrc = (avatarPreview && (avatarPreview.getAttribute('src') || avatarPreview.src)) ? (avatarPreview.getAttribute('src') || avatarPreview.src) : '';
        var initialHasAvatar = Boolean(initialAvatarSrc && avatarPreview && avatarPreview.style.display !== 'none' && !avatarPreview.hidden);

        if (avatarClickTarget && avatarFileInput) {
            avatarClickTarget.addEventListener('click', function () {
                avatarFileInput.click();
            });
        }

        function setAvatarState(kind, message) {
            if (!avatarPreview || !avatarFallback) return;
            if (kind === 'error') {
                avatarPreview.hidden = true;
                avatarPreview.style.display = 'none';
                avatarFallback.hidden = false;
                avatarFallback.style.display = 'flex';
                if (avatarStatus) {
                    avatarStatus.hidden = false;
                    avatarStatus.className = 'text-danger small mt-2';
                    avatarStatus.textContent = message || 'Không thể đọc ảnh. Vui lòng chọn JPG hoặc PNG khác (tối đa 2MB).';
                }
            } else if (kind === 'preview') {
                avatarPreview.hidden = false;
                avatarPreview.style.display = '';
                avatarFallback.hidden = true;
                avatarFallback.style.display = 'none';
                if (avatarStatus) {
                    avatarStatus.hidden = false;
                    avatarStatus.className = 'text-primary small mt-2';
                    avatarStatus.textContent = 'Ảnh mới chưa lưu';
                }
            } else {
                if (initialHasAvatar && initialAvatarSrc) {
                    avatarPreview.src = initialAvatarSrc;
                    avatarPreview.hidden = false;
                    avatarPreview.style.display = '';
                    avatarFallback.hidden = true;
                    avatarFallback.style.display = 'none';
                } else {
                    avatarPreview.hidden = true;
                    avatarPreview.style.display = 'none';
                    avatarFallback.hidden = false;
                    avatarFallback.style.display = 'flex';
                }
                if (avatarStatus) {
                    avatarStatus.hidden = true;
                    avatarStatus.textContent = '';
                }
            }
        }

        if (avatarFileInput) {
            avatarFileInput.addEventListener('change', function (e) {
                var file = e.target.files && e.target.files[0];
                if (!file) return;

                // Validate MIME and size (2MB max)
                var allowed = ['image/jpeg', 'image/jpg', 'image/png'];
                if (!allowed.includes(file.type.toLowerCase()) || file.size > 2 * 1024 * 1024) {
                    e.target.value = '';
                    setAvatarState('error');
                    return;
                }

                if (typeof FileReader !== 'undefined') {
                    var reader = new FileReader();
                    reader.onload = function (evt) {
                        avatarPreview.src = evt.target.result;
                        setAvatarState('preview');
                    };
                    reader.onerror = function () {
                        setAvatarState('error');
                    };
                    reader.readAsDataURL(file);
                }
            });
        }

        if (avatarPreview) {
            avatarPreview.addEventListener('error', function () {
                setAvatarState('error');
            });
        }

        if (profileCancel) {
            profileCancel.addEventListener('click', function () {
                if (fullNameInput) {
                    fullNameInput.value = initialFullName;
                }
                if (avatarFileInput) {
                    avatarFileInput.value = '';
                }
                setAvatarState('default');
                if (typeof window.location.reload === 'function') {
                    window.location.reload();
                }
            });
        }

        // ── Delete Account Modal Gate ──────────────────────────────
        var deleteConfirmInput = $('deleteConfirmInput');
        var deleteAccountBtn = $('deleteAccountBtn');
        var deleteAccountModal = $('deleteAccountModal');

        if (deleteConfirmInput && deleteAccountBtn) {
            deleteConfirmInput.addEventListener('input', function () {
                var val = deleteConfirmInput.value || '';
                deleteAccountBtn.disabled = (val.trim() !== 'CONFIRM');
            });
        }

        if (deleteAccountModal) {
            deleteAccountModal.addEventListener('shown.bs.modal', function () {
                if (deleteConfirmInput) {
                    deleteConfirmInput.value = '';
                    deleteConfirmInput.focus();
                }
                if (deleteAccountBtn) {
                    deleteAccountBtn.disabled = true;
                }
            });

            deleteAccountModal.addEventListener('hidden.bs.modal', function () {
                if (deleteConfirmInput) {
                    deleteConfirmInput.value = '';
                }
                if (deleteAccountBtn) {
                    deleteAccountBtn.disabled = true;
                }
            });
        }

        // ── Form Double Submit Prevention ──────────────────────────
        var forms = document.querySelectorAll('.account-ui form');
        function restoreFormSubmit(f) {
            f._inFlight = false;
            var submitBtn = f.querySelector('button[type="submit"]');
            if (!submitBtn) return;
            if (submitBtn.id === 'deleteAccountBtn') {
                var val = deleteConfirmInput ? deleteConfirmInput.value : '';
                submitBtn.disabled = (val.trim() !== 'CONFIRM');
            } else {
                submitBtn.disabled = false;
            }
            submitBtn.removeAttribute('aria-busy');
        }

        forms.forEach(function (f) {
            f.addEventListener('submit', function (e) {
                if (e.defaultPrevented) return;
                var submitBtn = f.querySelector('button[type="submit"]');
                if (!submitBtn) return;

                // Validate form first if available
                if (typeof f.checkValidity === 'function' && !f.checkValidity()) {
                    return;
                }

                // If this is the delete account form, enforce CONFIRM check
                if (submitBtn.id === 'deleteAccountBtn' || (deleteConfirmInput && f.contains(deleteConfirmInput))) {
                    var confirmVal = deleteConfirmInput ? deleteConfirmInput.value : '';
                    if (confirmVal.trim() !== 'CONFIRM') {
                        e.preventDefault();
                        submitBtn.disabled = true;
                        return;
                    }
                }

                if (submitBtn.disabled || f._inFlight) {
                    e.preventDefault();
                    return;
                }
                f._inFlight = true;
                submitBtn.disabled = true;
                submitBtn.setAttribute('aria-busy', 'true');
                // Validators can also cancel later in this event's propagation.
                Promise.resolve().then(function () {
                    if (e.defaultPrevented) restoreFormSubmit(f);
                });
            });
        });

        window.addEventListener('pageshow', function () {
            forms.forEach(restoreFormSubmit);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAccountUi);
    } else {
        initAccountUi();
    }

    // Expose previewImage alias for backward compatibility
    global.previewImage = function (input) {
        var event = new Event('change');
        input.dispatchEvent(event);
    };

})(typeof window !== 'undefined' ? window : globalThis);
