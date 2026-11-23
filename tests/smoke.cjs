'use strict';
// Deterministic integration test: real main/preload/renderer/IPC/filesystem,
// with only the network, native dialogs and Explorer action replaced.
const electron = require('electron');
const { app, BrowserWindow, net, nativeImage, dialog, shell, clipboard } = electron;
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const root = path.join(__dirname, '..', '.test-data');
const savedPath = path.join(root, 'صورة تجريبية.jpg');
let cancelSave = true, opened = false;
net.fetch = async url => {
  const variant = require('../src/thumbnail').VARIANTS.find(item => url.endsWith(`/${item.key}.jpg`));
  if (variant.key === 'hq720') return new Response(null, { status: 404 });
  const image = nativeImage.createFromPath(path.join(__dirname, '..', 'assets', 'icon.png'))
    .resize({ width: variant.minWidth, height: variant.minHeight }).toJPEG(85);
  return new Response(image, { headers: { 'content-type': 'image/jpeg' } });
};
dialog.showSaveDialog = async () => cancelSave ? { canceled: true } : { canceled: false, filePath: savedPath };
shell.showItemInFolder = file => { assert.equal(file, savedPath); opened = true; };
require('../src/main');
app.whenReady().then(async () => {
  let originalClipboard;
  try {
    await fs.mkdir(root, { recursive: true });
    const win = BrowserWindow.getAllWindows()[0];
    if (win.webContents.isLoading()) await new Promise(resolve => win.webContents.once('did-finish-load', resolve));
    const run = script => win.webContents.executeJavaScript(script);
    const waitFor = async expression => {
      for (let i = 0; i < 100; i++) {
        if (await run(expression)) return;
        await new Promise(resolve => setTimeout(resolve, 50));
      }
      throw new Error(`Timed out: ${expression}`);
    };
    assert.equal(await run('typeof require'), 'undefined');
    assert.equal(await run('typeof window.studio.lookup'), 'function');
    await run(`document.getElementById('urlInput').value='https://evilyoutube.com/watch?v=dQw4w9WgXcQ'; document.getElementById('urlForm').requestSubmit();`);
    await waitFor(`document.getElementById('status').classList.contains('error') && !document.getElementById('extractBtn').disabled`);
    assert.equal(await run(`document.getElementById('result').classList.contains('hidden')`), true);
    await run(`document.getElementById('urlInput').value='https://youtu.be/dQw4w9WgXcQ'; document.getElementById('urlForm').requestSubmit();`);
    await waitFor(`!document.getElementById('extractBtn').disabled && !document.getElementById('result').classList.contains('hidden') && document.getElementById('preview').naturalWidth > 0`);
    assert.equal(await run(`document.querySelectorAll('.quality-item').length`), 6);
    assert.equal(await run(`document.querySelectorAll('.quality-item input:disabled').length`), 1);
    assert.equal(await run(`document.querySelector('.quality-item input:checked').value`), 'maxresdefault');
    await run(`document.querySelector('input[value="mqdefault"]').click()`);
    await waitFor(`document.getElementById('preview').naturalWidth === 320`);
    originalClipboard = await clipboard.readText();
    await run(`document.getElementById('copyBtn').click()`);
    await waitFor(`document.getElementById('status').textContent.includes('تم نسخ')`);
    assert.equal(await clipboard.readText(), 'https://i.ytimg.com/vi/dQw4w9WgXcQ/mqdefault.jpg');
    await run(`document.getElementById('downloadBtn').click()`);
    await waitFor(`!document.getElementById('downloadBtn').disabled`);
    assert.equal(await run(`document.getElementById('status').textContent.includes('إلغاء')`), true);
    cancelSave = false;
    await run(`document.getElementById('downloadBtn').click()`);
    await waitFor(`!document.getElementById('downloadBtn').disabled && document.getElementById('status').textContent.includes('تم حفظ')`);
    const previewBytes = await run(`document.getElementById('preview').src.split(',')[1]`);
    assert.deepEqual(await fs.readFile(savedPath), Buffer.from(previewBytes, 'base64'));
    await run(`document.getElementById('folderBtn').click()`);
    assert.equal(opened, true);
    await assert.rejects(run(`window.studio.download({id:'dQw4w9WgXcQ',key:'../evil'})`));
    for (const width of [1160, 900, 760]) {
      win.setSize(width, 860);
      await new Promise(resolve => setTimeout(resolve, 100));
      assert.equal(await run('document.documentElement.scrollWidth <= window.innerWidth'), true);
    }
    win.setSize(1160, 860);
    await new Promise(resolve => setTimeout(resolve, 100));
    await fs.writeFile(path.join(root, 'smoke-preview.png'), (await win.webContents.capturePage()).toPNG());
    console.log('PASS: real IPC, invalid URL, six qualities, selection, preview, clipboard, cancel/save exact bytes, open folder, selection validation, responsive layout, sandbox.');
    if (originalClipboard !== undefined) await clipboard.writeText(originalClipboard);
    app.exit(0);
  } catch (error) {
    if (originalClipboard !== undefined) await clipboard.writeText(originalClipboard);
    console.error(error); app.exit(1);
  }
});
