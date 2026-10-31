/** API shapes and helpers shared by the organization structure masters. */

export interface StructureReference {
  id: string | null;
  name: string | null;
}

export interface StructureAddressDto {
  line1: string | null;
  line2: string | null;
  city: string | null;
  state: string | null;
  country: string | null;
  postalCode: string | null;
}

export interface StructureContactDto {
  name: string | null;
  designation: string | null;
  email: string | null;
  phoneCountry: string | null;
  phone: string | null;
}

export interface StructureAddress {
  line1: string;
  line2: string;
  city: string;
  state: string;
  country: string;
  postalCode: string;
}

export function fromApiAddress(a: StructureAddressDto | null | undefined): StructureAddress {
  return {
    line1: a?.line1 ?? '',
    line2: a?.line2 ?? '',
    city: a?.city ?? '',
    state: a?.state ?? '',
    country: a?.country ?? '',
    postalCode: a?.postalCode ?? ''
  };
}

/** Reference body for a record picked by id; null when there is no id. */
export function reference(id: string | null | undefined, name: string | null | undefined = null): StructureReference | null {
  return id ? { id, name: name ?? null } : null;
}

/** API timestamp (UTC ISO) -> local 'YYYY-MM-DDTHH:mm', the format the pages display. */
export function toLocalStamp(value: string | null | undefined): string {
  if (!value) return '';
  const date = new Date(value);
  if (isNaN(date.getTime())) return value;
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}
