/**
 * Verification test for user profile dropdown and global page loader
 */
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

test('User dropdown markup in _Layout.cshtml contains clay classes, semantic roles and badge', () => {
    const layoutPath = path.resolve(__dirname, '../../TCTEnglish/Views/Shared/_Layout.cshtml');
    const content = fs.readFileSync(layoutPath, 'utf8');

    assert.ok(content.includes('id="userProfileBtn"'), 'userProfileBtn ID must exist');
    assert.ok(content.includes('id="userDropdown"'), 'userDropdown ID must exist');
    assert.ok(content.includes('user-avatar-wrap'), 'Avatar wrap must exist');
    assert.ok(content.includes('avatar-fallback'), 'Initial avatar fallback must exist');
    assert.ok(content.includes('user-role-badge'), 'Role badge must exist');
    assert.ok(content.includes('user-dropdown-item'), 'Item classes must exist');
    assert.ok(content.includes('item-icon-box--profile'), 'Profile icon box class must exist');
    assert.ok(content.includes('item-icon-box--settings'), 'Settings icon box class must exist');
    assert.ok(content.includes('item-icon-box--logout'), 'Logout icon box class must exist');
    assert.ok(content.includes('item-icon-box--admin'), 'Admin icon box class must exist');
    assert.ok(content.includes('item-arrow'), 'Item arrow indicator must exist');
    assert.ok(content.includes('role="menu"'), 'role="menu" must be present for accessibility');
    assert.ok(content.includes('role="menuitem"'), 'role="menuitem" must be present for accessibility');
});

test('layout.css contains Claymorphism styling and interactive keyframes for user dropdown', () => {
    const cssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/layout.css');
    const content = fs.readFileSync(cssPath, 'utf8');

    assert.ok(content.includes('.user-profile-btn'), '.user-profile-btn must be styled');
    assert.ok(content.includes('.user-dropdown'), '.user-dropdown must be styled');
    assert.ok(content.includes('.user-dropdown.active'), '.user-dropdown.active must be styled');
    assert.ok(content.includes('tctUserDropdownPopIn'), 'Opening animation keyframe must exist');
    assert.ok(content.includes('.item-icon-box'), '.item-icon-box must be styled');
    assert.ok(content.includes('.user-role-badge'), '.user-role-badge must be styled');
    assert.ok(content.includes('.user-chevron'), 'Chevron rotation must be defined');
});

test('_Loader.cshtml and page-loader.css provide Claymorphism page loader', () => {
    const loaderPath = path.resolve(__dirname, '../../TCTEnglish/Views/Shared/_Loader.cshtml');
    const cssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/page-loader.css');

    const loaderContent = fs.readFileSync(loaderPath, 'utf8');
    const cssContent = fs.readFileSync(cssPath, 'utf8');

    assert.ok(loaderContent.includes('id="tct-page-loader"'), 'tct-page-loader ID must exist');
    assert.ok(loaderContent.includes('id="tctPageProgressBar"'), 'tctPageProgressBar ID must exist');
    assert.ok(loaderContent.includes('tct-loader-emblem-core'), 'Emblem core must exist');
    assert.ok(loaderContent.includes('dismissLoader'), 'dismissLoader function must exist');
    assert.ok(loaderContent.includes('showLoader'), 'showLoader function must exist');
    assert.ok(loaderContent.includes('tctShowLoader'), 'tctShowLoader global window API must exist');
    assert.ok(loaderContent.includes('beforeunload'), 'beforeunload listener must exist');
    assert.ok(loaderContent.includes('pageshow'), 'pageshow listener must exist for bfcache safety');

    assert.ok(cssContent.includes('.tct-page-loader-backdrop'), 'Backdrop class must be styled');
    assert.ok(cssContent.includes('.tct-page-loader-card'), 'Card class must be styled');
    assert.ok(cssContent.includes('.tct-page-progress-bar'), 'Progress bar must be styled');
    assert.ok(cssContent.includes('@media (prefers-reduced-motion: reduce)'), 'Reduced motion must be supported');
});

