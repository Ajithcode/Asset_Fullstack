import { BusinessUnitRecord } from './business-unit.model';

/* Option lists and the blank record for the business unit form. Records come from the API. */

export const EMPLOYEES = [
  'Suresh Nair', 'Rex Kumar', 'Aisha Rahman', 'Omar Haddad', 'Priya Nair', 'Faisal Al Harbi',
  'Divya Raman', 'Karthik R', 'Fatima Ali', 'Murugan S', 'Ahmed Saleh', 'Meera Iyer',
  'Amit Sharma', 'Ravi Prakash', 'Khalid Hassan'
];

export const COST_CENTERS = [
  'CC-MFG-001', 'CC-ENG-001', 'CC-LOG-001', 'CC-FM-001', 'CC-RET-001', 'CC-CORP-001', 'CC-OPS-001', 'CC-IT-001',
  'CC-QA-001', 'CC-HSE-001', 'CC-RND-001', 'CC-SNM-001', 'CC-FIN-001', 'CC-HR-001', 'CC-PRJ-001'
];

export function blankBusinessUnit(): BusinessUnitRecord {
  const today = new Date().toISOString().slice(0, 10);
  return {
    code: '',
    name: '',
    organizationCode: '',
    parentCode: null,
    icon: 'building',
    logo: null,
    description: '',
    head: '',
    costCenter: '',
    currency: 'AED - UAE Dirham',
    status: 'Active',
    address: { line1: '', line2: '', city: '', state: '', country: 'United Arab Emirates', postalCode: '' },
    contactEmail: '',
    phoneCountry: 'United Arab Emirates',
    phone: '',
    effectiveFrom: today,
    effectiveTo: '',
    allowSites: true,
    budgetTracking: true,
    assetNumbering: true,
    approvals: false,
    createdOn: '',
    updatedOn: ''
  };
}
