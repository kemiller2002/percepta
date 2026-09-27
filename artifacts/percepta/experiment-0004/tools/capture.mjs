// Layer 2: generic rendered-evidence capture, identical for every candidate x held-out case.
// Usage: node capture.mjs <scratch> <outDir>
// Reads $SCRATCH/staging/<candidate>/index.html and the verbatim held-out fixtures; never edits candidates.
import { chromium } from '/opt/node22/lib/node_modules/playwright/index.mjs';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';

const [S, OUT] = process.argv.slice(2);
const FIX = '/home/user/percepta/artifacts/percepta/experiment-0004/held-out-fixtures';
const CANDIDATES = ['candidate-1', 'candidate-2', 'candidate-3', 'candidate-4'];
const CASES = ['combined-blocker-falsification', 'pending-unverified', 'all-constraints', 'legal-with-history', 'illegal-without-known-blocker'];
const VIEWPORTS = [
  { name: 'desktop-1440x900', width: 1440, height: 900 },
  { name: 'landscape-1180x820', width: 1180, height: 820 },
  { name: 'portrait-820x1180', width: 820, height: 1180 },
  { name: 'phone-390x844', width: 390, height: 844 },
];
const DESKTOP = VIEWPORTS[0];
const SETTLE_MS = 300;
const EXE = '/opt/pw-browsers/chromium_headless_shell-1194/chrome-linux/headless_shell';

const writeJson = (path, value) => writeFileSync(path, JSON.stringify(value, null, 2) + '\n');
const lineDiff = (before, after) => {
  const b = new Set(before.split('\n')), a = new Set(after.split('\n'));
  return {
    added: [...a].filter((l) => !b.has(l)).slice(0, 80),
    removed: [...b].filter((l) => !a.has(l)).slice(0, 80),
  };
};

// In-page helpers (run in the browser). Controls are enumerated in document order.
const CONTROL_SELECTOR = 'button, a[href], input, select, textarea, summary, [role="button"], [role="link"], [role="tab"], [tabindex]:not([tabindex="-1"])';
function inventory(selector) {
  const isVisible = (el) => {
    const s = getComputedStyle(el);
    return s.display !== 'none' && s.visibility !== 'hidden' && el.getClientRects().length > 0;
  };
  const text = (el) => (el?.innerText || el?.textContent || '').replace(/\s+/g, ' ').trim();
  const describedBy = (el) => (el.getAttribute('aria-describedby') || '').split(/\s+/).filter(Boolean)
    .map((id) => text(document.getElementById(id))).filter(Boolean).join(' | ');
  const name = (el) => (el.getAttribute('aria-label')
    || (el.getAttribute('aria-labelledby') || '').split(/\s+/).map((id) => text(document.getElementById(id))).join(' ').trim()
    || text(el) || el.getAttribute('title') || el.getAttribute('value') || '').slice(0, 200);
  return [...document.querySelectorAll(selector)].map((el, index) => ({
    index,
    tag: el.tagName.toLowerCase(),
    role: el.getAttribute('role'),
    accessibleName: name(el),
    visible: isVisible(el),
    disabled: el.disabled === true,
    ariaDisabled: el.getAttribute('aria-disabled'),
    ariaExpanded: el.getAttribute('aria-expanded'),
    href: el.getAttribute('href'),
    describedByText: describedBy(el).slice(0, 400),
    isConfirmRootCauseControl: el.getAttribute('data-percepta-capability') === 'confirm-root-cause',
  }));
}
function confirmControl() {
  const text = (el) => (el?.innerText || el?.textContent || '').replace(/\s+/g, ' ').trim();
  const els = [...document.querySelectorAll('[data-percepta-capability="confirm-root-cause"]')];
  return els.map((el) => {
    const s = getComputedStyle(el);
    const container = el.closest('section, article, aside, fieldset, form, li, [role="region"], [role="group"]') || el.parentElement;
    return {
      tag: el.tagName.toLowerCase(),
      visibleText: text(el).slice(0, 300),
      ariaLabel: el.getAttribute('aria-label'),
      visible: s.display !== 'none' && s.visibility !== 'hidden' && el.getClientRects().length > 0,
      disabled: el.disabled === true,
      ariaDisabled: el.getAttribute('aria-disabled'),
      describedByText: (el.getAttribute('aria-describedby') || '').split(/\s+/).filter(Boolean)
        .map((id) => text(document.getElementById(id))).join(' | ').slice(0, 600),
      enclosingContainerText: text(container).slice(0, 1500),
    };
  });
}
function landing() {
  const text = (el) => (el?.innerText || el?.textContent || '').replace(/\s+/g, ' ').trim();
  const a = document.activeElement;
  const inView = [...document.querySelectorAll('h1, h2, h3, h4')]
    .filter((h) => { const r = h.getBoundingClientRect(); return r.bottom > 0 && r.top < innerHeight && r.width > 0; })
    .map((h) => text(h).slice(0, 120));
  return {
    scrollY: Math.round(scrollY),
    focused: a && a !== document.body ? { tag: a.tagName.toLowerCase(), text: text(a).slice(0, 300) } : null,
    headingsInViewport: inView,
    hash: location.hash,
  };
}