test('Account sidebar logout button and transitions support clean collapse without text overflow', () => {
    const profilePath = path.resolve(__dirname, '../../TCTEnglish/Views/Account/Profile.cshtml');
    const cssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/account-ui.css');

    const profileContent = fs.readFileSync(profilePath, 'utf8');
    const cssContent = fs.readFileSync(cssPath, 'utf8');

    // Verification that logout label is wrapped in nav-label so it hides on collapse
    assert.ok(profileContent.includes('<span class="nav-label">Đăng xuất</span>'), 'Profile logout must wrap text in nav-label');

    // Verification of smooth transitions and collapsed rules in account-ui.css
    assert.ok(cssContent.includes('transition: width 280ms'), 'Sidebar width transition must exist');
    assert.ok(cssContent.includes('transition: grid-template-columns 280ms'), 'Surface grid transition must exist');
    assert.ok(cssContent.includes('.sidebar-collapsed .nav-logout .btn'), 'Collapsed logout button styling must exist');
    assert.ok(cssContent.includes('.sidebar-collapsed .nav-label'), 'Collapsed nav-label hiding rule must exist');
});

test('Header menuToggleBtn works on Account pages: mainSidebar is rendered with collapsed class when HideSidebar is true', () => {
    const layoutPath = path.resolve(__dirname, '../../TCTEnglish/Views/Shared/_Layout.cshtml');
    const content = fs.readFileSync(layoutPath, 'utf8');

    assert.ok(content.includes('string sidebarClass = hideSidebar ? "sidebar collapsed" : "sidebar";'), 'sidebarClass must calculate collapsed state');
    assert.ok(content.includes('<aside class="@sidebarClass" id="mainSidebar"'), 'mainSidebar must always be present in DOM with @sidebarClass');
    assert.ok(!content.includes('@if (!hideSidebar)'), 'Must not completely omit mainSidebar from DOM when hideSidebar is true');
});

test('Account Profile tabs contain theme-colored sidebar icon boxes, stats cards, and dynamic tab animations', () => {
    const profilePath = path.resolve(__dirname, '../../TCTEnglish/Views/Account/Profile.cshtml');
    const cssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/account-ui.css');
    const jsPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/account-ui.js');

    const profileContent = fs.readFileSync(profilePath, 'utf8');
    const cssContent = fs.readFileSync(cssPath, 'utf8');
    const jsContent = fs.readFileSync(jsPath, 'utf8');

    // Sidebar icon boxes in the shared Account shell
    assert.ok(profileContent.includes('nav-icon-box--profile'), 'Profile view must include nav-icon-box--profile');
    assert.ok(profileContent.includes('nav-icon-box--security'), 'Profile view must include nav-icon-box--security');
    assert.ok(profileContent.includes('nav-icon-box--subscription'), 'Profile view must include nav-icon-box--subscription');
    assert.ok(profileContent.includes('nav-icon-box--logout'), 'Profile view must include nav-icon-box--logout');

    // Profile page content enrichment (stats, panels, badges)
    assert.ok(profileContent.includes('stat-clay-card stat-card--streak'), 'Profile stats streak card must exist');
    assert.ok(profileContent.includes('stat-icon-box--fire'), 'Fire streak icon box must exist');
    assert.ok(profileContent.includes('stat-icon-box--trophy'), 'Trophy badges icon box must exist');
    assert.ok(profileContent.includes('panel-icon-box--blue'), 'Blue panel icon box must exist');
    assert.ok(profileContent.includes('panel-icon-box--gold'), 'Gold panel icon box must exist');
    assert.ok(profileContent.includes('panel-icon-box--purple'), 'Purple panel icon box must exist');
    assert.ok(profileContent.includes('badge-item'), 'Badge item class must exist');
    assert.ok(profileContent.includes('badge-icon-box'), 'Badge icon box class must exist');

    // CSS rules and animations
    assert.ok(cssContent.includes('.account-ui .nav-icon-box'), '.nav-icon-box must be styled in account-ui.css');
    assert.ok(cssContent.includes('.account-ui .nav-icon-box--profile'), '.nav-icon-box--profile must be styled');
    assert.ok(cssContent.includes('.account-ui .nav-icon-box--security'), '.nav-icon-box--security must be styled');
    assert.ok(cssContent.includes('.account-ui .nav-icon-box--subscription'), '.nav-icon-box--subscription must be styled');
    assert.ok(cssContent.includes('.account-ui .nav-icon-box--logout'), '.nav-icon-box--logout must be styled');
    assert.ok(cssContent.includes('.account-ui .panel-icon-box'), '.panel-icon-box must be styled');
    assert.ok(cssContent.includes('.account-ui .stat-clay-card'), '.stat-clay-card must be styled');
    assert.ok(cssContent.includes('@keyframes accountTabFadeIn'), 'accountTabFadeIn keyframes must exist');
    assert.ok(cssContent.includes('@keyframes iconWiggle'), 'iconWiggle keyframes must exist');

    // JS reflow trigger for restarting tab animation
    assert.ok(jsContent.includes('pane.offsetWidth'), 'activateTab must force reflow to smoothly replay CSS animation');
});

