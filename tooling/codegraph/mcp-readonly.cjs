'use strict';

// Fixed-release adapter: the normal upstream MCP startup syncs automatically.
// readOnly also skips migrations/watching/writer locks and background workers.
const path = require('node:path');
const [bundle, project] = process.argv.slice(2);
const source = path.join(bundle, 'lib', 'dist', 'mcp');
const { MCPEngine } = require(path.join(source, 'engine.js'));
const { MCPSession } = require(path.join(source, 'session.js'));
const { StdioTransport } = require(path.join(source, 'transport.js'));

const engine = new MCPEngine({ readOnly: true, watch: false, queryPool: false });
let closing = false;
async function close() {
  if (closing) return;
  closing = true;
  await engine.stop();
  process.exit(0);
}
const transport = new StdioTransport({ exitOnClose: false, onClose: () => { void close(); } });
const session = new MCPSession(transport, engine, { explicitProjectPath: project });
process.once('SIGTERM', () => { void close(); });
process.once('SIGINT', () => { void close(); });
session.start();
