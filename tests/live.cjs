'use strict';
const { app, net, nativeImage } = require('electron');
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const { lookupThumbnails } = require('../src/thumbnail');
const { saveImage } = require('../src/save-image');
app.setPath('userData', path.join(__dirname, '..', '.test-data', 'live-profile'));
app.whenReady().then(async () => {
  const report = [];
  try {
    const dir = path.join(__dirname, '..', '.test-data', 'live-images');
    await fs.mkdir(dir, { recursive: true });
    for (const id of ['dQw4w9WgXcQ', 'jNQXAC9IVRw', 'aqz-KE-bpKQ']) {
      const start = Date.now();
      const { data, images } = await lookupThumbnails(`https://youtu.be/${id}`, {
        fetchImpl: (...args) => net.fetch(...args), getSize: buffer => nativeImage.createFromBuffer(buffer).getSize()
      });
      assert.ok(images.size > 0);
      for (const [key, buffer] of images) {
        const file = await saveImage(path.join(dir, `${id}-${key}.jpg`), buffer);
        assert.deepEqual(await fs.readFile(file), buffer);
        const size = nativeImage.createFromPath(file).getSize();
        const result = data.results.find(item => item.key === key);
        assert.equal(size.width, result.width); assert.equal(size.height, result.height);
      }
      const entry = { id, milliseconds: Date.now() - start, best: data.best,
        results: data.results.map(({ key, available, width, height, bytes, reason }) => ({ key, available, width, height, bytes, reason })) };
      report.push(entry); console.log(JSON.stringify(entry));
    }
    await assert.rejects(lookupThumbnails('https://youtu.be/00000000000', {
      fetchImpl: (...args) => net.fetch(...args), getSize: buffer => nativeImage.createFromBuffer(buffer).getSize()
    }));
    await fs.writeFile(path.join(__dirname, '..', '.test-data', 'live-report.json'), JSON.stringify(report, null, 2));
    app.exit(0);
  } catch (error) { console.error(error); app.exit(1); }
});
