import { ManufacturerRecord, PaymentTerms } from './manufacturer.model';

export interface ManufacturerCountry {
  name: string;
  flag: string;
  dial: string;
}

export const MANUFACTURER_COUNTRIES: ManufacturerCountry[] = [
  { name: 'USA', flag: '🇺🇸', dial: '+1' },
  { name: 'China', flag: '🇨🇳', dial: '+86' },
  { name: 'Japan', flag: '🇯🇵', dial: '+81' },
  { name: 'South Korea', flag: '🇰🇷', dial: '+82' },
  { name: 'Taiwan', flag: '🇹🇼', dial: '+886' },
  { name: 'Germany', flag: '🇩🇪', dial: '+49' },
  { name: 'United Kingdom', flag: '🇬🇧', dial: '+44' },
  { name: 'France', flag: '🇫🇷', dial: '+33' },
  { name: 'Italy', flag: '🇮🇹', dial: '+39' },
  { name: 'Sweden', flag: '🇸🇪', dial: '+46' },
  { name: 'Finland', flag: '🇫🇮', dial: '+358' },
  { name: 'Denmark', flag: '🇩🇰', dial: '+45' },
  { name: 'Switzerland', flag: '🇨🇭', dial: '+41' },
  { name: 'Austria', flag: '🇦🇹', dial: '+43' },
  { name: 'Ireland', flag: '🇮🇪', dial: '+353' },
  { name: 'Liechtenstein', flag: '🇱🇮', dial: '+423' },
  { name: 'Latvia', flag: '🇱🇻', dial: '+371' },
  { name: 'Netherlands', flag: '🇳🇱', dial: '+31' },
  { name: 'India', flag: '🇮🇳', dial: '+91' },
  { name: 'United Arab Emirates', flag: '🇦🇪', dial: '+971' }
];

export const PAYMENT_TERMS: PaymentTerms[] = ['Advance', 'Net 15', 'Net 30', 'Net 45', 'Net 60'];

export function countryInfo(name: string): ManufacturerCountry | undefined {
  return MANUFACTURER_COUNTRIES.find((c) => c.name === name);
}

const PALETTE = ['#2563eb', '#0f766e', '#b45309', '#7c3aed', '#be123c', '#0369a1', '#4d7c0f', '#c2410c', '#334155', '#9333ea'];

/** Short label shown in the logo badge: DELL, HP, EN for Extreme Networks, L for Lenovo. */
export function badgeText(name: string): string {
  const words = name.split(/[\s-]+/).filter(Boolean);
  if (words.length > 1) return words.slice(0, 3).map((w) => w[0].toUpperCase()).join('');
  return name.length <= 4 ? name.toUpperCase() : name[0].toUpperCase();
}

/** Dark text on light brand colours (e.g. Caterpillar yellow), white otherwise. */
export function badgeInk(hex: string): string {
  const v = parseInt(hex.slice(1), 16);
  const luminance = (0.299 * (v >> 16) + 0.587 * ((v >> 8) & 255) + 0.114 * (v & 255)) / 255;
  return luminance > 0.6 ? '#111827' : '#ffffff';
}

export function blankManufacturer(): ManufacturerRecord {
  return {
    code: '',
    name: '',
    country: '',
    website: '',
    description: '',
    logo: null,
    brandColor: PALETTE[0],
    status: 'Active',
    primaryContact: '',
    designation: '',
    email: '',
    phone: '',
    secondaryContact: '',
    secondaryEmail: '',
    secondaryPhone: '',
    address: { line1: '', line2: '', city: '', state: '', postalCode: '', country: '' },
    supportPortal: '',
    supportEmail: '',
    supportPhone: '',
    standardWarrantyMonths: 12,
    slaResponseHours: 24,
    defaultCurrency: 'AED - UAE Dirham',
    paymentTerms: 'Net 30',
    preferred: false,
    warrantyRegistration: true,
    autoCreateWarranty: true,
    documents: 0,
    createdOn: '',
    createdBy: '',
    updatedOn: ''
  };
}

export const BRAND_PALETTE = PALETTE;
