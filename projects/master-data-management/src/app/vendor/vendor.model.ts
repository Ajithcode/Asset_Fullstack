export type VendorStatus = 'Active' | 'Inactive';

export interface VendorAddress {
  line1: string;
  line2: string;
  city: string;
  state: string;
  postalCode: string;
  country: string;
}

export interface VendorDocument {
  name: string;
  type: string;
  // YYYY-MM-DD; empty when it doesn't expire
  expiry: string;
}

/** Fields every vendor type has (supplier, service provider, AMC vendor). */
export interface VendorBase {
  /** Mongo id from the API; empty until the record is first saved. */
  id: string;
  code: string;
  name: string;
  // Uploaded logo (data URL); a coloured badge with the initials is shown when empty
  logo: string | null;
  brandColor: string;
  website: string;
  contactPerson: string;
  email: string;
  phone: string;
  status: VendorStatus;
  // Address & contact
  address: VendorAddress;
  alternateContact: string;
  alternateEmail: string;
  alternatePhone: string;
  documents: VendorDocument[];
  createdOn: string;
  updatedOn: string;
}
