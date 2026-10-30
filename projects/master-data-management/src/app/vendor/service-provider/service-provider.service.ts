import { Injectable } from '@angular/core';
import { apiDate, apiText } from '../../people/people.data';
import { VendorBaseDto, vendorBaseFrom, vendorBaseRequest } from '../vendor.data';
import { VendorStore } from '../vendor-store';
import { ServiceProviderRecord } from './service-provider.model';

interface ServiceProviderDto extends VendorBaseDto {
  serviceProviderName: string;
  website: string | null;
  logo: string | null;
  primaryContact: string | null;
  serviceType: string | null;
  email: string | null;
  country: string | null;
  phone: string | null;
  description: string | null;
  rating: number;
  services: string[];
  skills: string[];
  coverage: string[];
  technicians: number;
  responseHours: number;
  licenceNo: string | null;
  licenceExpiry: string | null;
  insurancePolicy: string | null;
  insuranceExpiry: string | null;
  isoCertified: boolean;
  hseApproved: boolean;
  preferred: boolean;
  approved: boolean;
  portalAccess: boolean;
  emergencyCallout: boolean;
}

/** Service providers from /api/master-data/service-providers. Skills are held by name (the API follows renames). */
@Injectable({ providedIn: 'root' })
export class ServiceProviderService extends VendorStore<ServiceProviderRecord, ServiceProviderDto> {
  protected readonly resource = 'service-providers';
  // Not edited on the page; sent back unchanged so saving doesn't clear it
  private readonly descriptions = new Map<string, string | null>();

  constructor() {
    super();
  }

  countries(): string[] {
    return [...new Set(this.records.map((r) => r.country).filter(Boolean))].sort((a, b) => a.localeCompare(b));
  }

  protected toRecord(dto: ServiceProviderDto): ServiceProviderRecord {
    this.descriptions.set(dto.code, dto.description);
    return {
      ...vendorBaseFrom(dto, {
        name: dto.serviceProviderName,
        logo: dto.logo,
        website: dto.website,
        contactPerson: dto.primaryContact,
        email: dto.email,
        phone: dto.phone
      }),
      serviceType: dto.serviceType ?? '',
      country: dto.country ?? '',
      rating: dto.rating ?? 0,
      services: dto.services ?? [],
      skills: dto.skills ?? [],
      coverage: dto.coverage ?? [],
      technicians: dto.technicians ?? 0,
      responseHours: dto.responseHours ?? 24,
      licenceNo: dto.licenceNo ?? '',
      licenceExpiry: dto.licenceExpiry ?? '',
      insurancePolicy: dto.insurancePolicy ?? '',
      insuranceExpiry: dto.insuranceExpiry ?? '',
      isoCertified: dto.isoCertified,
      hseApproved: dto.hseApproved,
      preferred: dto.preferred,
      approved: dto.approved,
      portalAccess: dto.portalAccess,
      emergencyCallout: dto.emergencyCallout
    };
  }

  protected toRequest(r: ServiceProviderRecord): object {
    return {
      ...vendorBaseRequest(r),
      serviceProviderName: r.name,
      website: apiText(r.website),
      logo: r.logo,
      primaryContact: apiText(r.contactPerson),
      serviceType: apiText(r.serviceType),
      email: apiText(r.email),
      country: apiText(r.country),
      phone: apiText(r.phone),
      description: this.descriptions.get(r.code) ?? null,
      rating: r.rating,
      services: r.services,
      skills: r.skills,
      coverage: r.coverage,
      technicians: r.technicians,
      responseHours: r.responseHours,
      licenceNo: apiText(r.licenceNo),
      licenceExpiry: apiDate(r.licenceExpiry),
      insurancePolicy: apiText(r.insurancePolicy),
      insuranceExpiry: apiDate(r.insuranceExpiry),
      isoCertified: r.isoCertified,
      hseApproved: r.hseApproved,
      preferred: r.preferred,
      approved: r.approved,
      portalAccess: r.portalAccess,
      emergencyCallout: r.emergencyCallout
    };
  }
}
