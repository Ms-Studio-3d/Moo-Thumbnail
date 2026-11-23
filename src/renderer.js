'use strict';
const $ = id => document.getElementById(id);
const input = $('urlInput');
let current = null, selected = null, busy = false, copying = false;
function setStatus(message, type = '') {
  $('status').textContent = message;
  $('status').className = `status ${type}`;
}
function setBusy(value, action = 'lookup') {
  busy = value;
  input.disabled = value;
  $('extractBtn').disabled = value;
  $('extractBtn').textContent = value && action === 'lookup' ? 'جارٍ فحص الجودات…' : '↙ استخراج الصور';
  $('downloadBtn').disabled = value;
  $('downloadBtn').textContent = value && action === 'save' ? 'جارٍ الحفظ…' : '↓ حفظ الصورة';
  $('copyBtn').disabled = value;
  $('folderBtn').disabled = value;
  document.querySelectorAll('.quality-item input').forEach(radio => { radio.disabled = value || radio.dataset.available !== 'true'; });
  $('result').setAttribute('aria-busy', String(value));
}
function choose(key) {
  const item = current?.results.find(row => row.key === key && row.available);
  if (!item) return;
  selected = key;
  $('preview').src = item.preview;
  $('qualityTag').textContent = item.size;
  $('imageBytes').textContent = `${(item.bytes / 1024).toFixed(1)} KB · JPG`;
  document.querySelectorAll('.quality-item').forEach(row => {
    const radio = row.querySelector('input');
    radio.checked = radio.value === key;
    row.classList.toggle('selected', radio.checked);
  });
}
function render(data) {
  current = data;
  $('emptyState').classList.add('hidden');
  $('result').classList.remove('hidden');
  $('videoId').textContent = `VIDEO ID: ${data.id}`;
  const count = data.results.filter(item => item.available).length;
  $('qualityCount').textContent = `${count} / 6`;
  $('qualityList').replaceChildren();
  for (const item of data.results) {
    const row = document.createElement('label');
    row.className = `quality-item${item.available ? '' : ' unavailable'}`;
    const radio = document.createElement('input');
    radio.type = 'radio'; radio.name = 'quality'; radio.value = item.key;
    radio.disabled = !item.available; radio.dataset.available = String(item.available);
    const title = document.createElement('span');
    const name = document.createElement('span'); name.className = 'quality-name'; name.textContent = item.label;
    const size = document.createElement('span'); size.className = 'quality-size';
    size.textContent = item.available ? item.size : '—';
    title.append(name, size);
    const state = document.createElement('span'); state.className = 'quality-state';
    state.textContent = item.available ? 'متاحة' : item.reason === 'network' ? 'تعذّر الفحص' : 'غير متاحة';
    row.append(radio, title, state);
    radio.addEventListener('change', () => { if (!busy) choose(item.key); });
    $('qualityList').append(row);
  }
  choose(data.best);
  setStatus(`تم العثور على ${count} جودة متاحة.${data.results.some(item => item.reason === 'network') ? ' تعذّر فحص بعض الجودات؛ يمكنك إعادة المحاولة.' : ' اختر الجودة واحفظ صورتك.'}`, 'success');
}
async function extract(event) {
  event?.preventDefault();
  if (busy) return;
  if (!input.value.trim()) { setStatus('أضف رابط فيديو YouTube أولًا.', 'error'); input.focus(); return; }
  current = null; selected = null;
  $('result').classList.add('hidden'); $('emptyState').classList.remove('hidden');
  $('preview').removeAttribute('src');
  setBusy(true); setStatus('جارٍ فحص الجودات الست من YouTube…', 'busy');
  try { render(await window.studio.lookup(input.value.trim())); }
  catch (error) { setStatus(error.message || 'تعذّر استخراج الصور. حاول مرة أخرى.', 'error'); }
  finally { setBusy(false); }
}
$('urlForm').addEventListener('submit', extract);
$('downloadBtn').addEventListener('click', async () => {
  if (busy || !current || !selected) return;
  setBusy(true, 'save');
  try {
    const result = await window.studio.download({ id: current.id, key: selected });
    if (result.canceled) setStatus('تم إلغاء الحفظ. يمكنك اختيار مكان آخر.');
    else {
      setStatus(`تم حفظ الصورة بنجاح: ${result.filePath}`, 'success');
      $('folderBtn').classList.remove('hidden');
    }
  } catch (error) { setStatus(error.message || 'تعذّر حفظ الصورة. اختر مجلدًا آخر.', 'error'); }
  finally { setBusy(false); }
});
$('copyBtn').addEventListener('click', async () => {
  if (busy || copying || !current || !selected) return;
  copying = true;
  try { await window.studio.copy({ id: current.id, key: selected }); setStatus('تم نسخ رابط الصورة المباشر.', 'success'); }
  catch (error) { setStatus(error.message || 'تعذّر نسخ الرابط.', 'error'); }
  finally { copying = false; }
});
$('folderBtn').addEventListener('click', async () => {
  if (busy) return;
  try { await window.studio.openFolder(); }
  catch (error) { setStatus(error.message || 'تعذّر فتح المجلد.', 'error'); }
});
window.studio.info().then(info => { $('version').textContent = `v${info.version}`; }).catch(() => {});