test('Collapsed account sidebar arrow toggle is circular and all items are vertically centered and aligned', () => {
    const cssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/account-ui.css');
    const cssContent = fs.readFileSync(cssPath, 'utf8');

    // Arrow toggle button circular geometry
    assert.ok(cssContent.includes('#accountSidebarToggle'), '#accountSidebarToggle must be styled');
    assert.ok(cssContent.includes('border-radius: 50% !important;'), 'Toggle button must be circular (border-radius: 50%)');
    assert.ok(cssContent.includes('aspect-ratio: 1 / 1 !important;'), 'Toggle button must have 1:1 aspect ratio to avoid oval stretching');

    // Collapsed centering and gap removal
    assert.ok(cssContent.includes('.account-ui .sidebar-collapsed .nav-heading'), 'Collapsed nav-heading must be defined');
    assert.ok(cssContent.includes('.account-ui .sidebar-collapsed .sidebar-nav-item'), 'Collapsed nav items must be defined');
    assert.ok(cssContent.includes('.account-ui .sidebar-collapsed .nav-logout .btn'), 'Collapsed logout button must be defined');

    // Ensure nav-label has display: none !important so flex gap does NOT shift icons to the left
    assert.ok(cssContent.includes('display: none !important;'), 'Collapsed nav-label must have display: none to prevent shifting icons left');
});

test('Footer spacing is tightened and optimized on Account pages and layout', () => {
    const accountCssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/account-ui.css');
    const layoutCssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/layout.css');

    const accountCss = fs.readFileSync(accountCssPath, 'utf8');
    const layoutCss = fs.readFileSync(layoutCssPath, 'utf8');

    // Account view optimizations
    assert.ok(accountCss.includes('.main:has(.account-ui)'), 'Shell rule for account-ui must exist');
    assert.ok(accountCss.includes('body:has(.account-ui) .tct-footer'), 'Footer margin-top override for account-ui must exist');
    assert.ok(accountCss.includes('min-height: auto !important;'), 'Account main min-height must be auto');
    assert.ok(accountCss.includes('margin-bottom: 0 !important;'), 'Account last panel bottom margin must be cleared');

    // Layout footer optimization
    assert.ok(layoutCss.includes('margin-top: var(--tct-space-xl, 2rem);'), 'Default footer margin-top must be tightened');
});

