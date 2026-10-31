'use strict';

// Project-local setup and launch only. No shell-evaluated event data or global installs.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawn, spawnSync } = require('node:child_process');
const ROOT = path.resolve(__dirname, '..');
const REASON = 'The owner requires one agent. Implement and review inline; do not delegate.';
const HOSTS = new Set(['codex', 'claude', 'cursor', 'antigravity']);
const EVENTS = new Set(['SessionStart', 'PreToolUse', 'PostToolUse', 'Stop']);
const DELEGATION = new Set(['Agent', 'Task', 'spawn_agent', 'followup_task',
  'collaboration.spawn_agent', 'collaboration.followup_task', 'invoke_subagent', 'define_subagent']);
const log = message => process.stderr.write(`TCT tools: ${message}\n`);
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8'));

function inside(root, target) {
  const relative = path.relative(path.resolve(root), path.resolve(target));
  if (!relative || relative.startsWith('..') || path.isAbsolute(relative)) {
    throw new Error('Runtime path must stay inside this checkout.');
  }
  let ancestor = path.resolve(target);
  while (!fs.existsSync(ancestor)) ancestor = path.dirname(ancestor);
  const physical = path.relative(fs.realpathSync(root), fs.realpathSync(ancestor));
  if (physical.startsWith('..') || path.isAbsolute(physical)) throw new Error('Runtime path crosses a link outside this checkout.');
  return target;
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: ROOT, encoding: 'utf8', windowsHide: true,
    stdio: ['pipe', 'pipe', 'pipe'], ...options });
  return result;
}

function checked(command, args, options = {}) {
  const result = run(command, args, options);
  if (result.error || result.status !== 0) {
    if (result.error?.code === 'ETIMEDOUT') throw new Error('Dependency installation timed out and was not marked complete. Retry the setup task or SETUP-AGENTS.cmd.');
    throw new Error(`${path.basename(command)} failed. ${result.error?.message || result.stderr?.trim() || `Exit ${result.status}`}`);
  }
  return result;
}

async function downloadVerified(entry, fetcher = fetch) {
  const response = await fetcher(entry.url, { signal: AbortSignal.timeout(120000) });
  if (!response.ok) throw new Error(`Runtime download failed (HTTP ${response.status}). Retry setup when online.`);
  const bytes = Buffer.from(await response.arrayBuffer());
  if (bytes.length !== entry.size || hash(bytes) !== entry.sha256) {
    throw new Error('Runtime archive integrity mismatch; nothing was extracted.');
  }
  return bytes;
}

async function withLock(root, key, action, { timeoutMs = 660000, pollMs = 150 } = {}) {
  if (!/^[a-z-]+$/.test(key)) throw new Error('Invalid installation lock.');
  const lock = inside(root, path.join(root, 'tooling', '.runtime', 'locks', `${key}.lock`));
  fs.mkdirSync(path.dirname(lock), { recursive: true });
  const token = crypto.randomUUID();
  const started = Date.now();
  for (;;) {
    try {
      fs.mkdirSync(lock);
      fs.writeFileSync(path.join(lock, 'owner.json'), JSON.stringify({ pid: process.pid, token }));
      break;
    } catch (error) {
      if (error.code !== 'EEXIST') throw error;
      try {
        const owner = readJson(path.join(lock, 'owner.json'));
        try { process.kill(owner.pid, 0); }
        catch (probe) {
          if (probe.code === 'ESRCH') {
            // Re-read the token so another startup's lock is never removed.
            if (readJson(path.join(lock, 'owner.json')).token === owner.token) {
              fs.rmSync(inside(root, lock), { recursive: true });
            }
            continue;
          }
        }
      } catch (readError) {
        if (readError.code !== 'ENOENT') throw readError;
      }
      if (Date.now() - started > timeoutMs) throw new Error('Another setup is still running. Wait for it, then retry.');
      await new Promise(resolve => setTimeout(resolve, pollMs));
    }
  }
  try { return await action(); }
  finally {
    if (readJson(path.join(lock, 'owner.json')).token === token) {
      fs.rmSync(inside(root, lock), { recursive: true });
    }
  }
}

