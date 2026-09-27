// EX-PERCEPTA-2026-0004 deterministic per-state runtime probe.
// Usage: node probe.mjs <heldOutCasesJson> <baseUrl> <candidateDir> <outDir>
// Records observations only; performs no semantic scoring.
import { createRequire } from "node:module";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { readFileSync } from "node:fs";

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE ?? "/opt/node22/lib/node_modules/playwright");

const VIEWPORTS = Object.freeze([
  { name: "390x844", width: 390, height: 844, contractBreakpoint: "narrow-phone" },
  { name: "820x1180", width: 820, height: 1180, contractBreakpoint: "ipad-portrait" },
  { name: "1180x820", width: 1180, height: 820, contractBreakpoint: "ipad-landscape" },
  { name: "1440x900", width: 1440, height: 900, contractBreakpoint: "desktop-reference" },
]);
const REGIONS = Object.freeze(["observation", "hypotheses", "contradictions", "unknowns", "obligations", "evidence", "legal-actions", "persistence-state"]);
const BREAKPOINT_REQUIRED = Object.freeze({
  "narrow-phone": ["hypotheses", "unknowns", "legal-actions"],
  "ipad-portrait": ["hypotheses", "unknowns", "legal-actions"],
  "ipad-landscape": ["hypotheses", "unknowns", "legal-actions"],
  "desktop-reference": ["observation", "hypotheses", "unknowns", "legal-actions"],
});
const CAPABILITY = "confirm-root-cause";
const SETTLE_MS = 400;
const TRACE_LIMIT = 40;

const [casesPath, baseUrl, candidateDir, outDir] = process.argv.slice(2);
const cases = JSON.parse(readFileSync(casesPath, "utf8")).cases;
const url = `${baseUrl.replace(/\/$/, "")}/${candidateDir}/index.html`;

// In-page collector: pure function of the current DOM.
const collect = ({ regions, capability, breakpointRequired }) => {
  const isVisible = (el) => {
    if (!el) return false;
    const s = getComputedStyle(el);
    const r = el.getBoundingClientRect();
    return s.display !== "none" && s.visibility !== "hidden" && Number(s.opacity) !== 0 && r.width > 0 && r.height > 0;
  };
  const box = (el) => {
    const r = el.getBoundingClientRect();
    return { x: Math.round(r.x), y: Math.round(r.y + scrollY), width: Math.round(r.width), height: Math.round(r.height) };
  };
  const clippedHorizontally = (el) => {
    const r = el.getBoundingClientRect();
    return r.left < -1 || r.right > document.documentElement.clientWidth + 1;
  };
  const text = (el, n = 300) => (el?.innerText ?? el?.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, n);
  const all = (sel) => [...document.querySelectorAll(sel)];
  const regionObs = Object.fromEntries(regions.map((id) => {
    const els = all(`[data-percepta-region="${id}"]`);
    const vis = els.filter(isVisible);
    return [id, {
      count: els.length,
      visibleCount: vis.length,
      visible: vis.length > 0,
      clippedHorizontally: vis.some(clippedHorizontally),
      boxes: vis.map(box),
      textSample: text(vis[0] ?? els[0], 400),
    }];
  }));
  const capEls = all(`[data-percepta-capability="${capability}"]`);
  const cap0 = capEls[0];
  const reasonEls = all(`[data-percepta-unavailable-reason-for="${capability}"]`);
  const blockerEls = all(`[data-percepta-blocker-link-for="${capability}"]`);
  const blockerObs = blockerEls.map((el) => {
    const href = el.getAttribute("href");
    const target = href && href.startsWith("#") && href.length > 1 ? document.getElementById(decodeURIComponent(href.slice(1))) : null;
    return { tag: el.tagName.toLowerCase(), visible: isVisible(el), href, text: text(el, 200), targetExists: !!target, targetVisible: isVisible(target), targetText: text(target, 200) };
  });
  const ids = all("[id]").map((el) => el.id).filter(Boolean);
  const duplicateIds = [...new Set(ids.filter((id, i) => ids.indexOf(id) !== i))];
  const unnamedControls = all("button, a[href], input, select, textarea").filter((el) => {
    const s = getComputedStyle(el);
    if (s.display === "none" || s.visibility === "hidden") return false;
    const name = (el.getAttribute("aria-label") || el.getAttribute("title") || el.textContent || "").trim();
    return !name && !el.getAttribute("aria-labelledby");
  }).map((el) => el.outerHTML.slice(0, 160));
  const stateEls = all("[data-percepta-state]");
  const colorOnlyStateEls = stateEls.filter((el) => !((el.textContent || "").trim()) && !(el.getAttribute("aria-label") || "").trim()).length;
  const observations = [...new Set(all("[data-percepta-observation]").filter(isVisible).map((el) => el.getAttribute("data-percepta-observation")))].sort();
  const bpRequired = breakpointRequired.map((id) => ({ region: id, visible: regionObs[id]?.visible ?? false }));
  return {
    entryPoint: typeof window.__perceptaSetState === "function",
    regions: regionObs,
    capability: {
      count: capEls.length,
      visible: isVisible(cap0),
      tag: cap0?.tagName.toLowerCase() ?? null,
      disabledProperty: cap0 ? !!cap0.disabled : null,
      ariaDisabled: cap0?.getAttribute("aria-disabled") ?? null,
      text: text(cap0, 200),
      ariaLabel: cap0?.getAttribute("aria-label") ?? null,
      describedBy: cap0?.getAttribute("aria-describedby") ?? null,
      inLegalActionsRegion: !!cap0?.closest('[data-percepta-region="legal-actions"]'),
    },
    unavailableReason: reasonEls.map((el) => ({ visible: isVisible(el), text: text(el, 400) })),
    blockerLinks: blockerObs,
    visibleObservationHooks: observations,
    accessibility: {
      lang: document.documentElement.getAttribute("lang"),
      mainCount: all("main").length,
      h1Count: all("h1").length,
      h1Text: all("h1").map((el) => text(el, 120)),
      duplicateIds,
      imagesMissingAlt: all("img").filter((i) => !i.hasAttribute("alt")).length,
      unnamedControls,
      stateElements: stateEls.length,
      colorOnlyStateElements: colorOnlyStateEls,
    },
    overflow: {
      scrollWidth: document.documentElement.scrollWidth,
      clientWidth: document.documentElement.clientWidth,
      horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
      scrollHeight: document.documentElement.scrollHeight,
    },
    breakpointRequiredRegions: bpRequired,
    breakpointRequiredRegionsAllVisible: bpRequired.every((r) => r.visible),
  };
};

