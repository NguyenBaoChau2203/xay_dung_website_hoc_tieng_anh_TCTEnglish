'use strict';

// Regressions found by the 2026-11-16 bug sweep (plan: landing-night-and-bug-sweep, N2).
// Source-level checks, in the style of test_admin_shell_and_users_ui.cjs.

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const ROOT = path.join(__dirname, '..', '..', 'TCTEnglish');
const read = (rel) => fs.readFileSync(path.join(ROOT, rel), 'utf8');
const adminView = (rel) => `Areas/Admin/Views/${rel}`;

// ---------------------------------------------------------------- jQuery validation scripts

test('admin layout does not load jQuery, so views must not include the jQuery validation partial', () => {
  const layout = read(adminView('Shared/_AdminLayout.cshtml'));
  if (/jquery(\.min)?\.js|lib\/jquery\/|code\.jquery\.com/i.test(layout)) {
    return; // once the layout loads jQuery before the Scripts section the partial is safe again
  }
  const owned = [
    'SetManagement/Create.cshtml',
    'SetManagement/Edit.cshtml',
    'SetManagement/AddCard.cshtml',
    'SetManagement/EditCard.cshtml',
    'ListeningManagement/Editor.cshtml',
    'ListeningManagement/_TabInfo.cshtml',
    'NotificationManagement/Create.cshtml',
  ];
  for (const rel of owned) {
    assert.ok(
      !/_ValidationScriptsPartial/.test(read(adminView(rel))),
      `${rel} includes _ValidationScriptsPartial: jquery.validate throws "jQuery is not defined" without jQuery`,
    );
  }
});

// ---------------------------------------------------------------- accessible names

// Returns the opening tags (possibly multi-line) that match the given regex.
const tagsMatching = (source, tagRegex) => source.match(new RegExp(tagRegex.source, 'g')) || [];
const hasName = (tag) => /\baria-label="[^"]+"/.test(tag);

function assertNamed(rel, tagRegex, expectedCount) {
  const tags = tagsMatching(read(rel), tagRegex);
  assert.equal(tags.length, expectedCount, `${rel}: expected ${expectedCount} tags for ${tagRegex}, found ${tags.length}`);
  tags.forEach((tag) => assert.ok(hasName(tag), `${rel}: control without aria-label: ${tag.replace(/\s+/g, ' ').slice(0, 110)}`));
}

test('Folder detail: the three-dot buttons have an accessible name', () => {
  assertNamed('Views/Folder/FolderDetail.cshtml', /<button class="btn-circle-action"[^>]*>/, 1);
  assertNamed('Views/Folder/FolderDetail.cshtml', /<button class="btn-icon-more"[^>]*>/, 1);
});

// The vocabulary-set pages moved to the shared admin content kit (A3 of admin-content-complete): the
// filter controls are named by a <label for> inside _AcFilterBar, and every icon action carries an aria-label.
test('Admin Set list: search and filters are labelled through the shared filter bar, icon actions are named', () => {
  const index = read(adminView('SetManagement/Index.cshtml'));
  assert.match(index, /<partial name="_AcFilterBar"/);
  const bar = read(adminView('Shared/_AcFilterBar.cshtml'));
  assert.match(bar, /<label class="ac-label" for="@inputId"/);
  assert.equal((bar.match(/id="@inputId"/g) || []).length, 2, 'search input and select both carry the id the label points at');
  const iconActions = tagsMatching(index, /<(?:a|button)\b[^>]*class="(?:btn )?ac-icon-btn[^"]*"[^>]*>/);
  assert.ok(iconActions.length >= 3, 'the row actions are rendered with the icon-button class');
  iconActions.forEach((tag) => assert.ok(hasName(tag), `icon action without aria-label: ${tag.replace(/\s+/g, ' ').slice(0, 110)}`));
  assert.match(read(adminView('Shared/_AcConfirmDelete.cshtml')), /aria-label="@Model\.AriaLabel"/);
});

// The listening pages moved to the shared admin content kit (A5 of admin-content-complete): filters are labelled
// through _AcFilterBar, icon actions carry an aria-label, and every speaker field has a visible <label for>.
test('Admin Listening list: search and filters are labelled through the shared filter bar, icon actions are named', () => {
  const index = read(adminView('ListeningManagement/Index.cshtml'));
  assert.match(index, /<partial name="_AcFilterBar"/);
  const iconActions = tagsMatching(index, /<(?:a|button)\b[^>]*class="(?:btn )?ac-icon-btn[^"]*"[^>]*>/);
  assert.ok(iconActions.length >= 2, 'the row actions are rendered with the icon-button class');
  iconActions.forEach((tag) => assert.ok(hasName(tag), `icon action without aria-label: ${tag.replace(/\s+/g, ' ').slice(0, 110)}`));
});

test('Admin Listening form: the four speaker inputs are labelled', () => {
  const info = read(adminView('ListeningManagement/_TabInfo.cshtml'));
  ['Speaker1Name', 'Speaker1Country', 'Speaker2Name', 'Speaker2Country'].forEach((field) => {
    assert.match(info, new RegExp('<label class="ac-label" for="info-' + field + '"'), field + ' label');
    assert.match(info, new RegExp('<input id="info-' + field + '" name="' + field + '"'), field + ' input');
  });
});

// The payment order list moved to the shared admin content kit (A6 of admin-content-complete): its search and
// filters are labelled through _AcFilterBar. The Premium users list keeps its own aria-labelled controls.
test('Admin Billing lists: search and filter controls are named', () => {
  const index = read(adminView('BillingManagement/Index.cshtml'));
  assert.match(index, /<partial name="_AcFilterBar"/);
  ['q', 'provider', 'status', 'range'].forEach((name) => assert.match(index, new RegExp('Name = "' + name + '"')));
  assert.match(read(adminView('Shared/_AcFilterBar.cshtml')), /<label class="ac-label" for="@inputId"/);
  assert.match(index, /aria-label="Xem chi tiết đơn @order\.OrderCode"/);
  assertNamed(adminView('BillingManagement/PremiumUsers.cshtml'), /<input type="text" name="q"[^>]*>/, 1);
  assertNamed(adminView('BillingManagement/PremiumUsers.cshtml'), /<select name="filter"[^>]*>/, 1);
});

test('Admin notification form: labels point at the real input ids', () => {
  const view = read(adminView('NotificationManagement/Create.cshtml'));
  assert.match(view, /<label asp-for="Title"[^>]*\sfor="titleInput"/);
  assert.match(view, /<label asp-for="Message"[^>]*\sfor="messageInput"/);
});
