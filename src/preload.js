const { contextBridge, ipcRenderer } = require('electron');
async function invoke(channel, payload) {
  const response = await ipcRenderer.invoke(channel, payload);
  if (!response.ok) throw new Error(response.error.message);
  return response.data;
}
contextBridge.exposeInMainWorld('studio', Object.freeze({
  lookup: url => invoke('thumbnail:lookup', url),
  download: selection => invoke('thumbnail:download', selection),
  copy: selection => invoke('thumbnail:copy', selection),
  openFolder: () => invoke('app:open-folder'),
  info: () => invoke('app:info')
}));
