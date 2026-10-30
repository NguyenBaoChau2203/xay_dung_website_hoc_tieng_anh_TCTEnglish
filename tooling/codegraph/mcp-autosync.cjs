'use strict';
const fs = require('node:fs');
const path = require('node:path');

// The upstream daemon owns coordination/index writes. This gate only narrows
// host requests to the approved checkout and the single exposed read tool.
function restrictRequest(message, project) {
  if (!message || typeof message !== 'object' || Array.isArray(message)) {
    throw new Error('MCP requests must be individual JSON-RPC messages.');
  }
  if (message?.method !== 'tools/call') return message;
  if (message.params?.name !== 'codegraph_explore') {
    throw new Error('Only codegraph_explore is available in this MCP.');
  }
  const root = fs.realpathSync(project);
  const args = message.params.arguments || {};
  const supplied = args.projectPath;
  if (supplied != null) {
    if (typeof supplied !== 'string') throw new Error('projectPath must stay inside the approved checkout.');
    let actual;
    try { actual = fs.realpathSync(path.resolve(root, supplied)); }
    catch { throw new Error('projectPath must stay inside the approved checkout.'); }
    const relative = path.relative(root, actual);
    if (relative.startsWith('..') || path.isAbsolute(relative)) {
      throw new Error('projectPath must stay inside the approved checkout.');
    }
  }
  return { ...message, params: { ...message.params, arguments: { ...args, projectPath: root } } };
}

function restrictToolList(message) {
  if (!Array.isArray(message?.result?.tools)) return message;
  return { ...message, result: { ...message.result,
    tools: message.result.tools.filter(tool => tool.name === 'codegraph_explore') } };
}

function main() {
  const { spawn } = require('node:child_process');
  const readline = require('node:readline');
  const [bundle, project] = process.argv.slice(2);
  const child = spawn(process.execPath, ['--liftoff-only', '--disable-warning=ExperimentalWarning',
    path.join(bundle, 'lib/dist/bin/codegraph.js'), 'serve', '--mcp', '--path', project], {
    cwd: project, windowsHide: true, stdio: ['pipe', 'pipe', 'inherit'],
    env: { ...process.env, CODEGRAPH_MCP_TOOLS: 'explore', CODEGRAPH_NO_DAEMON: '0' },
  });
  // A daemon may have been started by a different client with a wider tool list.
  // Enforce this gateway's discovery contract independently of daemon env.
  const output = readline.createInterface({ input: child.stdout, terminal: false });
  output.on('line', line => {
    let filtered = line;
    try {
      const message = JSON.parse(line);
      const narrowed = restrictToolList(message);
      if (narrowed !== message) filtered = JSON.stringify(narrowed);
    } catch { /* preserve non-JSON backend diagnostics rather than rewrite data */ }
    process.stdout.write(filtered + '\n');
  });
  const input = readline.createInterface({ input: process.stdin, terminal: false });
  input.on('line', line => {
    if (!line.trim()) return;
    let message;
    try {
      if (line.length > 4 * 1024 * 1024) throw new Error('MCP request is too large.');
      message = JSON.parse(line);
      child.stdin.write(JSON.stringify(restrictRequest(message, project)) + '\n');
    } catch (error) {
      process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: message?.id ?? null,
        error: { code: -32602, message: error instanceof SyntaxError ? 'Invalid MCP JSON.' : error.message } }) + '\n');
    }
  });
  input.on('close', () => { if (!child.stdin.destroyed) child.stdin.end(); });
  child.stdin.on('error', () => { input.close(); process.stdin.destroy(); });
  child.on('error', () => {
    process.stderr.write('TCT CodeGraph: could not start the pinned auto-sync server.\n');
    input.close(); process.stdin.destroy(); process.exitCode = 1;
  });
  child.on('close', code => { input.close(); process.stdin.destroy(); process.exitCode = code ?? 1; });
  for (const signal of ['SIGTERM', 'SIGINT']) process.once(signal, () => { input.close(); child.kill(signal); });
  process.stdout.on('error', () => { input.close(); child.kill(); });
}

module.exports = { restrictRequest, restrictToolList };
if (require.main === module) main();
