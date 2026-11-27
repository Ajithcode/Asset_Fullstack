// mongosh --quiet --file shadow-compare.js  (read-only; DB Asset360Dev). Compares the fast resolver's shadow decisions (every variant)
// with the 15 s resolver's real zone changes ("official", seen as TagLocationState changes) per trolley:
// zone switches, flips (A -> B -> A within 60 s) and how many seconds earlier each variant decided the same change.
// Optional: SINCE = ISO time, e.g. mongosh --eval "var SINCE='2026-11-27T09:50:20Z'" --file shadow-compare.js
const d = db.getSiblingDB('Asset360Dev');
const since = (typeof SINCE !== 'undefined' ? new Date(SINCE) : new Date(Date.now() - 3600e3)).getTime();
const zoneName = {}; d.maps_plots.find({ kind: 'device', is_deleted: { $ne: true } }).forEach(p => { const z = p.zone_id || (p.location && p.location.zone_id); if (z) zoneName[z] = p.zone_name || (p.location && p.location.zone_name) || z; });
const asset = {}; d.TrackingTag.find({}, { tag_id: 1, asset_registration_id: 1 }).forEach(t => asset[t.tag_id] = t.asset_registration_id);
const label = t => `${asset[t] || '?'} (${t.slice(-4)})`;
const logs = d.LocationShadowLog.find({ atMs: { $gte: since }, type: { $in: ['decision', 'official'] } }).sort({ atMs: 1 }).toArray();
const last = logs.length ? logs[logs.length - 1].atMs : since;
print(`window ${new Date(since).toISOString()} .. ${new Date(last).toISOString()}  (${((last - since) / 60000).toFixed(0)} min), ${logs.length} events`);
const variants = [...new Set(logs.map(l => l.variant))].sort((a, b) => (a === 'official' ? -1 : b === 'official' ? 1 : a.localeCompare(b)));
const changes = (v, tag) => logs.filter(l => l.variant === v && l.tag === tag && l.kind === 'ZoneChange');
const tags = [...new Set(logs.map(l => l.tag))].sort();
const flips = list => { let f = 0; for (let i = 2; i < list.length; i++) if (list[i].toZone === list[i - 2].toZone && list[i].atMs - list[i - 2].atMs <= 60000) f++; return f; };
const med = a => a.length ? a.slice().sort((x, y) => x - y)[Math.floor((a.length - 1) / 2)] : null;

print('\n== zone switches / flips per trolley (switches/flips)');
print(['trolley'.padEnd(18), ...variants.map(v => v.padEnd(14))].join(' '));
const tot = Object.fromEntries(variants.map(v => [v, [0, 0]]));
for (const t of tags) {
  const cells = variants.map(v => { const c = changes(v, t); tot[v][0] += c.length; const f = flips(c); tot[v][1] += f; return `${c.length}/${f}`.padEnd(14); });
  print([label(t).padEnd(18), ...cells].join(' '));
}
print(['TOTAL'.padEnd(18), ...variants.map(v => `${tot[v][0]}/${tot[v][1]}`.padEnd(14))].join(' '));

print('\n== seconds earlier than the 15 s resolver (same change: first switch into the same zone from -90 s to +30 s)');
for (const v of variants.filter(x => x !== 'official')) {
  const leads = []; let unmatched = 0; const rows = [];
  for (const t of tags) for (const o of changes('official', t)) {
    const m = changes(v, t).find(c => c.toZone === o.toZone && c.atMs >= o.atMs - 90000 && c.atMs <= o.atMs + 30000);
    if (m) { const lead = (o.atMs - m.atMs) / 1000; leads.push(lead); rows.push(`${new Date(o.atMs).toISOString().slice(11, 19)} ${label(t)} -> ${zoneName[o.toZone] || o.toZone}: ${lead.toFixed(1)} s`); }
    else unmatched++;
  }
  const extra = tags.reduce((n, t) => n + changes(v, t).filter(c => !changes('official', t).some(o => o.toZone === c.toZone && Math.abs(o.atMs - c.atMs) <= 90000)).length, 0);
  print(`${v}: matched ${leads.length}, official changes not matched ${unmatched}, variant-only changes ${extra}; lead median ${med(leads)} s, min ${leads.length ? Math.min(...leads) : null}, max ${leads.length ? Math.max(...leads) : null}`);
  if (process.env.VERBOSE || typeof VERBOSE !== 'undefined') rows.forEach(r => print('   ' + r));
}
