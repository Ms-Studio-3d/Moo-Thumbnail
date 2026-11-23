'use strict';
const { app, BrowserWindow, ipcMain, dialog, shell, clipboard, net, nativeImage, session } = require('electron');
const path = require('node:path');
const fs = require('node:fs');
const { pathToFileURL } = require('node:url');
const { lookupThumbnails, ThumbnailError } = require('./thumbnail');
const { saveImage } = require('./save-image');
const page = path.join(__dirname, 'index.html');
const pageUrl = pathToFileURL(page).href;
let win, current = null, lookupBusy = false, saving = false, lastSaved = null;
const portableRoot = process.env.PORTABLE_EXECUTABLE_DIR;
const dataRoot = portableRoot ? path.join(portableRoot, 'Moo-Thumbnail-data')
  : app.isPackaged ? path.join(path.dirname(app.getPath('exe')), 'Moo-Thumbnail-data')
    : path.join(__dirname, '..', '.dev-data');
let startupError;
try {
  fs.mkdirSync(dataRoot, { recursive: true });
  fs.accessSync(dataRoot, fs.constants.W_OK);
  app.setPath('userData', dataRoot);
  app.setPath('sessionData', dataRoot);
  app.setAppLogsPath(path.join(dataRoot, 'logs'));
} catch { startupError = true; }
app.setName('Moo-Thumbnail');
app.setAppUserModelId('com.moo.thumbnail');
function createWindow() {
  win = new BrowserWindow({
    title: 'Moo-Thumbnail', width: 1160, height: 860, minWidth: 760, minHeight: 620,
    show: false, backgroundColor: '#0e1014', autoHideMenuBar: true,
    icon: path.join(__dirname, '..', 'assets', 'icon.ico'),
    webPreferences: { preload: path.join(__dirname, 'preload.js'), contextIsolation: true, nodeIntegration: false, sandbox: true }
  });
  win.removeMenu();
  win.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  win.webContents.on('will-navigate', event => event.preventDefault());
  win.webContents.on('will-attach-webview', event => event.preventDefault());
  win.once('ready-to-show', () => win.show());
  win.on('closed', () => { win = null; current = null; });
  win.loadFile(page);
}
function handle(channel, callback) {
  ipcMain.handle(channel, async (event, payload) => {
    try {
      if (!win || event.sender !== win.webContents || event.senderFrame !== win.webContents.mainFrame || event.senderFrame.url !== pageUrl) {
        throw new ThumbnailError('FORBIDDEN', 'طلب غير مسموح.');
      }
      return { ok: true, data: await callback(payload) };
    } catch (error) {
      let message = error instanceof ThumbnailError ? error.message : 'حدث خطأ غير متوقع. أعد المحاولة.';
      if (['EACCES', 'EPERM', 'EROFS'].includes(error.code)) message = 'لا يمكن الكتابة في هذا المجلد. اختر مكانًا آخر لحفظ الصورة.';
      if (error.code === 'ENOSPC') message = 'المساحة غير كافية لحفظ الصورة. حرّر مساحة ثم حاول مرة أخرى.';
      if (error.code === 'ENOENT') message = 'مجلد الحفظ غير موجود. اختر مجلدًا آخر.';
      return { ok: false, error: { code: error.code || 'UNKNOWN', message } };
    }
  });
}
function selectedImage(selection) {
  if (!current || selection?.id !== current.data.id || !current.images.has(selection?.key)) {
    throw new ThumbnailError('INVALID_SELECTION', 'استخرج الصور واختر جودة متاحة أولًا.');
  }
  return { item: current.data.results.find(item => item.key === selection.key), buffer: current.images.get(selection.key) };
}
handle('thumbnail:lookup', async input => {
  if (lookupBusy || saving) throw new ThumbnailError('BUSY', 'انتظر اكتمال العملية الحالية.');
  lookupBusy = true;
  current = null;
  try {
    current = await lookupThumbnails(input, { fetchImpl: (...args) => net.fetch(...args), getSize: buffer => nativeImage.createFromBuffer(buffer).getSize() });
    return current.data;
  } finally { lookupBusy = false; }
});
handle('thumbnail:download', async selection => {
  if (saving || lookupBusy) throw new ThumbnailError('BUSY', 'انتظر اكتمال العملية الحالية.');
  const { buffer } = selectedImage(selection);
  saving = true;
  try {
    const { canceled, filePath } = await dialog.showSaveDialog(win, {
      title: 'حفظ الصورة المصغّرة',
      defaultPath: path.join(app.getPath('pictures'), `Moo-Thumbnail-${selection.id}-${selection.key}.jpg`),
      filters: [{ name: 'JPEG Image', extensions: ['jpg', 'jpeg'] }],
      properties: ['showOverwriteConfirmation']
    });
    if (canceled || !filePath) return { canceled: true };
    if (!/\.jpe?g$/i.test(filePath)) throw new ThumbnailError('INVALID_FILENAME', 'اختر اسم ملف ينتهي بـ .jpg أو .jpeg ثم أعد الحفظ.');
    lastSaved = await saveImage(filePath, buffer);
    return { canceled: false, filePath: lastSaved };
  } finally { saving = false; }
});
handle('thumbnail:copy', async selection => {
  const { item } = selectedImage(selection);
  await clipboard.writeText(item.url);
  return true;
});
handle('app:open-folder', () => {
  if (!lastSaved || !fs.existsSync(lastSaved)) throw new ThumbnailError('MISSING_FILE', 'الصورة غير موجودة في مكان الحفظ. احفظها مرة أخرى.');
  shell.showItemInFolder(lastSaved);
  return true;
});
handle('app:info', () => ({ version: app.getVersion(), portable: !!portableRoot }));
const gotLock = app.requestSingleInstanceLock();
if (!gotLock) app.quit();
else {
  app.on('second-instance', () => { if (win) { if (win.isMinimized()) win.restore(); win.show(); win.focus(); } });
  app.whenReady().then(() => {
    if (startupError) {
      dialog.showErrorBox('Moo-Thumbnail', 'تعذّر إنشاء مجلد بيانات البرنامج. انقل ملف EXE إلى مجلد يسمح بالكتابة ثم شغّله مجددًا.');
      app.quit();
      return;
    }
    session.defaultSession.setPermissionRequestHandler((_contents, _permission, callback) => callback(false));
    session.defaultSession.setPermissionCheckHandler(() => false);
    createWindow();
    app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
  });
}
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