function installGitHook(root, runner = run) {
  const current = runner('git', ['config', '--get', 'core.hooksPath'], { cwd: root });
  if (current.error || ![0, 1].includes(current.status)) throw new Error('Git is required to install the project hook.');
  const configured = (current.stdout || '').trim();
  if (configured && configured !== '.githooks') throw new Error(`Existing hooksPath (${configured}); combine hook managers explicitly.`);
  if (!configured) {
    const found = runner('git', ['rev-parse', '--path-format=absolute', '--git-path', 'hooks'], { cwd: root });
    if (found.status !== 0) throw new Error('Cannot inspect the Git hook directory.');
    const folder = found.stdout.trim();
    if (fs.existsSync(folder) && fs.readdirSync(folder).some(name => !name.endsWith('.sample') && fs.statSync(path.join(folder, name)).isFile())) {
      throw new Error('Existing Git hook files found; combine them explicitly before enabling .githooks.');
    }
  }
  if (process.platform !== 'win32') {
    for (const name of ['pre-commit', 'post-merge']) fs.chmodSync(path.join(root, '.githooks', name), 0o755);
  }
  if (configured === '.githooks') return;
  const result = runner('git', ['config', '--local', 'core.hooksPath', '.githooks'], { cwd: root });
  if (result.status !== 0) {
    const recheck = runner('git', ['config', '--get', 'core.hooksPath'], { cwd: root });
    if (recheck.status !== 0 || recheck.stdout.trim() !== '.githooks') throw new Error('Could not enable the project Git hooks.');
  }
}

