import { ChangeStatus, ConfigSetting, ConfigValue } from './system-configuration.data';

const TYPE_LABELS: Record<ConfigSetting['type'], string> = {
  text: 'Text',
  number: 'Number',
  select: 'Dropdown',
  toggle: 'Toggle',
  email: 'Email',
  url: 'URL',
  logo: 'Image upload'
};

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const HTTPS_URL = /^https:\/\/[^\s/$.?#][^\s]*\.[^\s]+$/i;

export function describeType(s: ConfigSetting): string {
  return TYPE_LABELS[s.type];
}

/** Human readable value, e.g. "Enabled" or "30 minutes". */
export function describeValue(s: ConfigSetting, v: ConfigValue): string {
  if (s.type === 'toggle') return v ? 'Enabled' : 'Disabled';
  if (s.type === 'logo') return v ? 'Custom logo' : 'Default Asset360 logo';
  if (s.type === 'number' && s.unit) return `${v} ${s.unit}`;
  return v === '' ? '—' : String(v);
}

export function allowedValues(s: ConfigSetting): string {
  if (s.allowed) return s.allowed;
  switch (s.type) {
    case 'select':
      return (s.options ?? []).join(', ');
    case 'toggle':
      return 'Enabled / Disabled';
    case 'number':
      return `${s.min ?? 0} – ${s.max ?? '∞'}${s.unit ? ' ' + s.unit : ''}`;
    case 'email':
      return 'A valid email address';
    case 'url':
      return 'An https:// URL';
    case 'logo':
      return 'PNG or JPG, up to 2MB';
    default:
      return s.required ? 'Free text (required, max 120 characters)' : 'Free text (max 120 characters)';
  }
}

/** Validation message for a working value, or '' when it is valid. */
export function validateSetting(s: ConfigSetting, v: ConfigValue): string {
  if (s.type === 'toggle' || s.type === 'logo') return '';
  if (s.type === 'number') {
    if (v === '' || v === null || typeof v !== 'number' || Number.isNaN(v)) return 'Enter a number.';
    if (!Number.isInteger(v)) return 'Use a whole number.';
    if (s.min !== undefined && v < s.min) return `Minimum is ${s.min}.`;
    if (s.max !== undefined && v > s.max) return `Maximum is ${s.max}.`;
    return '';
  }
  const text = String(v).trim();
  if (!text) return s.required ? `${s.label} is required.` : '';
  if (text.length > 120) return 'Keep it under 120 characters.';
  if (s.type === 'select' && s.options && !s.options.includes(text)) return 'Pick a value from the list.';
  if (s.type === 'email' && !EMAIL.test(text)) return 'Enter a valid email address.';
  if (s.type === 'url' && !HTTPS_URL.test(text)) return 'Enter a valid https:// URL.';
  return '';
}

export function changeTone(status: ChangeStatus): string {
  return status === 'Applied' ? 'green' : status === 'Pending' ? 'orange' : 'red';
}
