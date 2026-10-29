import { Injectable } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../../shared/api-code-store';
import { blankSupplier } from './supplier.data';
import { SupplierRecord } from './supplier.model';

interface SupplierDto extends MasterDto {
  supplierName: string;
  website: string | null;
  supplierLogo: string | null;
  primaryContact: string | null;
  category: string | null;
  email: string | null;
  country: string | null;
  phone: string | null;
}

/**
 * Suppliers from {projectApiUrl}api/master-data/suppliers. Products, financial details, address,
 * documents and settings are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class SupplierService extends ApiCodeStore<SupplierRecord, SupplierDto> {
  constructor() {
    super('suppliers');
    this.load();
  }

  countries(): string[] {
    return [...new Set(this.records.map((r) => r.country).filter(Boolean))].sort((a, b) => a.localeCompare(b));
  }

  protected toRecord(dto: SupplierDto, cached: SupplierRecord | undefined): SupplierRecord {
    return {
      ...(cached ?? blankSupplier()),
      name: dto.supplierName,
      website: dto.website ?? '',
      logo: dto.supplierLogo,
      contactPerson: dto.primaryContact ?? '',
      category: dto.category ?? '',
      email: dto.email ?? '',
      country: dto.country ?? '',
      phone: dto.phone ?? ''
    };
  }

  protected toRequest(r: SupplierRecord) {
    return {
      supplierName: r.name,
      code: r.code,
      website: r.website || null,
      // TODO: upload through the Media service and send its URL; this sends the picked image as a data URL
      supplierLogo: r.logo,
      primaryContact: r.contactPerson,
      category: r.category,
      email: r.email,
      country: r.country,
      phone: r.phone
    };
  }
}