const withPage = async (browser, viewport, fn) => {
  const context = await browser.newContext({ viewport: { width: viewport.width, height: viewport.height }, deviceScaleFactor: 1 });
  const page = await context.newPage();
  const consoleErrors = [];
  const pageErrors = [];
  const failedRequests = [];
  page.on("console", (m) => (m.type() === "error" ? consoleErrors.push(m.text()) : undefined));
  page.on("pageerror", (e) => pageErrors.push(String(e?.stack ?? e)));
  page.on("requestfailed", (r) => failedRequests.push({ url: r.url(), failure: r.failure()?.errorText ?? null }));
  try {
    return await fn(page, { consoleErrors, pageErrors, failedRequests });
  } finally {
    await context.close();
  }
};

const loadAndSet = async (page, state) => {
  const nav = await page.goto(url, { waitUntil: "load", timeout: 20000 }).then((r) => ({ ok: true, status: r?.status() ?? null }), (e) => ({ ok: false, error: String(e) }));
  if (!nav.ok) return { nav, setState: { attempted: false } };
  await page.waitForTimeout(150);
  const setState = await page.evaluate((s) => {
    if (typeof window.__perceptaSetState !== "function") return { attempted: true, entryPoint: false, threw: false };
    try {
      const ret = window.__perceptaSetState(JSON.parse(JSON.stringify(s)));
      return { attempted: true, entryPoint: true, threw: false, returnedPromise: !!(ret && typeof ret.then === "function") };
    } catch (e) {
      return { attempted: true, entryPoint: true, threw: true, error: String(e?.stack ?? e) };
    }
  }, state);
  await page.waitForTimeout(SETTLE_MS);
  return { nav, setState };
};

const probeViewport = (browser, kase, viewport, caseDir) =>
  withPage(browser, viewport, async (page, errs) => {
    const { nav, setState } = await loadAndSet(page, kase.state);
    if (!nav.ok) return { viewport, nav, setState, infrastructureFailure: true, errors: errs };
    const obs = await page.evaluate(collect, { regions: REGIONS, capability: CAPABILITY, breakpointRequired: BREAKPOINT_REQUIRED[viewport.contractBreakpoint] });
    const shot = path.join(caseDir, `${viewport.name}.png`);
    await page.screenshot({ path: shot, fullPage: true });
    const desktopExtras = viewport.name === "1440x900"
      ? {
          ariaSnapshot: await page.locator("body").ariaSnapshot().catch((e) => `ARIA_SNAPSHOT_ERROR: ${e}`),
          innerText: await page.evaluate(() => document.body.innerText),
        }
      : {};
    return { viewport, nav, setState, infrastructureFailure: false, observations: obs, screenshot: path.basename(shot), ...desktopExtras, errors: errs };
  });

// Enumerate operable targets for the operation trace (desktop only).
const enumerateTargets = (page) =>
  page.evaluate(({ capability, limit }) => {
    const isVisible = (el) => {
      const s = getComputedStyle(el);
      const r = el.getBoundingClientRect();
      return s.display !== "none" && s.visibility !== "hidden" && r.width > 0 && r.height > 0;
    };
    const operable = (el) => isVisible(el) && !el.disabled && el.getAttribute("aria-disabled") !== "true";
    const inRegion = [...document.querySelectorAll('[data-percepta-region="legal-actions"]')]
      .flatMap((r) => [...r.querySelectorAll('a[href], button, [role="button"], [role="link"], summary')]);
    const anchors = [...document.querySelectorAll('a[href^="#"]')];
    const capEl = document.querySelector(`[data-percepta-capability="${capability}"]`);
    const unique = [...new Set([...(capEl ? [capEl] : []), ...inRegion, ...anchors])].filter(operable);
    return unique.slice(0, limit).map((el, i) => {
      const all = [...document.querySelectorAll("*")];
      return {
        index: i,
        domIndex: all.indexOf(el),
        tag: el.tagName.toLowerCase(),
        role: el.getAttribute("role"),
        label: (el.getAttribute("aria-label") || el.innerText || el.textContent || "").replace(/\s+/g, " ").trim().slice(0, 160),
        href: el.getAttribute("href"),
        isConfirmCapability: el === capEl,
      };
    });
  }, { capability: CAPABILITY, limit: TRACE_LIMIT });

