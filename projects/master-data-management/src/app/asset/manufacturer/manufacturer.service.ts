import { Injectable } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { AssetStore, activeStatus, localStamp } from '../asset-store';
import { blankManufacturer } from './manufacturer.data';
import { ManufacturerFilters, ManufacturerRecord, PaymentTerms } from './manufacturer.model';

type Address = ManufacturerRecord['address'];

/** Manufacturer as `/api/master-data/manufacturers` returns it. */
interface ManufacturerDto extends MdmAudit {
  manufacturerName: string;
  code: string;
  country: string | null;
  website: string | null;
  description: string | null;
  contactPerson: string | null;
  email: string | null;
  phoneNumber: string | null;
  logo: string | null;
  brandColor: string | null;
  designation: string | null;
  secondaryContact: string | null;
  secondaryEmail: string | null;
  secondaryPhone: string | null;
  address: { [K in keyof Address]: string | null } | null;
  supportPortal: string | null;
  supportEmail: string | null;
  supportPhone: string | null;
  standardWarrantyMonths: number | null;
  slaResponseHours: number | null;
  defaultCurrency: string | null;
  paymentTerms: string | null;
  preferred: boolean;
  warrantyRegistration: boolean;
  autoCreateWarranty: boolean;
}

/**
 * Manufacturers from the master data API. Has no dependencies so the Asset Model master can use it
 * for its Manufacturer list. A manufacturer's models are not sent; each model points at its maker.
 */
@Injectable({ providedIn: 'root' })
export class ManufacturerService extends AssetStore<ManufacturerRecord, ManufacturerDto> {
  protected readonly resource = 'manufacturers';

  constructor() {
    super();
  }

  /** Active manufacturers A-Z, plus `keep` so an existing choice stays selectable. */
  activeNames(keep = ''): string[] {
    return this.all()
      .filter((r) => r.status === 'Active' || r.name === keep)
      .map((r) => r.name)
      .sort((a, b) => a.localeCompare(b));
  }

  // `makesType` answers whether a manufacturer has a model of the chosen asset type
  filter(f: ManufacturerFilters, makesType: (name: string) => boolean): ManufacturerRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.assetType || makesType(r.name)) &&
        (!term || [r.name, r.code, r.country, r.primaryContact, r.email].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected toRecord(dto: ManufacturerDto): ManufacturerRecord {
    const blank = blankManufacturer();
    const a = dto.address;
    return {
      ...blank,
      code: dto.code,
      name: dto.manufacturerName,
      country: dto.country ?? '',
      website: dto.website ?? '',
      description: dto.description ?? '',
      logo: dto.logo,
      brandColor: dto.brandColor ?? blank.brandColor,
      status: activeStatus(dto.status),
      primaryContact: dto.contactPerson ?? '',
      designation: dto.designation ?? '',
      email: dto.email ?? '',
      phone: dto.phoneNumber ?? '',
      secondaryContact: dto.secondaryContact ?? '',
      secondaryEmail: dto.secondaryEmail ?? '',
      secondaryPhone: dto.secondaryPhone ?? '',
      address: {
        line1: a?.line1 ?? '',
        line2: a?.line2 ?? '',
        city: a?.city ?? '',
        state: a?.state ?? '',
        postalCode: a?.postalCode ?? '',
        country: a?.country ?? ''
      },
      supportPortal: dto.supportPortal ?? '',
      supportEmail: dto.supportEmail ?? '',
      supportPhone: dto.supportPhone ?? '',
      standardWarrantyMonths: dto.standardWarrantyMonths ?? blank.standardWarrantyMonths,
      slaResponseHours: dto.slaResponseHours ?? blank.slaResponseHours,
      defaultCurrency: dto.defaultCurrency ?? blank.defaultCurrency,
      paymentTerms: (dto.paymentTerms as PaymentTerms) ?? blank.paymentTerms,
      preferred: dto.preferred,
      warrantyRegistration: dto.warrantyRegistration,
      autoCreateWarranty: dto.autoCreateWarranty,
      createdOn: localStamp(dto.createdAt),
      createdBy: dto.createdBy ?? '',
      updatedOn: localStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: ManufacturerRecord): object {
    return {
      manufacturerName: r.name,
      code: r.code,
      country: r.country,
      website: r.website,
      description: r.description,
      contactPerson: r.primaryContact,
      email: r.email,
      phoneNumber: r.phone,
      logo: r.logo,
      brandColor: r.brandColor,
      designation: r.designation,
      secondaryContact: r.secondaryContact,
      secondaryEmail: r.secondaryEmail,
      secondaryPhone: r.secondaryPhone,
      address: r.address,
      supportPortal: r.supportPortal,
      supportEmail: r.supportEmail,
      supportPhone: r.supportPhone,
      standardWarrantyMonths: r.standardWarrantyMonths,
      slaResponseHours: r.slaResponseHours,
      defaultCurrency: r.defaultCurrency,
      paymentTerms: r.paymentTerms,
      preferred: r.preferred,
      warrantyRegistration: r.warrantyRegistration,
      autoCreateWarranty: r.autoCreateWarranty,
      status: r.status
    };
  }
}
