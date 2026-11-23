'use strict';
const VARIANTS = Object.freeze([
  { key: 'maxresdefault', label: 'أعلى جودة', minWidth: 1280, minHeight: 720 },
  { key: 'hq720', label: 'جودة HD', minWidth: 1280, minHeight: 720 },
  { key: 'sddefault', label: 'جودة SD', minWidth: 640, minHeight: 480 },
  { key: 'hqdefault', label: 'جودة عالية', minWidth: 480, minHeight: 360 },
  { key: 'mqdefault', label: 'جودة متوسطة', minWidth: 320, minHeight: 180 },
  { key: 'default', label: 'الجودة الأساسية', minWidth: 120, minHeight: 90 }
]);
const MAX_BYTES = 8 * 1024 * 1024;
class ThumbnailError extends Error {
  constructor(code, message) { super(message); this.code = code; }
}
function extractVideoId(input) {
  if (typeof input !== 'string' || input.length > 4096 || !input.trim()) {
    throw new ThumbnailError('INVALID_URL', 'أدخل رابط فيديو YouTube صحيحًا.');
  }
  let value = input.trim();
  if (/^(?:(?:www|m|music)\.)?(?:youtube\.com|youtu\.be)\//i.test(value)) value = `https://${value}`;
  let url;
  try { url = new URL(value); } catch {
    throw new ThumbnailError('INVALID_URL', 'الرابط غير صحيح. انسخ رابط الفيديو كاملًا.');
  }
  if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password || url.port) {
    throw new ThumbnailError('INVALID_URL', 'استخدم رابط فيديو من YouTube فقط.');
  }
  const host = url.hostname.toLowerCase();
  const parts = url.pathname.split('/').filter(Boolean);
  let id;
  if (host === 'youtu.be' || host === 'www.youtu.be') {
    if (parts.length === 1) id = parts[0];
  } else if (['youtube.com', 'www.youtube.com', 'm.youtube.com', 'music.youtube.com',
    'youtube-nocookie.com', 'www.youtube-nocookie.com'].includes(host)) {
    if (url.pathname.replace(/\/$/, '') === '/watch') id = url.searchParams.get('v');
    else if (['shorts', 'embed', 'live', 'v'].includes(parts[0]) && parts.length === 2) id = parts[1];
  }
  if (!/^[A-Za-z0-9_-]{11}$/.test(id || '')) {
    throw new ThumbnailError('INVALID_URL', 'تعذّر العثور على فيديو في هذا الرابط. استخدم رابط فيديو أو Shorts أو Live.');
  }
  return id;
}
function thumbnailUrl(id, key) {
  if (!/^[A-Za-z0-9_-]{11}$/.test(id) || !VARIANTS.some(v => v.key === key)) {
    throw new ThumbnailError('INVALID_SELECTION', 'اختر جودة متاحة أولًا.');
  }
  return `https://i.ytimg.com/vi/${id}/${key}.jpg`;
}
async function fetchJpeg(url, fetchImpl, getSize, timeout = 12000) {
  try {
    const response = await fetchImpl(url, {
      redirect: 'error', signal: AbortSignal.timeout(timeout), headers: { Accept: 'image/jpeg' }
    });
    if ([404, 410].includes(response.status)) return null;
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    if (!/^image\/jpeg(?:;|$)/i.test(response.headers.get('content-type') || '')) throw new Error('Invalid image type');
    if (Number(response.headers.get('content-length')) > MAX_BYTES) throw new Error('Image too large');
    const reader = response.body.getReader();
    const chunks = [];
    let length = 0;
    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        length += value.byteLength;
        if (length > MAX_BYTES) throw new Error('Image too large');
        chunks.push(Buffer.from(value));
      }
    } finally { await reader.cancel().catch(() => {}); }
    const buffer = Buffer.concat(chunks);
    if (buffer.length < 4 || buffer[0] !== 0xff || buffer[1] !== 0xd8) throw new Error('Invalid JPEG');
    const { width, height } = getSize(buffer);
    if (!width || !height) throw new Error('Invalid image dimensions');
    return { buffer, width, height };
  } catch {
    throw new ThumbnailError('NETWORK', 'تعذّر الاتصال بخادم الصور. تحقق من الإنترنت ثم حاول مرة أخرى.');
  }
}
async function lookupThumbnails(input, { fetchImpl = fetch, getSize, timeout } = {}) {
  const id = extractVideoId(input);
  const images = new Map();
  const results = await Promise.all(VARIANTS.map(async variant => {
    const url = thumbnailUrl(id, variant.key);
    const result = { key: variant.key, label: variant.label, url, available: false, reason: 'missing' };
    try {
      const image = await fetchJpeg(url, fetchImpl, getSize, timeout);
      // Missing variants can return a 120 × 90 placeholder with HTTP 200.
      if (image && image.width >= variant.minWidth && image.height >= variant.minHeight) {
        images.set(variant.key, image.buffer);
        Object.assign(result, {
          available: true, reason: null, width: image.width, height: image.height,
          bytes: image.buffer.length, size: `${image.width} × ${image.height}`,
          preview: `data:image/jpeg;base64,${image.buffer.toString('base64')}`
        });
      }
    } catch { result.reason = 'network'; }
    return result;
  }));
  const available = results.filter(item => item.available).sort((a, b) => b.width * b.height - a.width * a.height);
  if (!available.length) {
    if (results.some(item => item.reason === 'network')) throw new ThumbnailError('NETWORK', 'تعذّر فحص الصور. تحقق من الإنترنت ثم أعد المحاولة.');
    throw new ThumbnailError('NOT_FOUND', 'لم نعثر على صور لهذا الفيديو. قد يكون الرابط غير متاح أو الفيديو محذوفًا.');
  }
  return { data: { id, results, best: available[0].key }, images };
}
module.exports = { VARIANTS, MAX_BYTES, ThumbnailError, extractVideoId, thumbnailUrl, fetchJpeg, lookupThumbnails };