const traceOne = (browser, kase, target, caseDir) =>
  withPage(browser, VIEWPORTS[3], async (page, errs) => {
    const { nav } = await loadAndSet(page, kase.state);
    if (!nav.ok) return { target, infrastructureFailure: true, nav };
    const before = await page.evaluate(() => ({ scrollY: Math.round(scrollY), hash: location.hash }));
    const handle = await page.evaluateHandle((i) => document.querySelectorAll("*")[i], target.domIndex);
    const click = await handle.asElement().click({ timeout: 3000 }).then(() => ({ ok: true }), (e) => ({ ok: false, error: String(e).split("\n")[0] }));
    await page.waitForTimeout(SETTLE_MS);
    const after = await page.evaluate(() => {
      const t = (el, n = 400) => (el?.innerText ?? el?.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, n);
      const hashTarget = location.hash.length > 1 ? document.getElementById(decodeURIComponent(location.hash.slice(1))) : null;
      const ae = document.activeElement;
      const r = hashTarget?.getBoundingClientRect();
      return {
        scrollY: Math.round(scrollY),
        hash: location.hash,
        activeElement: ae && ae !== document.body ? { tag: ae.tagName.toLowerCase(), text: t(ae, 300) } : null,
        hashTargetText: t(hashTarget),
        hashTargetInViewport: r ? r.bottom > 0 && r.top < innerHeight : null,
      };
    });
    const shot = path.join(caseDir, "trace", `${String(target.index).padStart(2, "0")}.jpg`);
    await mkdir(path.dirname(shot), { recursive: true });
    await page.screenshot({ path: shot, type: "jpeg", quality: 70 });
    return { target, click, before, after, screenshot: `trace/${path.basename(shot)}`, pageErrors: errs.pageErrors, consoleErrors: errs.consoleErrors };
  });

const probeCase = async (browser, kase) => {
  const caseDir = path.join(outDir, kase.id);
  await mkdir(caseDir, { recursive: true });
  const viewports = await VIEWPORTS.reduce(async (accP, vp) => [...(await accP), await probeViewport(browser, kase, vp, caseDir)], Promise.resolve([]));
  const targets = await withPage(browser, VIEWPORTS[3], async (page) => (await loadAndSet(page, kase.state)).nav.ok ? enumerateTargets(page) : []);
  const trace = await targets.reduce(async (accP, t) => [...(await accP), await traceOne(browser, kase, t, caseDir)], Promise.resolve([]));
  const record = { caseId: kase.id, suppliedState: kase.state, probedAtUtc: new Date().toISOString(), viewports, operationTrace: trace };
  await writeFile(path.join(caseDir, "observations.json"), JSON.stringify(record, null, 2));
  return { caseId: kase.id, viewports: viewports.length, traceSteps: trace.length };
};

const main = async () => {
  await mkdir(outDir, { recursive: true });
  const browser = await chromium.launch({ headless: true });
  try {
    const loadOnly = await withPage(browser, VIEWPORTS[3], async (page, errs) => {
      const nav = await page.goto(url, { waitUntil: "load", timeout: 20000 }).then((r) => ({ ok: true, status: r?.status() ?? null }), (e) => ({ ok: false, error: String(e) }));
      await page.waitForTimeout(SETTLE_MS);
      const entryPoint = nav.ok ? await page.evaluate(() => typeof window.__perceptaSetState === "function") : null;
      if (nav.ok) await page.screenshot({ path: path.join(outDir, "initial-load-1440x900.png"), fullPage: true });
      return { nav, entryPoint, errors: errs };
    });
    const summaries = await cases.reduce(async (accP, k) => [...(await accP), await probeCase(browser, k)], Promise.resolve([]));
    const meta = { url, browserVersion: browser.version(), playwrightVersion: require((process.env.PLAYWRIGHT_MODULE ?? "/opt/node22/lib/node_modules/playwright") + "/package.json").version, finishedAtUtc: new Date().toISOString(), initialLoad: loadOnly, cases: summaries };
    await writeFile(path.join(outDir, "probe-run.json"), JSON.stringify(meta, null, 2));
    console.log(JSON.stringify(meta.cases));
  } finally {
    await browser.close();
  }
};

main().catch((e) => {
  console.error("PROBE_INFRASTRUCTURE_FAILURE", e);
  process.exitCode = 2;
});
