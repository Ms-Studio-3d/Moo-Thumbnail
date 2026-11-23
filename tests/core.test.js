'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { VARIANTS, MAX_BYTES, extractVideoId, thumbnailUrl, fetchJpeg, lookupThumbnails } = require('../src/thumbnail');
const { saveImage } = require('../src/save-image');
const id = 'dQw4w9WgXcQ';
for (const url of [
  `https://www.youtube.com/watch?v=${id}&t=20`, `https://youtu.be/${id}?si=abc`,
  `https://youtube.com/shorts/${id}`, `https://youtube.com/live/${id}?feature=share`,
  `https://www.youtube-nocookie.com/embed/${id}`, `https://m.youtube.com/watch?v=${id}`,
  `https://music.youtube.com/watch?v=${id}`, `youtube.com/watch?v=${id}`, `youtu.be/${id}`,
  ` https://www.youtube.com/v/${id} `
]) test(`accepts ${url}`, () => assert.equal(extractVideoId(url), id));
for (const url of ['', null, {}, 'not a URL', 'x'.repeat(5000),
  `https://evilyoutube.com/watch?v=${id}`, `https://youtube.com.evil.test/watch?v=${id}`,
  `https://youtube.com@evil.test/watch?v=${id}`, `https://user@youtube.com/watch?v=${id}`,
  `ftp://youtube.com/watch?v=${id}`, `https://youtube.com:8443/watch?v=${id}`,
  'https://youtube.com/playlist?list=xxx', `https://youtu.be/${id}/extra`,
  'https://youtube.com/watch?v=short', 'file:///C:/example.txt'
]) test(`rejects ${String(url).slice(0, 100)}`, () => assert.throws(() => extractVideoId(url), { code: 'INVALID_URL' }));

const jpeg = Buffer.from([0xff, 0xd8, 0xff, 0xd9]);
const response = () => new Response(jpeg, { headers: { 'content-type': 'image/jpeg' } });
test('allows only fixed thumbnail destinations', () => {
  assert.equal(thumbnailUrl(id, 'default'), `https://i.ytimg.com/vi/${id}/default.jpg`);
  assert.throws(() => thumbnailUrl(id, '../evil'));
  assert.throws(() => thumbnailUrl('../evil', 'default'));
});
test('accepts valid small images; bytes do not determine quality', async () => {
  const image = await fetchJpeg('https://example.test', response, () => ({ width: 1280, height: 720 }));
  assert.equal(image.buffer.length, 4);
});
test('six checks run concurrently and reject high-resolution placeholders', async () => {
  let active = 0, peak = 0;
  const fetchImpl = async () => {
    active++; peak = Math.max(peak, active);
    await new Promise(resolve => setTimeout(resolve, 5)); active--;
    return response();
  };
  const { data, images } = await lookupThumbnails(`https://youtu.be/${id}`, { fetchImpl, getSize: () => ({ width: 120, height: 90 }) });
  assert.equal(peak, 6);
  assert.equal(data.best, 'default'); assert.equal(images.size, 1);
  assert.equal(data.results.filter(item => item.available).length, 1);
});
test('reports real dimensions and preserves exact preview bytes', async () => {
  const { data, images } = await lookupThumbnails(`https://youtu.be/${id}`, { fetchImpl: response, getSize: () => ({ width: 1920, height: 1080 }) });
  assert.equal(data.best, 'maxresdefault');
  assert.equal(data.results[0].size, '1920 × 1080');
  assert.deepEqual(Buffer.from(data.results[0].preview.split(',')[1], 'base64'), images.get('maxresdefault'));
});
test('all missing images fail clearly', async () => {
  await assert.rejects(lookupThumbnails(`https://youtu.be/${id}`, { fetchImpl: () => new Response(null, { status: 404 }), getSize: () => ({}) }), { code: 'NOT_FOUND' });
});
test('offline requests fail clearly', async () => {
  await assert.rejects(lookupThumbnails(`https://youtu.be/${id}`, { fetchImpl: () => { throw new Error('offline'); }, getSize: () => ({}) }), { code: 'NETWORK' });
});
test('partial outages retain available images and mark failed checks', async () => {
  const { data } = await lookupThumbnails(`https://youtu.be/${id}`, {
    fetchImpl: url => { if (!url.endsWith('/default.jpg')) throw new Error('offline'); return response(); },
    getSize: () => ({ width: 120, height: 90 })
  });
  assert.equal(data.best, 'default');
  assert.equal(data.results.filter(item => item.reason === 'network').length, 5);
});
test('timeouts use a signal and remain bounded', async () => {
  await assert.rejects(fetchJpeg('https://example.test', async (_url, { signal }) => {
    await new Promise(resolve => setTimeout(resolve, 15)); signal.throwIfAborted();
  }, () => ({}), 5), { code: 'NETWORK' });
});
test('rejects HTML, corrupt JPEG, oversized length and oversized streams', async () => {
  for (const make of [
    () => new Response('<html>error</html>', { headers: { 'content-type': 'text/html' } }),
    () => new Response('corrupt', { headers: { 'content-type': 'image/jpeg' } }),
    () => new Response(jpeg, { headers: { 'content-type': 'image/jpeg', 'content-length': String(MAX_BYTES + 1) } }),
    () => new Response(Buffer.alloc(MAX_BYTES + 1), { headers: { 'content-type': 'image/jpeg' } })
  ]) await assert.rejects(fetchJpeg('https://example.test', make, () => ({ width: 1280, height: 720 })), { code: 'NETWORK' });
});
test('saves byte-for-byte JPG, overwrites, supports Arabic paths and cleans temporary files', async () => {
  const root = path.join(__dirname, '..', '.test-data');
  await fs.mkdir(root, { recursive: true });
  const dir = await fs.mkdtemp(path.join(root, 'حفظ صورة-'));
  try {
    const destination = await saveImage(path.join(dir, 'صورتي'), jpeg);
    assert.match(destination, /\.jpg$/);
    assert.deepEqual(await fs.readFile(destination), jpeg);
    await saveImage(destination, Buffer.concat([jpeg, jpeg]));
    assert.equal((await fs.readFile(destination)).length, 8);
    assert.deepEqual(await fs.readdir(dir), ['صورتي.jpg']);
    await assert.rejects(saveImage(path.join(dir, 'missing', 'image.jpg'), jpeg), { code: 'ENOENT' });
  } finally { await fs.rm(dir, { recursive: true, force: true }); }
});
test('package is portable only with correct identity', () => {
  const pkg = require('../package.json');
  assert.equal(pkg.build.productName, 'Moo-Thumbnail');
  assert.equal(pkg.build.win.target[0].target, 'portable');
  assert.equal(pkg.build.portable.requestExecutionLevel, 'user');
  assert.equal(VARIANTS.length, 6);
});
