/**
 * test_ai_chat_clay_ui_and_logo.cjs
 * Verifies AI Chat original logo assets, Claymorphism token adoption,
 * responsive markup, and reduced-motion alternate behavior across all surfaces.
 */

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '../..');

test('Logo deliverables: vector master, monochrome variant, 512px PNG, and identity guide', () => {
  const masterSvgPath = path.join(repoRoot, 'TCTEnglish/wwwroot/images/ai/tct-ai-mark.svg');
  const monoSvgPath = path.join(repoRoot, 'TCTEnglish/wwwroot/images/ai/tct-ai-mark-mono.svg');
  const pngPath = path.join(repoRoot, 'TCTEnglish/wwwroot/images/ai/tct-ai-mark.png');
  const guidePath = path.join(repoRoot, 'docs/design-system/ai-chatbot-identity.md');

  assert.ok(fs.existsSync(masterSvgPath), 'Master SVG asset must exist');
  assert.ok(fs.existsSync(monoSvgPath), 'Monochrome SVG asset must exist');
  assert.ok(fs.existsSync(pngPath), '512px transparent PNG export must exist');
  assert.ok(fs.existsSync(guidePath), 'Vietnamese identity guide must exist');

  const masterSvg = fs.readFileSync(masterSvgPath, 'utf8');
  assert.match(masterSvg, /viewBox="0 0 128 128"/, 'Master SVG must have viewBox 0 0 128 128');
  assert.match(masterSvg, /#4255ff/i, 'Master SVG must use primary brand blue');
  assert.match(masterSvg, /#ffcd1f/i, 'Master SVG must use brand accent gold');
  assert.doesNotMatch(masterSvg, /<script/i, 'SVG must be clean vector without scripts');
  assert.doesNotMatch(masterSvg, /foreignObject/i, 'SVG must not contain foreignObject');

  const monoSvg = fs.readFileSync(monoSvgPath, 'utf8');
  assert.match(monoSvg, /viewBox="0 0 128 128"/, 'Monochrome SVG must have viewBox 0 0 128 128');
  assert.match(monoSvg, /currentColor/i, 'Monochrome SVG must use currentColor');

  const pngBuf = fs.readFileSync(pngPath);
  assert.ok(pngBuf.length > 10000, 'PNG must have substantive raster data');
  const width = pngBuf.readUInt32BE(16);
  const height = pngBuf.readUInt32BE(20);
  assert.equal(width, 512, 'PNG export width must be 512px');
  assert.equal(height, 512, 'PNG export height must be 512px');

  const guide = fs.readFileSync(guidePath, 'utf8');
  assert.match(guide, /TCT Companion Beacon/i, 'Guide documents the chosen design direction');
  assert.match(guide, /24px/i, 'Guide specifies 24px minimum usable size');
  assert.match(guide, /Khoảng cách an toàn/i, 'Guide specifies clear space');
});

test('AC01 Full Chat & Partials: Clay theme wrapper, logo integration, and accessible shell', () => {
  const chatView = fs.readFileSync(path.join(repoRoot, 'TCTEnglish/Views/Ai/Chat.cshtml'), 'utf8');
  const shellPartial = fs.readFileSync(path.join(repoRoot, 'TCTEnglish/Views/Ai/_ChatShell.cshtml'), 'utf8');

  assert.match(chatView, /class="[^"]*tct-theme[^"]*ai-chat-page[^"]*"/, 'Chat view wraps page in tct-theme');
  assert.match(chatView, /class="modal-content tct-theme"/, 'Delete modal wraps content in tct-theme');
  assert.match(chatView, /data-ai-history-root/, 'Preserves history root attribute');
  assert.match(chatView, /id="confirmDeleteConversationBtn"/, 'Preserves delete confirmation button');

  assert.match(shellPartial, /var aiAvatar = "\/images\/ai\/tct-ai-mark\.svg";/, 'Shell references new vector mark');
  assert.match(shellPartial, /data-ai-avatar="@aiAvatar"/, 'Shell exposes data-ai-avatar attribute');
  assert.match(shellPartial, /ai-chat-header-mark/, 'Shell header renders brand logo mark');
  assert.match(shellPartial, /id="messageInput"/, 'Preserves message textarea ID');
  assert.match(shellPartial, /id="sendBtn"/, 'Preserves send button ID');
  assert.match(shellPartial, /id="stopBtn"/, 'Preserves stop button ID');
  assert.match(shellPartial, /id="scrollToBottomBtn"/, 'Preserves scroll to bottom button ID');
});

test('AC02 Embedded Chat: Standalone document loads tokens and scopes theme', () => {
  const embedView = fs.readFileSync(path.join(repoRoot, 'TCTEnglish/Views/Ai/ChatEmbed.cshtml'), 'utf8');

  assert.match(embedView, /href="~\/css\/tct-learning-ui\.css"/, 'ChatEmbed explicitly loads shared design token stylesheet');
  assert.match(embedView, /class="[^"]*tct-theme[^"]*ai-chat-embed-body[^"]*"/, 'ChatEmbed body is scoped with tct-theme');
  assert.match(embedView, /href="~\/css\/ai-chat\.css"/, 'ChatEmbed loads feature CSS');
});

test('AC03 Floating Launcher: Theme wrapper, mark trigger, and accessible header', () => {
  const launcherPartial = fs.readFileSync(path.join(repoRoot, 'TCTEnglish/Views/Shared/_AiChatLauncher.cshtml'), 'utf8');

  assert.match(launcherPartial, /class="[^"]*tct-theme[^"]*ai-launcher[^"]*"/, 'Launcher container scoped with tct-theme');
  assert.match(launcherPartial, /img src="~\/images\/ai\/tct-ai-mark\.svg"[^>]*class="ai-launcher-icon"/, 'Trigger renders new mark');
  assert.match(launcherPartial, /img src="~\/images\/ai\/tct-ai-mark\.svg"[^>]*class="ai-launcher-header-mark"/, 'Panel header renders new mark');
  assert.match(launcherPartial, /data-ai-toggle/, 'Preserves trigger toggle hook');
  assert.match(launcherPartial, /data-ai-frame/, 'Preserves lazy iframe hook');
  assert.match(launcherPartial, /data-ai-close/, 'Preserves close button hook');
});

test('AA01 Admin AI Management: Scoped clay theme, status cards, and operation handlers', () => {
  const adminView = fs.readFileSync(path.join(repoRoot, 'TCTEnglish/Areas/Admin/Views/AiManagement/Index.cshtml'), 'utf8');
  const adminCss = fs.readFileSync(path.join(repoRoot, 'TCTEnglish/wwwroot/css/admin-ai-management.css'), 'utf8');

  assert.match(adminView, /class="[^"]*tct-theme[^"]*admin-ai-management-page[^"]*"/, 'Admin view scoped with tct-theme');
  assert.match(adminView, /href="~\/css\/admin-ai-management\.css"/, 'Admin view links admin-ai-management.css');
  assert.match(adminView, /id="retrievalIndexForm"/, 'Preserves BM25 index form');
  assert.match(adminView, /id="vectorIndexForm"/, 'Preserves vector index form');
  assert.match(adminView, /id="trainForm"/, 'Preserves ML.NET train form');

  assert.match(adminCss, /\.admin-ai-management-page/, 'Admin CSS scopes selectors');
  assert.match(adminCss, /--tct-clay-shadow/, 'Admin CSS consumes clay tokens');
  assert.match(adminCss, /@media \(prefers-reduced-motion: reduce\)/, 'Admin CSS provides reduced motion alternate');
});

test('CSS & JS Motion: Clay depth, bubble transitions, and reduced motion fallback', () => {
  const chatCss = fs.readFileSync(path.join(repoRoot, 'TCTEnglish/wwwroot/css/ai-chat.css'), 'utf8');
  const launcherCss = fs.readFileSync(path.join(repoRoot, 'TCTEnglish/wwwroot/css/ai-chat-launcher.css'), 'utf8');
  const chatJs = fs.readFileSync(path.join(repoRoot, 'TCTEnglish/wwwroot/js/ai-chat.js'), 'utf8');

  assert.match(chatCss, /--tct-clay-shadow/, 'Chat CSS consumes clay shadow tokens');
  assert.match(chatCss, /@keyframes ai-bubble-appear/, 'Chat CSS defines purposeful bubble entry animation');
  assert.match(chatCss, /@media \(prefers-reduced-motion: reduce\)/, 'Chat CSS has reduced-motion block');
  assert.match(chatCss, /scroll-behavior: auto !important;/, 'Chat CSS disables smooth scroll in reduced-motion');

  assert.match(launcherCss, /--tct-clay-shadow-lift/, 'Launcher CSS uses clay lift on trigger');
  assert.match(launcherCss, /--tct-clay-shadow-overlay/, 'Launcher CSS uses clay overlay on panel');
  assert.match(launcherCss, /@media \(prefers-reduced-motion: reduce\)/, 'Launcher CSS has reduced-motion block');

  assert.match(chatJs, /prefers-reduced-motion: reduce/, 'Chat JS checks prefers-reduced-motion in scrollToBottom');
  assert.match(chatJs, /\/images\/ai\/tct-ai-mark\.svg/, 'Chat JS references new mark as default assistant avatar');
});
