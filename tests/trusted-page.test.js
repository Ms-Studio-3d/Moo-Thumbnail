'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { pathToFileURL } = require('node:url');
const { isTrustedPage } = require('../src/trusted-page');
const page = 'C:\\Users\\MOHAME~1.SAL\\AppData\\Local\\Temp\\nst123.tmp\\app-64\\resources\\app.asar\\src\\index.html';
const chromiumUrl = 'file:///C:/Users/MOHAME~1.SAL/AppData/Local/Temp/nst123.tmp/app-64/resources/app.asar/src/index.html';

test('regression: portable Windows short path is accepted despite different URL encoding', () => {
  const nodeUrl = pathToFileURL(page, { windows: true }).href;
  assert.notEqual(nodeUrl, chromiumUrl);
  assert.equal(isTrustedPage(chromiumUrl, page, 'win32'), true);
  assert.equal(isTrustedPage(nodeUrl, page, 'win32'), true);
});
test('Windows path casing and encoded Arabic, spaces, hash and percent characters', () => {
  assert.equal(isTrustedPage(chromiumUrl.toLowerCase(), page, 'win32'), true);
  const special = 'C:\\صور # 100%\\app.asar\\src\\index.html';
  assert.equal(isTrustedPage(pathToFileURL(special, { windows: true }).href, special, 'win32'), true);
});
test('rejects another local file, suffix, query, fragment, remote URL and malformed URL', () => {
  for (const url of [chromiumUrl.replace('index.html', 'evil.html'), chromiumUrl + '.evil',
    chromiumUrl + '?file=evil', chromiumUrl + '#other', 'https://example.com/index.html',
    'data:text/html,test', 'invalid', 'file:///C:/invalid%ZZ/index.html',
    chromiumUrl.replace('file:///', 'file://evil/'), null]) {
    assert.equal(isTrustedPage(url, page, 'win32'), false, String(url));
  }
});
test('does not treat a different directory or similarly named ASAR as trusted', () => {
  assert.equal(isTrustedPage(chromiumUrl.replace('app.asar', 'evil.asar'), page, 'win32'), false);
  assert.equal(isTrustedPage(chromiumUrl.replace('/src/', '/different/'), page, 'win32'), false);
});
test('POSIX paths retain case-sensitive checks', () => {
  assert.equal(isTrustedPage('file:///tmp/App/index.html', '/tmp/App/index.html', 'linux'), true);
  assert.equal(isTrustedPage('file:///tmp/app/index.html', '/tmp/App/index.html', 'linux'), false);
});
