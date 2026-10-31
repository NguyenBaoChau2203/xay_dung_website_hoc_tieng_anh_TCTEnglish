'use strict';

// Resolve only installed, pinned packages. Never run npx or a download fallback.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

try {
  const spec = JSON.parse(fs.readFileSync(path.join(__dirname, 'package.json'), 'utf8'));
  const expected = spec.dependencies['@colbymchenry/codegraph'];
  if (!/^\d+\.\d+\.\d+$/.test(expected)) throw new Error('CodeGraph needs an exact reviewed version.');
  const moduleRoot = path.join(__dirname, 'node_modules', '@colbymchenry');
  const packageDir = path.join(moduleRoot, 'codegraph');
  const bundle = path.join(moduleRoot, `codegraph-${process.platform}-${process.arch}`);
  for (const directory of [packageDir, bundle]) {
    const metadata = JSON.parse(fs.readFileSync(path.join(directory, 'package.json'), 'utf8'));
    if (metadata.version !== expected) throw new Error('Installed CodeGraph differs from the project pin. Run npm ci --prefix tooling/codegraph --ignore-scripts.');
  }
  const runtime = path.join(bundle, process.platform === 'win32' ? 'node.exe' : 'node');
  const project = path.resolve(__dirname, '..', '..');
  const supplied = process.argv.slice(2);
  const projectMode = supplied[0] === '--mcp-project';
  const policy = projectMode ? require('./project-policy.cjs').readLocalPolicy(project, expected) : { autoSync: false };
  const readOnly = supplied[0] === '--mcp-readonly' || (projectMode && !policy.autoSync);
  const entry = readOnly ? path.join(__dirname, 'mcp-readonly.cjs')
    : projectMode ? path.join(__dirname, 'mcp-autosync.cjs')
    : path.join(bundle, 'lib', 'dist', 'bin', 'codegraph.js');
  const args = readOnly ? [bundle, project]
    : projectMode ? [bundle, project]
    : supplied.length ? supplied : ['--help'];
  if (policy.autoSync) process.stderr.write(`TCT CodeGraph: checkout auto-sync enabled (${expected}; ${policy.debounceMs}ms debounce).\n`);
  const result = spawnSync(runtime, ['--liftoff-only', '--disable-warning=ExperimentalWarning', entry, ...args], {
    cwd: project,
    stdio: 'inherit',
    windowsHide: true,
    env: {
      ...process.env,
      CODEGRAPH_TELEMETRY: '0',
      DO_NOT_TRACK: '1',
      CODEGRAPH_NO_DOWNLOAD: '1',
      CODEGRAPH_NO_UPDATE_CHECK: '1',
      CODEGRAPH_MCP_TOOLS: 'explore',
      ...(policy.autoSync ? { CODEGRAPH_WATCH_DEBOUNCE_MS: String(policy.debounceMs), CODEGRAPH_NO_DAEMON: '0' } : {}),
    },
  });
  if (result.error) throw result.error;
  process.exit(result.status === null ? 1 : result.status);
} catch (error) {
  process.stderr.write(`TCT CodeGraph: ${error.message}\n`);
  process.exit(1);
}
