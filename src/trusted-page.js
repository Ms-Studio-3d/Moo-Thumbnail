'use strict';
const path = require('node:path');
const { fileURLToPath } = require('node:url');

function isTrustedPage(frameUrl, expectedPath, platform = process.platform) {
  try {
    const url = new URL(frameUrl);
    if (url.protocol !== 'file:' || url.search || url.hash) return false;
    const windows = platform === 'win32';
    const paths = windows ? path.win32 : path.posix;
    const actual = paths.normalize(fileURLToPath(url, { windows }));
    const expected = paths.resolve(expectedPath);
    // Chromium and Node serialize Windows short-path '~' differently.
    // Compare decoded filesystem paths while still requiring this exact page.
    return windows ? actual.toLowerCase() === expected.toLowerCase() : actual === expected;
  } catch { return false; }
}

module.exports = { isTrustedPage };
