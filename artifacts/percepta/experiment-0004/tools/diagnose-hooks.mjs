import { chromium } from '/opt/node22/lib/node_modules/playwright/index.mjs';
import { readFileSync } from 'node:fs';
const S = process.argv[2];
const state = JSON.parse(readFileSync('/home/user/percepta/artifacts/percepta/experiment-0004/held-out-fixtures/combined-blocker-falsification.json','utf8'));
const probe = () => ({
  observations: [...document.querySelectorAll('[data-percepta-observation]')].map(e => [e.getAttribute('data-percepta-observation'), !!(e.offsetWidth||e.offsetHeight||e.getClientRects().length)]),
  reasonFor: document.querySelectorAll('[data-percepta-unavailable-reason-for]').length,
  blockerLink: document.querySelectorAll('[data-percepta-blocker-link-for]').length,
});
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium_headless_shell-1194/chrome-linux/headless_shell' });
for (const c of ['candidate-1','candidate-2','candidate-3','candidate-4']) {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await page.goto(`file://${S}/staging/${c}/index.html`);
  await page.evaluate(s => window.__perceptaSetState(s), state);
  const now = await page.evaluate(probe);
  await page.waitForTimeout(1500);
  const later = await page.evaluate(probe);
  console.log(c, JSON.stringify({ now, laterSame: JSON.stringify(now) === JSON.stringify(later), later }));
  await page.close();
}
await browser.close();