const openInState = async (browser, url, state, vp) => {
  const page = await browser.newPage({ viewport: { width: vp.width, height: vp.height } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(`pageerror: ${e.message}`));
  page.on('console', (m) => m.type() === 'error' && errors.push(`console: ${m.text()}`));
  await page.goto(url, { waitUntil: 'load' });
  const protocol = await page.evaluate((s) => {
    if (typeof window.__perceptaSetState !== 'function') return 'missing';
    window.__perceptaSetState(s);
    return 'called';
  }, state);
  await page.waitForTimeout(SETTLE_MS);
  return { page, errors, protocol };
};

const captureViewport = async (browser, url, state, vp, dir) => {
  const { page, errors, protocol } = await openInState(browser, url, state, vp);
  await page.screenshot({ path: join(dir, `${vp.name}.full.png`), fullPage: true });
  const aria = await page.locator('body').ariaSnapshot();
  writeFileSync(join(dir, `${vp.name}.aria.txt`), aria + '\n');
  const docHeight = await page.evaluate(() => document.documentElement.scrollHeight);
  const overflowX = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth);
  const tiles = vp === DESKTOP ? Math.min(Math.ceil(docHeight / vp.height), 12) : 0;
  for (let t = 0; t < tiles; t += 1) {
    await page.screenshot({ path: join(dir, `${vp.name}.tile-${String(t + 1).padStart(2, '0')}.png`), fullPage: true,
      clip: { x: 0, y: t * vp.height, width: vp.width, height: Math.min(vp.height, docHeight - t * vp.height) } });
  }
  const controls = vp === DESKTOP ? await page.evaluate(inventory, CONTROL_SELECTOR) : null;
  const confirm = vp === DESKTOP ? await page.evaluate(confirmControl) : null;
  const title = await page.title();
  await page.close();
  return { viewport: vp.name, protocol, errors, docHeight, horizontalOverflow: overflowX, tiles, title, controls, confirm, aria };
};

// Probe: activate one visible, enabled control on a fresh page in the same state and record what changed.
const probeControl = async (browser, url, state, control, dir) => {
  const { page, errors } = await openInState(browser, url, state, DESKTOP);
  const before = await page.locator('body').ariaSnapshot();
  const handle = page.locator(CONTROL_SELECTOR).nth(control.index);
  const start = await page.evaluate(landing);
  let outcome = 'clicked';
  try {
    await handle.click({ timeout: 2000 });
  } catch (e) {
    outcome = `not-clickable: ${String(e.message).split('\n')[0].slice(0, 200)}`;
  }
  await page.waitForTimeout(SETTLE_MS);
  const after = await page.locator('body').ariaSnapshot();
  const land = await page.evaluate(landing);
  const moved = land.scrollY !== start.scrollY || (land.focused && land.focused.text !== control.accessibleName);
  const shot = moved ? `probe-${String(control.index).padStart(3, '0')}.viewport.png` : null;
  if (shot) await page.screenshot({ path: join(dir, shot) });
  await page.close();
  return {
    controlIndex: control.index, tag: control.tag, accessibleName: control.accessibleName,
    isConfirmRootCauseControl: control.isConfirmRootCauseControl, outcome,
    before: start, after: land, ariaDiff: lineDiff(before, after), viewportScreenshotAfter: shot, errors,
  };
};

const shouldProbe = (c) => c.visible && !c.disabled;

const main = async () => {
  const browser = await chromium.launch({ executablePath: EXE });
  const results = [];
  for (const candidate of CANDIDATES) {
    for (const caseId of CASES) {
      const state = JSON.parse(readFileSync(join(FIX, `${caseId}.json`), 'utf8'));
      const url = `file://${S}/staging/${candidate}/index.html`;
      const dir = join(OUT, candidate, caseId);
      mkdirSync(dir, { recursive: true });
      try {
        const views = [];
        for (const vp of VIEWPORTS) views.push(await captureViewport(browser, url, state, vp, dir));
        const desktop = views[0];
        const probes = [];
        for (const c of desktop.controls.filter(shouldProbe)) probes.push(await probeControl(browser, url, state, c, dir));
        writeJson(join(dir, 'capture.json'), {
          candidate, caseId, state,
          viewports: views.map(({ aria, controls, confirm, ...rest }) => rest),
          controls: desktop.controls, confirmControl: desktop.confirm, probes,
        });
        results.push({ candidate, caseId, status: 'captured', protocol: desktop.protocol, probes: probes.length });
      } catch (e) {
        writeJson(join(dir, 'capture-failure.json'), { candidate, caseId, error: String(e.stack || e) });
        results.push({ candidate, caseId, status: 'infrastructure-failure', error: String(e.message) });
      }
    }
  }
  await browser.close();
  writeJson(join(OUT, 'capture-summary.json'), results);
  console.log(JSON.stringify(results));
};

await main();
