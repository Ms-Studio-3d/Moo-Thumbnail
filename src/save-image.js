'use strict';
const fs = require('node:fs/promises');
const path = require('node:path');
const { randomUUID } = require('node:crypto');
async function saveImage(filePath, buffer) {
  const destination = /\.jpe?g$/i.test(filePath) ? filePath : `${filePath}.jpg`;
  const temporary = path.join(path.dirname(destination), `.moo-thumbnail-${randomUUID()}.tmp`);
  try {
    await fs.writeFile(temporary, buffer, { flag: 'wx' });
    await fs.rename(temporary, destination);
    return destination;
  } finally { await fs.unlink(temporary).catch(() => {}); }
}
module.exports = { saveImage };
