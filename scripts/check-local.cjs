#!/usr/bin/env node
// Read-only prerequisite checks; no installers, credentials, containers or AI calls.
'use strict';
const { execFileSync, spawnSync } = require('node:child_process');
const { existsSync } = require('node:fs');
const { join } = require('node:path');

const checks = [];
function read(command, args) {
  try { return execFileSync(command, args, { encoding: 'utf8', timeout: 15000, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] }).trim(); }
  catch { return ''; }
}
function check(name, ok, help) { checks.push({ name, ok: Boolean(ok), help }); }
check('Windows x64', process.platform === 'win32' && process.arch === 'x64', 'Whisper/FFmpeg local pins currently support Windows x64.');
check('Node.js 22+', Number(process.versions.node.split('.')[0]) >= 22, 'Install Node.js 22+ with npm: https://nodejs.org/en/download');
check('Git', read('git', ['--version']), 'Install Git: https://git-scm.com/downloads/win');
const npmPresent = process.platform === 'win32'
  ? spawnSync('where.exe', ['npm.cmd'], { windowsHide: true, stdio: 'ignore', timeout: 5000 }).status === 0
  : Boolean(read('npm', ['--version']));
check('npm', npmPresent, 'npm is included in the Node.js installer; reopen the IDE after installation.');
const psVersion = read('pwsh', ['-NoProfile', '-Command', '$PSVersionTable.PSVersion.Major']);
check('PowerShell 7+', Number(psVersion) >= 7, 'Install PowerShell 7: https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows');
check('Visual C++ x64 runtime', process.platform === 'win32' && ['vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll'].every(name => existsSync(join(process.env.SystemRoot || 'C:\\Windows', 'System32', name))), 'Whisper native Windows build needs the Visual C++ x64 Redistributable: https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist');
const sdks = read('dotnet', ['--list-sdks']);
const runtimes = read('dotnet', ['--list-runtimes']);
check('.NET SDK 9+', sdks.split(/\r?\n/).some(line => Number(line.match(/^(\d+)\./)?.[1]) >= 9), 'Install .NET SDK 9+; the app still targets net9.0: https://dotnet.microsoft.com/download/dotnet/9.0');
check('ASP.NET Core runtime 9+', runtimes.split(/\r?\n/).some(line => /^Microsoft\.AspNetCore\.App (?:9|[1-9]\d+)\./.test(line)), 'SDK 9 includes the runtime; SDK 10 can use this app\'s LatestMajor roll-forward.');
check('Docker CLI + Compose', read('docker', ['compose', 'version']), 'Install Docker Desktop: https://docs.docker.com/desktop/setup/install/windows-install/');
check('Docker Linux engine', read('docker', ['info', '--format', '{{.OSType}}']) === 'linux', 'Start Docker Desktop with WSL2 / Linux containers, then retry. Do not reset other project containers.');
for (const item of checks) console.log(`${item.ok ? 'OK' : 'MISSING'} ${item.name}${item.ok ? '' : ': ' + item.help}`);
if (checks.every(item => item.ok)) {
  console.log('Prerequisites ready. Run SETUP-AGENTS.cmd once, then SETUP-LOCAL.cmd. Set the app key locally with scripts/set-local-ai-key.ps1.');
} else console.log('Install/start only the missing prerequisites, reopen the IDE, then run node scripts/check-local.cjs again. SSMS is not required.');
process.exitCode = checks.every(item => item.ok) ? 0 : 1;
