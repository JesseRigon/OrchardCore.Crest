const fs = require('fs');
const path = require('path');
const { PNG } = require('pngjs');
// pixelmatch 7.x ships ESM-only; requiring it from CommonJS gets the module namespace
// object, not the function itself.
const pixelmatch = require('pixelmatch').default;

// output/base/  — stable screenshots, hand-promoted at known-good points. Checked in.
// output/new/   — wiped at the start of every suite run, repopulated by this run,
//                 compared against base/. Diff images (when a check fails) land here too.
function resolveDirs(outputRoot) {
  const base = path.join(outputRoot, 'base');
  const fresh = path.join(outputRoot, 'new');
  fs.mkdirSync(base, { recursive: true });
  fs.mkdirSync(fresh, { recursive: true });
  return { base, fresh };
}

function cleanNewDir(outputRoot) {
  const { fresh } = resolveDirs(outputRoot);
  for (const file of fs.readdirSync(fresh)) {
    fs.rmSync(path.join(fresh, file), { force: true, recursive: true });
  }
}

// Captures a screenshot into new/, then pixel-diffs it against base/<name>.png.
// No AI judgment anywhere in this path — pixelmatch is a deterministic pixel comparator.
async function captureAndCompare(page, name, opts = {}) {
  const outputRoot = opts.outputRoot || path.join(__dirname, '..', 'output');
  const threshold = opts.threshold ?? 0.1; // pixelmatch per-pixel color-distance tolerance
  const maxDiffPixels = opts.maxDiffPixels ?? 0; // how many differing pixels still count as a pass
  const { base, fresh } = resolveDirs(outputRoot);

  const newPath = path.join(fresh, `${name}.png`);
  const basePath = path.join(base, `${name}.png`);

  fs.mkdirSync(path.dirname(newPath), { recursive: true });
  // `mask` covers matching locators with a solid box before capture — use it for any
  // element whose content legitimately changes every request (hashes, timestamps,
  // request IDs) so it never shows up as a false-positive diff.
  await page.screenshot({ path: newPath, fullPage: opts.fullPage ?? true, mask: opts.mask });

  if (process.env.UPDATE_BASE === '1') {
    fs.mkdirSync(path.dirname(basePath), { recursive: true });
    fs.copyFileSync(newPath, basePath);
    return { name, status: 'base-updated', pass: true, message: `baseline written to ${path.relative(outputRoot, basePath)}` };
  }

  if (!fs.existsSync(basePath)) {
    return {
      name,
      status: 'no-base',
      pass: null,
      message: `no baseline at ${path.relative(outputRoot, basePath)} — rerun with UPDATE_BASE=1 to create it`,
    };
  }

  const baseImg = PNG.sync.read(fs.readFileSync(basePath));
  const newImg = PNG.sync.read(fs.readFileSync(newPath));

  if (baseImg.width !== newImg.width || baseImg.height !== newImg.height) {
    return {
      name,
      status: 'size-mismatch',
      pass: false,
      message: `base ${baseImg.width}x${baseImg.height} vs new ${newImg.width}x${newImg.height}`,
    };
  }

  const { width, height } = baseImg;
  const diff = new PNG({ width, height });
  const diffPixels = pixelmatch(baseImg.data, newImg.data, diff.data, width, height, { threshold });
  const pass = diffPixels <= maxDiffPixels;

  if (!pass) {
    fs.writeFileSync(path.join(fresh, `${name}.diff.png`), PNG.sync.write(diff));
  }

  return {
    name,
    status: pass ? 'match' : 'diff',
    pass,
    diffPixels,
    diffPercent: Number(((diffPixels / (width * height)) * 100).toFixed(3)),
    message: pass ? 'matches baseline' : `${diffPixels} px differ (${path.relative(outputRoot, path.join(fresh, `${name}.diff.png`))})`,
  };
}

module.exports = { captureAndCompare, cleanNewDir, resolveDirs };