function npmCommand() {
  const candidates = [path.join(path.dirname(process.execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js'),
    path.resolve(path.dirname(process.execPath), '..', 'lib', 'node_modules', 'npm', 'bin', 'npm-cli.js')];
  for (const directory of (process.env.PATH || '').split(path.delimiter)) {
    const npm = path.join(directory, process.platform === 'win32' ? 'npm.cmd' : 'npm');
    if (!fs.existsSync(npm)) continue;
    if (process.platform === 'win32') candidates.push(path.join(directory, 'node_modules', 'npm', 'bin', 'npm-cli.js'));
    else candidates.push(fs.realpathSync(npm));
  }
  const cli = candidates.find(file => fs.existsSync(file));
  if (!cli) throw new Error('Install Node.js 22+ with npm, reopen the editor, then double-click SETUP-AGENTS.cmd.');
  return cli;
}

function createSetup(root = ROOT) {
  root = path.resolve(root);
  const config = readJson(path.join(root, '.ai', 'tooling', 'bootstrap.lock.json'));
  if (Number(process.versions.node.split('.')[0]) < config.node_minimum_major) throw new Error('Node.js 22 or newer is required.');
  const runtime = inside(root, path.join(root, 'tooling', '.runtime'));
  const python = path.join(root, 'tooling', 'mcp', '.venv', process.platform === 'win32' ? 'Scripts/python.exe' : 'bin/python');
  const offline = process.env.TCT_AGENT_TOOLS_OFFLINE === '1';
  const env = { ...process.env, PYTHONUTF8: '1', UV_NO_CONFIG: '1', UV_PYTHON_PREFERENCE: 'only-managed', UV_PYTHON_INSTALL_DIR: path.join(runtime, 'python'),
    UV_CACHE_DIR: path.join(runtime, 'cache'), UV_NO_PROGRESS: '1' };
  const fingerprint = files => hash(Buffer.concat(files.map(file => fs.readFileSync(path.join(root, file)))));
  function ready(key, signature, files) {
    try {
      return readJson(path.join(runtime, `${key}.json`)).signature === signature && files.every(file => fs.existsSync(file));
    } catch { return false; }
  }
  function mark(key, signature) {
    fs.mkdirSync(runtime, { recursive: true });
    const file = path.join(runtime, `${key}.json`), temporary = `${file}.${process.pid}.tmp`;
    fs.writeFileSync(temporary, JSON.stringify({ signature }));
    fs.renameSync(temporary, file);
  }
  async function ensureUv() {
    const existing = run('uv', ['--version'], { cwd: root });
    if (existing.status === 0 && existing.stdout.startsWith(`uv ${config.uv.version} `)) return 'uv';
    const asset = config.uv.assets[`${process.platform}-${process.arch}`];
    if (!asset) throw new Error('No reviewed uv runtime for this platform. See docs/team-agent-setup.md.');
    const directory = inside(root, path.join(runtime, `uv-${config.uv.version}-${process.platform}-${process.arch}`));
    const executable = path.join(directory, asset.executable);
    if (fs.existsSync(executable) && run(executable, ['--version'], { cwd: root }).stdout?.startsWith(`uv ${config.uv.version} `)) return executable;
    if (offline) throw new Error('uv is not installed locally and offline mode is enabled. Run setup once while online.');
    log(`Downloading pinned uv ${config.uv.version}; verifying SHA-256.`);
    const bytes = await downloadVerified(asset);
    const temporary = inside(root, path.join(runtime, `download-${crypto.randomUUID()}`));
    fs.mkdirSync(temporary, { recursive: true });
    try {
      const archive = path.join(temporary, asset.url.endsWith('.zip') ? 'uv.zip' : 'uv.tar.gz');
      fs.writeFileSync(archive, bytes);
      const listing = checked('tar', ['-tf', archive], { cwd: root }).stdout.split(/\r?\n/).filter(Boolean);
      if (listing.some(name => path.isAbsolute(name) || name.split(/[\\/]/).includes('..'))) throw new Error('Unsafe runtime archive paths.');
      const extract = path.join(temporary, 'unpacked');
      fs.mkdirSync(extract);
      checked('tar', ['-xf', archive, '-C', extract], { cwd: root });
      if (!fs.existsSync(path.join(extract, asset.executable))) throw new Error('uv archive did not contain the reviewed executable.');
      if (fs.existsSync(directory)) fs.rmSync(inside(root, directory), { recursive: true });
      fs.renameSync(extract, directory);
      return executable;
    } finally { fs.rmSync(inside(root, temporary), { recursive: true, force: true }); }
  }
  async function ensurePython() {
    const signature = fingerprint(['.ai/tooling/bootstrap.lock.json', 'tooling/mcp/pyproject.toml', 'tooling/mcp/uv.lock']);
    return withLock(root, 'python', async () => {
      if (ready('python', signature, [python])) return python;
      const uv = await ensureUv();
      log('Restoring locked Python MCP dependencies (first use or lockfile changed).');
      checked(uv, ['sync', '--project', path.join(root, 'tooling', 'mcp'), '--python', config.python, '--locked', '--no-dev',
        ...(offline ? ['--offline'] : [])], { cwd: root, env, timeout: 180000 });
      if (!fs.existsSync(python)) throw new Error('Python environment was not created.');
      mark('python', signature);
      return python;
    });
  }
  async function ensureCodegraph() {
    const folder = path.join(root, 'tooling', 'codegraph');
    const signature = fingerprint(['tooling/codegraph/package.json', 'tooling/codegraph/package-lock.json']);
    const bundle = path.join(folder, 'node_modules', '@colbymchenry', `codegraph-${process.platform}-${process.arch}`);
    const binary = path.join(bundle, process.platform === 'win32' ? 'node.exe' : 'node');
    return withLock(root, 'codegraph', async () => {
      if (ready('codegraph', signature, [binary, path.join(bundle, 'lib', 'dist', 'mcp', 'engine.js')])) return;
      log('Restoring pinned CodeGraph packages; no index is created or synchronized.');
      checked(process.execPath, [npmCommand(), 'ci', '--prefix', folder, '--ignore-scripts', '--no-audit', '--no-fund',
        ...(offline ? ['--offline'] : [])], { cwd: root, timeout: 600000 });
      if (!fs.existsSync(binary)) throw new Error('CodeGraph has no bundle for this platform.');
      mark('codegraph', signature);
    });
  }
  return { root, python, ensurePython, ensureCodegraph };
}

function fastHook(host, name, payload, singleAgent = true) {
  if (!HOSTS.has(host) || !EVENTS.has(name)) throw new Error('Unsupported hook host or event.');
  const tool = host === 'antigravity' ? payload.toolCall?.name : payload.tool_name || (host === 'cursor' && payload.subagent_id ? 'Agent' : '');
  if (!singleAgent || name !== 'PreToolUse' || !DELEGATION.has(tool)) return {};
  if (host === 'cursor') return { permission: 'deny', user_message: REASON };
  if (host === 'antigravity') return { decision: 'deny', reason: REASON };
  return { hookSpecificOutput: { hookEventName: name, permissionDecision: 'deny', permissionDecisionReason: REASON } };
}

function start(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd: ROOT, stdio: 'inherit', windowsHide: true, ...options });
    child.once('error', reject);
    child.once('exit', code => resolve(code ?? 1));
    // Stop the actual stdio server as well when its host stops the launcher.
    const stop = () => child.kill();
    process.once('SIGTERM', stop);
    process.once('SIGINT', stop);
    child.once('close', () => { process.removeListener('SIGTERM', stop); process.removeListener('SIGINT', stop); });
  });
}

