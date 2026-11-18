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
    'ListeningManagement/CreateEdit.cshtml',
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

test('Admin Listening list: search, filters and row menu are named', () => {
  assertNamed(adminView('ListeningManagement/Index.cshtml'), /<input type="text" name="q"[^>]*>/, 1);
  assertNamed(adminView('ListeningManagement/Index.cshtml'), /<select name="level"[^>]*>/, 1);
  assertNamed(adminView('ListeningManagement/Index.cshtml'), /<button class="btn btn-light btn-sm" type="button" data-bs-toggle="dropdown"[^>]*>/, 1);
});

// The owner picker is gone (system sets are always system-owned); the read-only field that remains is the set name.
test('Admin Set edit form: the read-only name field is labelled', () => {
  const form = read(adminView('SetManagement/_SetForm.cshtml'));
  assert.match(form, /<label class="ac-label" for="set-name"/);
  assert.match(form, /<input id="set-name" name="Name"[\s\S]*?readonly="@isEdit"/);
  assert.match(read(adminView('SetManagement/Edit.cshtml')), /<partial name="_SetForm"/);
});

test('Admin Listening form: the four speaker inputs are named', () => {
  assertNamed(adminView('ListeningManagement/CreateEdit.cshtml'), /<input asp-for="Speaker[12](Name|Country)"[^>]*>/, 4);
});

test('Admin Billing lists: search and filter controls are named', () => {
  const index = read(adminView('BillingManagement/Index.cshtml'));
  assert.ok(hasName(tagsMatching(index, /<input\s+type="text"\s+name="q"[^>]*>/)[0] || ''), 'Billing index search input');
  assertNamed(adminView('BillingManagement/Index.cshtml'), /<select name="(provider|status)"[^>]*>/, 2);
  assertNamed(adminView('BillingManagement/PremiumUsers.cshtml'), /<input type="text" name="q"[^>]*>/, 1);
  assertNamed(adminView('BillingManagement/PremiumUsers.cshtml'), /<select name="filter"[^>]*>/, 1);
});

test('Admin notification form: labels point at the real input ids', () => {
  const view = read(adminView('NotificationManagement/Create.cshtml'));
  assert.match(view, /<label asp-for="Title"[^>]*\sfor="titleInput"/);
  assert.match(view, /<label asp-for="Message"[^>]*\sfor="messageInput"/);
});
