// Tag Management: one-off report of tags that are the same physical tag once normalised, e.g. C300009AAF43 and
// BLE-C300009AAF43. READ-ONLY: nothing is written. Same rule and grouping as GET /api/tag-ids/duplicates (TagIdKey):
//   trim; drop a leading "BLE-" (any case) only when a 12-hex-digit MAC follows; remove ':', '-' and whitespace; upper-case.
// A tag is matched on the key of its tag_id and of its value (MAC / EPC / encoded value / UID); deleted tags are skipped.
//
//   mongosh "<uri>" --quiet --file scripts/report-tracking-tag-duplicates.js            # database Asset360
//   mongosh "<uri>" --quiet --eval 'var DB="Other"' --file scripts/report-tracking-tag-duplicates.js

const database = db.getSiblingDB(typeof DB !== 'undefined' ? DB : 'Asset360');
const tags = database.getCollection('TrackingTag');

function strip(text) {
  return text.replace(/[:\-\s]/g, '').toUpperCase();
}

function tagKey(raw) {
  if (typeof raw !== 'string' || raw.trim() === '') return null;
  let text = raw.trim();
  if (/^ble-/i.test(text) && /^[0-9A-F]{12}$/.test(strip(text.slice(4)))) text = text.slice(4);
  const key = strip(text);
  return key === '' ? null : key;
}

const all = tags
  .find({ is_deleted: { $ne: true } }, {
    tag_id: 1, tag_number: 1, value: 1, technology: 1, status: 1,
    asset_registration_id: 1, asset_code: 1, asset_name: 1, created_at: 1, created_by: 1
  })
  .toArray();

const byKey = new Map();
for (const tag of all) {
  for (const key of new Set([tagKey(tag.tag_id), tagKey(tag.value)].filter(Boolean))) {
    if (!byKey.has(key)) byKey.set(key, []);
    byKey.get(key).push(tag);
  }
}

// Tags sharing both their id key and their value key form one group, listed with both keys.
const groups = new Map();
for (const [key, members] of byKey) {
  if (members.length < 2) continue;
  const id = members.map((t) => String(t._id)).sort().join('|');
  if (!groups.has(id)) groups.set(id, { keys: [], members });
  groups.get(id).keys.push(key);
}

print(`database: ${database.getName()}, tags checked: ${all.length}, duplicate groups: ${groups.size}`);
for (const { keys, members } of [...groups.values()].sort((a, b) => a.keys.sort()[0].localeCompare(b.keys.sort()[0]))) {
  print(`\nkey ${keys.sort().join(' / ')} (${members.length} tags)`);
  for (const t of members.sort((a, b) => (a.created_at ?? 0) - (b.created_at ?? 0))) {
    const on = [keys.includes(tagKey(t.tag_id)) && 'tag id', keys.includes(tagKey(t.value)) && 'value'].filter(Boolean).join(', ');
    const asset = t.asset_registration_id ? `${t.asset_registration_id} ${t.asset_code ?? ''} ${t.asset_name ?? ''}`.trim() : '-';
    const created = t.created_at instanceof Date ? t.created_at.toISOString() : '-';
    print(`  tag_id=${JSON.stringify(t.tag_id)} value=${JSON.stringify(t.value ?? null)} matched on ${on} | ${t.technology} | ${t.status ?? '-'} | asset ${asset} | created ${created} by ${t.created_by ?? '-'}`);
  }
}