async function main(args) {
  const [action, ...rest] = args;
  if (action === 'hook') {
    const [host, name] = rest;
    // No dependency downloads in a lifecycle hook, even on a fresh clone.
    const input = fs.readFileSync(0, 'utf8');
    if (Buffer.byteLength(input) > 1024 * 1024) throw new Error('Hook payload too large.');
    const payload = JSON.parse(input);
    if (!payload || Array.isArray(payload) || typeof payload !== 'object') throw new Error('Expected a hook event object.');
    const singleAgent = /^\s*multi_agent\s*=\s*false\s*$/m.test(fs.readFileSync(path.join(ROOT, '.codex', 'config.toml'), 'utf8'));
    const immediate = fastHook(host, name, payload, singleAgent);
    if (Object.keys(immediate).length) { process.stdout.write(JSON.stringify(immediate)); return 0; }
    const setup = createSetup();
    if (fs.existsSync(setup.python)) {
      const result = run(setup.python, [path.join(ROOT, 'scripts', 'agent_lifecycle.py'), name, '--host', host],
        { input, cwd: ROOT, env: { ...process.env, PYTHONUTF8: '1' }, timeout: 20000 });
      if (result.status !== 0) throw new Error('Lifecycle validation failed to run. Run the TCT setup task.');
      process.stdout.write(result.stdout);
      if (result.stderr) process.stderr.write(result.stderr);
      return 0;
    }
    if (name === 'SessionStart') {
      const message = 'TCT English: reuse AGENTS.md; one agent implements and reviews. Read .ai/README.md only for routing/setup questions. Setup is pending: allow the TCT folder-open task or double-click SETUP-AGENTS.cmd. MCP first use restores locked dependencies. Do not index CodeGraph automatically.';
      const output = host === 'cursor' ? { additional_context: message } : host === 'antigravity'
        ? { injectSteps: [{ ephemeralMessage: message }] } : { hookSpecificOutput: { hookEventName: name, additionalContext: message } };
      process.stdout.write(JSON.stringify(output));
    } else {
      if (name === 'Stop') log('Tooling validation pending: run the TCT setup task or SETUP-AGENTS.cmd.');
      process.stdout.write(JSON.stringify(host === 'antigravity' && name === 'PreToolUse' ? { decision: 'allow' }
        : host === 'antigravity' && name === 'Stop' ? { decision: 'stop' } : {}));
    }
    return 0;
  }
  const setup = createSetup();
  if (action === 'doctor' && rest.every(arg => arg === '--json')) {
    if (!fs.existsSync(setup.python)) throw new Error('Doctor needs the locked project Python runtime. Run SETUP-AGENTS.cmd first; doctor never installs it.');
    return start(setup.python, [path.join(ROOT, 'scripts', 'agent_doctor.py'), ...rest], { env: { ...process.env, PYTHONUTF8: '1' } });
  }
  if (action === 'setup') {
    await setup.ensurePython();
    await setup.ensureCodegraph();
    installGitHook(ROOT);
    checked(setup.python, [path.join(ROOT, 'scripts', 'agent_tooling.py')]);
    log('Setup complete. Skills/rules/config are shared; editor trust stays local.');
    log(fs.existsSync(path.join(ROOT, '.codegraph', 'codegraph.db'))
      ? 'Existing CodeGraph index preserved; no index/sync requested.'
      : 'CodeGraph index is missing. Choose the explicit Create CodeGraph index task if the team wants one.');
    return 0;
  }
  if (action === 'validate') {
    if (rest.some(arg => arg !== '--staged')) throw new Error('Unknown validation argument.');
    if (!fs.existsSync(setup.python)) throw new Error('Run the TCT setup task or double-click SETUP-AGENTS.cmd first.');
    return start(setup.python, [path.join(ROOT, 'scripts', 'agent_tooling.py'), ...rest], { env: { ...process.env, PYTHONUTF8: '1' } });
  }
  if (action === 'mcp' && rest.length === 1 && ['git', 'codegraph'].includes(rest[0])) {
    if (rest[0] === 'git') await setup.ensurePython();
    else await setup.ensureCodegraph();
    try { installGitHook(ROOT); } catch (error) { log(error.message); }
    return rest[0] === 'git'
      ? start(setup.python, [path.join(ROOT, 'tooling', 'mcp', 'git_readonly.py')], { env: { ...process.env, PYTHONUTF8: '1' } })
      : start(process.execPath, [path.join(ROOT, 'tooling', 'codegraph', 'launch.cjs'), '--mcp-project']);
  }
  if (action === 'index' && rest.length === 0) {
    await setup.ensureCodegraph();
    log('Explicit index action selected. Rebuilding this checkout\'s CodeGraph index.');
    return start(process.execPath, [path.join(ROOT, 'tooling', 'codegraph', 'launch.cjs'), 'index', ROOT]);
  }
  throw new Error('Choose doctor [--json], setup, validate [--staged], mcp git, mcp codegraph, or explicit index.');
}

module.exports = { downloadVerified, withLock, installGitHook, fastHook, createSetup, main };
if (require.main === module) {
  main(process.argv.slice(2)).then(code => { process.exitCode = code; }).catch(error => {
    // Hook errors are deliberately generic; never echo payload or transcript data.
    log(process.argv[2] === 'hook' ? 'Hook could not run. Check local setup and hook configuration.' : error.message);
    process.exitCode = 1;
  });
}
