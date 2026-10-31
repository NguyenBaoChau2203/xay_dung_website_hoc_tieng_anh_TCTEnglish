'use strict';
const fs = require('node:fs');
const path = require('node:path');

// Local approval is never imported from a dependency or copied to another checkout.
// This selects an existing index's incremental updater; it never creates/rebuilds it.
function readLocalPolicy(project, expectedVersion) {
  const root = fs.realpathSync(project);
  const file = path.join(root, '.ai', 'codegraph.local.json');
  const readOnly = { autoSync: false, debounceMs: 2000 };
  if (!fs.existsSync(file)) return readOnly;
  const actual = fs.realpathSync(file);
  const relative = path.relative(root, actual);
  if (relative.startsWith('..') || path.isAbsolute(relative)) throw new Error('Local CodeGraph policy must stay inside this checkout.');
  if (fs.statSync(actual).size > 8192) throw new Error('Local CodeGraph policy is too large.');
  let policy;
  try { policy = JSON.parse(fs.readFileSync(actual, 'utf8')); }
  catch { throw new Error('Local CodeGraph policy is not valid JSON.'); }
  if (!policy || policy.schemaVersion !== 1) throw new Error('Local CodeGraph policy schema is unsupported.');
  if (policy.mode === 'read-only') return readOnly;
  if (policy.mode !== 'auto-sync') throw new Error('Local CodeGraph policy mode is unsupported.');
  if (typeof policy.checkoutRoot !== 'string' || !path.isAbsolute(policy.checkoutRoot)
      || path.relative(root, path.resolve(policy.checkoutRoot)) !== '') {
    throw new Error('Local CodeGraph policy belongs to another checkout.');
  }
  if (policy.reviewedVersion !== expectedVersion) throw new Error('Local CodeGraph policy needs approval for this package version.');
  if (!fs.existsSync(path.join(root, '.codegraph', 'codegraph.db'))) {
    throw new Error('Create the CodeGraph index explicitly before enabling auto-sync.');
  }
  const debounceMs = policy.debounceMs ?? 2000;
  if (!Number.isInteger(debounceMs) || debounceMs < 100 || debounceMs > 60000) {
    throw new Error('Local CodeGraph debounce must be an integer from 100 to 60000 ms.');
  }
  return { autoSync: true, debounceMs };
}

module.exports = { readLocalPolicy };