test('Account tabs feature compact, balanced Bento layouts without artificial empty whitespace', () => {
    const profileCshtmlPath = path.resolve(__dirname, '../../TCTEnglish/Views/Account/Profile.cshtml');
    const accountCssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/account-ui.css');

    const profileContent = fs.readFileSync(profileCshtmlPath, 'utf8');
    const accountCss = fs.readFileSync(accountCssPath, 'utf8');

    // CSS prevents vertical stretching that causes blank voids
    assert.ok(accountCss.includes('align-items: start;'), 'profile-columns and security-grid must use align-items: start to stop empty stretch voids');

    // Tab 1 Profile Bento layout & 100% Symmetrical Height Alignment
    assert.ok(profileContent.includes('profile-form-grid'), 'Profile form must use 2-column input grid');
    assert.ok(profileContent.includes('account-overview-strip'), 'Profile form must include metadata overview strip to prevent void');
    assert.ok(profileContent.includes('profile-main-stack'), 'Profile must have 2-card main stack to match side stack');
    assert.ok(profileContent.includes('learning-quick-actions'), 'Profile must have learning quick actions to fill gap');
    assert.ok(profileContent.includes('habit-metrics-grid'), 'Right column must include habit-metrics-grid card to balance height with left stack');
    assert.ok(accountCss.includes('.account-ui .profile-main-stack'), 'CSS for profile-main-stack must exist');
    assert.ok(accountCss.includes('.account-ui .learning-quick-actions'), 'CSS for learning-quick-actions must exist');
    assert.ok(accountCss.includes('.account-ui .habit-metrics-grid'), 'CSS for habit-metrics-grid must exist');
    assert.ok(accountCss.includes('.account-ui .profile-main-stack > .panel:last-child'), 'Profile left stack last panel must stretch to match right stack');
    assert.ok(accountCss.includes('.account-ui .info-stack > .panel:last-child'), 'Profile right stack last panel must stretch to match left stack');
    assert.ok(profileContent.includes('badge-showcase-grid'), 'Empty badges must display inspirational milestone preview cards');

    // Tab 2 Security Bento layout
    assert.ok(profileContent.includes('security-grid'), 'Security tab must use 2-column Bento grid');
    assert.ok(profileContent.includes('security-main'), 'Security tab must have main column for forms');
    assert.ok(profileContent.includes('security-side'), 'Security tab must have side column for health and notifications');

    // Tab 3 Subscription Bento layout
    assert.ok(profileContent.includes('subscription-hub'), 'Subscription tab must use subscription-hub container');
    assert.ok(profileContent.includes('subscription-plans-grid'), 'Subscription tab must include plan comparison grid');
    assert.ok(profileContent.includes('plan-card--premium'), 'Subscription tab must have highlighted premium plan card');
    assert.ok(profileContent.includes('subscription-perks'), 'Subscription tab must have perks and guarantees strip');
});

test('Account stat icon boxes and panel icon boxes are centered and cannot be displaced by baseline alignment', () => {
    const accountCssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/account-ui.css');
    const accountCss = fs.readFileSync(accountCssPath, 'utf8');

    // stat-icon-box must be circular, perfectly centered with line-height 1 and flexbox
    assert.ok(accountCss.includes('.account-ui .stat-icon-box'), 'stat-icon-box rule must exist');
    assert.ok(accountCss.includes('border-radius: 50% !important;'), 'stat-icon-box must be circular (50%)');
    assert.ok(accountCss.includes('.account-ui .stat-icon-box i'), 'stat-icon-box i rule must exist');

    // Legacy fallback must NOT match descendant div inside profile-stats
    assert.ok(!accountCss.includes('.account-ui .profile-stats div:not(.stat-clay-card)'), 'Must NOT have loose descendant selector causing baseline alignment on stat-icon-box');
    assert.ok(accountCss.includes('.account-ui dl.profile-stats > div'), 'Fallback must be strictly scoped to dl.profile-stats > div');

    // Centering on icon box children
    assert.ok(accountCss.includes('.account-ui .panel-icon-box i'), 'panel-icon-box i must be centered');
    assert.ok(accountCss.includes('.account-ui .badge-icon-box i'), 'badge-icon-box i must be centered');
    assert.ok(accountCss.includes('.account-ui .social-icon-box i'), 'social-icon-box i must be centered');
});

