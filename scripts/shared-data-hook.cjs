'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
function run(root = path.resolve(__dirname, '..'), env = process.env, spawn = spawnSync) {
  if (env.TCT_SHARED_SYNC_IN_PROGRESS === '1') return 0;
  if (!fs.existsSync(path.join(root, 'data', 'shared', 'catalog.json'))) return 0;
  const result = spawn('pwsh', ['-NoProfile', '-File', path.join(root, 'scripts', 'shared-data.ps1'), '-Action', 'Sync'],
    { cwd: root, env, stdio: 'inherit', windowsHide: true });
  if (result.error || result.status !== 0) {
    process.stderr.write('TCT: Git merge/pull completed, but local shared-data sync failed. Resolve prerequisites/conflicts and run scripts/shared-data.ps1; do not report data as synchronized.\n');
    return 1;
  }
  return 0;
}
module.exports = { run };
if (require.main === module) process.exitCode = run();
