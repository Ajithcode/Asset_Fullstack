import { Injectable } from '@angular/core';
import { apiText } from '../../people/people.data';
import { VendorBaseDto, vendorBaseFrom, vendorBaseRequest } from '../vendor.data';
import { VendorStore } from '../vendor-store';
import { SupplierRecord } from './supplier.model';

interface SupplierDto extends VendorBaseDto {
  supplierName: string;
  website: string | null;
  supplierLogo: string | null;
  primaryContact: string | null;
  category: string | null;
  email: string | null;
  country: string | null;
  phone: string | null;
  rating: number;
  products: string[];
  assetCategories: string[];
  brands: string[];
  currency: string | null;
  paymentTerms: string | null;
  creditLimit: number | null;
  trn: string | null;
  bankName: string | null;
  iban: string | null;
  preferred: boolean;
  approved: boolean;
  portalAccess: boolean;
  emailPurchaseOrders: boolean;
}

/** Suppliers from /api/master-data/suppliers. */
@Injectable({ providedIn: 'root' })
export class SupplierService extends VendorStore<SupplierRecord, SupplierDto> {
  protected readonly resource = 'suppliers';

  constructor() {
    super();
  }

  countries(): string[] {
    return [...new Set(this.records.map((r) => r.country).filter(Boolean))].sort((a, b) => a.localeCompare(b));
  }

  protected toRecord(dto: SupplierDto): SupplierRecord {
    return {
      ...vendorBaseFrom(dto, {
        name: dto.supplierName,
        logo: dto.supplierLogo,
        website: dto.website,
        contactPerson: dto.primaryContact,
        email: dto.email,
        phone: dto.phone
      }),
      category: dto.category ?? '',
      country: dto.country ?? '',
      rating: dto.rating ?? 0,
      products: dto.products ?? [],
      assetCategories: dto.assetCategories ?? [],
      brands: dto.brands ?? [],
      currency: dto.currency ?? '',
      paymentTerms: dto.paymentTerms ?? '',
      creditLimit: dto.creditLimit,
      trn: dto.trn ?? '',
      bankName: dto.bankName ?? '',
      iban: dto.iban ?? '',
      preferred: dto.preferred,
      approved: dto.approved,
      portalAccess: dto.portalAccess,
      emailPurchaseOrders: dto.emailPurchaseOrders
    };
  }

  protected toRequest(r: SupplierRecord): object {
    return {
      ...vendorBaseRequest(r),
      supplierName: r.name,
      website: apiText(r.website),
      supplierLogo: r.logo,
      primaryContact: apiText(r.contactPerson),
      category: apiText(r.category),
      email: apiText(r.email),
      country: apiText(r.country),
      phone: apiText(r.phone),
      rating: r.rating,
      products: r.products,
      assetCategories: r.assetCategories,
      brands: r.brands,
      currency: apiText(r.currency),
      paymentTerms: apiText(r.paymentTerms),
      creditLimit: r.creditLimit,
      trn: apiText(r.trn),
      bankName: apiText(r.bankName),
      iban: apiText(r.iban),
      preferred: r.preferred,
      approved: r.approved,
      portalAccess: r.portalAccess,
      emailPurchaseOrders: r.emailPurchaseOrders
    };
  }
}
