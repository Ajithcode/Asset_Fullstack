/**
 * One shared time zone list for every picker: all IANA ids the browser knows
 * (Intl.supportedValuesOf('timeZone')), labelled "(UTC+05:30) Asia/Kolkata".
 */
export const DEFAULT_TIME_ZONE = 'Asia/Kolkata';

export interface TimeZoneOption {
  /** IANA id, e.g. Asia/Kolkata */
  value: string;
  /** "(UTC+05:30) Asia/Kolkata" */
  label: string;
  offsetMinutes: number;
}

/** Small fallback for browsers without Intl.supportedValuesOf. */
const FALLBACK_IDS = [
  'UTC', 'Africa/Cairo', 'Africa/Johannesburg', 'Africa/Lagos', 'Africa/Nairobi', 'America/Chicago', 'America/Denver', 'America/Los_Angeles',
  'America/New_York', 'America/Sao_Paulo', 'America/Toronto', 'Asia/Baghdad', 'Asia/Bangkok', 'Asia/Colombo', 'Asia/Dhaka', 'Asia/Dubai', 'Asia/Hong_Kong',
  'Asia/Jakarta', 'Asia/Karachi', 'Asia/Kathmandu', 'Asia/Kolkata', 'Asia/Kuwait', 'Asia/Manila', 'Asia/Muscat', 'Asia/Qatar', 'Asia/Riyadh', 'Asia/Seoul',
  'Asia/Shanghai', 'Asia/Singapore', 'Asia/Tehran', 'Asia/Tokyo', 'Australia/Perth', 'Australia/Sydney', 'Europe/Berlin', 'Europe/Istanbul', 'Europe/London',
  'Europe/Madrid', 'Europe/Moscow', 'Europe/Paris', 'Pacific/Auckland'
];

/** Old picker values (labels the previous dropdowns saved) mapped to their IANA id, so saved data still shows up selected. */
const LEGACY: Record<string, string> = {
  dubai: 'Asia/Dubai', 'abu dhabi': 'Asia/Dubai', riyadh: 'Asia/Riyadh', kuwait: 'Asia/Kuwait', doha: 'Asia/Qatar', qatar: 'Asia/Qatar', muscat: 'Asia/Muscat',
  kolkata: 'Asia/Kolkata', mumbai: 'Asia/Kolkata', chennai: 'Asia/Kolkata', london: 'Europe/London', 'new york': 'America/New_York', singapore: 'Asia/Singapore',
  utc: 'UTC'
};

function offsetMinutes(id: string, at: Date): number {
  try {
    const part = new Intl.DateTimeFormat('en-US', { timeZone: id, timeZoneName: 'longOffset' }).formatToParts(at).find((p) => p.type === 'timeZoneName')?.value ?? 'GMT';
    const m = /GMT([+-])(\d{1,2})(?::?(\d{2}))?/.exec(part);
    if (!m) return 0;
    return (m[1] === '-' ? -1 : 1) * (Number(m[2]) * 60 + Number(m[3] ?? 0));
  } catch {
    return 0;
  }
}

export function formatOffset(minutes: number): string {
  const a = Math.abs(minutes);
  return `UTC${minutes < 0 ? '-' : '+'}${String(Math.floor(a / 60)).padStart(2, '0')}:${String(a % 60).padStart(2, '0')}`;
}

export function timeZoneLabel(id: string, at: Date = new Date()): string {
  return `(${formatOffset(offsetMinutes(id, at))}) ${id}`;
}

let cache: TimeZoneOption[] | null = null;

/** Every time zone, sorted by UTC offset then name. Computed once. */
export function allTimeZones(): TimeZoneOption[] {
  if (cache) return cache;
  let ids: string[];
  try {
    ids = (Intl as unknown as { supportedValuesOf?: (k: string) => string[] }).supportedValuesOf?.('timeZone') ?? FALLBACK_IDS;
  } catch {
    ids = FALLBACK_IDS;
  }
  if (!ids.includes('UTC')) ids = [...ids, 'UTC'];
  const now = new Date();
  cache = ids
    .map((value) => {
      const offset = offsetMinutes(value, now);
      return { value, label: `(${formatOffset(offset)}) ${value}`, offsetMinutes: offset };
    })
    .sort((a, b) => a.offsetMinutes - b.offsetMinutes || a.value.localeCompare(b.value));
  return cache;
}

/** An old label or a bare id → the IANA id ('' when empty, the text itself when unknown). */
export function toTimeZoneId(value: string | null | undefined): string {
  const v = (value ?? '').trim();
  if (!v) return '';
  if (allTimeZones().some((z) => z.value === v)) return v;
  const bare = v.replace(/^\([^)]*\)\s*/, '').trim();
  if (allTimeZones().some((z) => z.value === bare)) return bare;
  const stripped = bare.toLowerCase();
  const first = stripped.split(',')[0].trim();
  return LEGACY[stripped] ?? LEGACY[first] ?? v;
}

/** Shown text for a stored value (label for known ids, the raw value otherwise). */
export function displayTimeZone(value: string | null | undefined): string {
  const id = toTimeZoneId(value);
  return id && allTimeZones().some((z) => z.value === id) ? timeZoneLabel(id) : (value ?? '');
}