test('Account views feature a unified dynamic Claymorphism Account Header Hero with interactive chips and tab sync', () => {
    const profilePath = path.resolve(__dirname, '../../TCTEnglish/Views/Account/Profile.cshtml');
    const cssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/account-ui.css');
    const jsPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/js/account-ui.js');

    const profileHtml = fs.readFileSync(profilePath, 'utf8');
    const css = fs.readFileSync(cssPath, 'utf8');
    const js = fs.readFileSync(jsPath, 'utf8');

    // Profile markup checks
    assert.ok(profileHtml.includes('account-header-hero'), 'Profile must use account-header-hero');
    assert.ok(profileHtml.includes('account-breadcrumb-pill'), 'Profile must have breadcrumb pill');
    assert.ok(profileHtml.includes('id="accountPageTitle"'), 'Profile must have dynamic H1 title element');
    assert.ok(profileHtml.includes('id="accountPageDesc"'), 'Profile must have dynamic description element');
    assert.ok(profileHtml.includes('account-header-hero__meta'), 'Profile must have quick meta chips container');
    assert.ok(profileHtml.includes('meta-chip--status'), 'Status chip must exist');
    assert.ok(profileHtml.includes('meta-chip--role'), 'Role chip must exist');
    assert.ok(profileHtml.includes('meta-chip--dynamic'), 'Dynamic tab stat chip must exist');

    // No duplicate visible headings in tabs
    assert.ok(profileHtml.includes('id="profile-security-heading" class="visually-hidden"'), 'Security tab heading must be visually-hidden to avoid duplicate banner');
    assert.ok(profileHtml.includes('id="profile-subscription-heading" class="visually-hidden"'), 'Subscription tab heading must be visually-hidden to avoid duplicate banner');

    // CSS rules
    assert.ok(css.includes('.account-ui .account-header-hero'), 'Hero container CSS must exist');
    assert.ok(css.includes('.account-ui .account-breadcrumb-pill'), 'Breadcrumb pill CSS must exist');
    assert.ok(css.includes('.account-ui .account-meta-chip'), 'Meta chip CSS must exist');
    assert.ok(css.includes('.account-ui .account-meta-chip .meta-chip__icon'), 'Meta chip icon CSS must exist');

    // JS dynamic sync logic
    assert.ok(js.includes('tabHeroConfig'), 'JS must define tabHeroConfig for dynamic titles and breadcrumbs');
    assert.ok(js.includes('accountPageTitle'), 'JS must update accountPageTitle on tab switch');
    assert.ok(js.includes('accountBreadcrumbActive'), 'JS must update accountBreadcrumbActive on tab switch');
});

test('Account Security tab retains all settings cards', () => {
    const profilePath = path.resolve(__dirname, '../../TCTEnglish/Views/Account/Profile.cshtml');
    const cssPath = path.resolve(__dirname, '../../TCTEnglish/wwwroot/css/account-ui.css');

    const profileHtml = fs.readFileSync(profilePath, 'utf8');
    const css = fs.readFileSync(cssPath, 'utf8');

    // Cards in Profile.cshtml
    assert.ok(profileHtml.includes('security-card--email'), 'Email card must have security-card--email class');
    assert.ok(profileHtml.includes('security-card--health'), 'Health card must have security-card--health class');
    assert.ok(profileHtml.includes('security-card--password'), 'Password card must have security-card--password class');
    assert.ok(profileHtml.includes('security-card--notifications'), 'Notifications card must have security-card--notifications class');
    assert.ok(profileHtml.includes('security-card--linked'), 'Linked card must have security-card--linked class');
    assert.ok(profileHtml.includes('security-card--danger'), 'Danger card must have security-card--danger class');

});
