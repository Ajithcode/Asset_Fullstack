import { Injectable } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../../shared/api-code-store';
import { blankServiceProvider } from './service-provider.data';
import { ServiceProviderRecord } from './service-provider.model';

interface ServiceProviderDto extends MasterDto {
  serviceProviderName: string;
  website: string | null;
  primaryContact: string | null;
  serviceType: string | null;
  email: string | null;
  country: string | null;
  phone: string | null;
  description: string | null;
}

/**
 * Service providers from {projectApiUrl}api/master-data/service-providers. Services, licences,
 * address, documents and settings are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class ServiceProviderService extends ApiCodeStore<ServiceProviderRecord, ServiceProviderDto> {
  constructor() {
    super('service-providers');
    this.load();
  }

  countries(): string[] {
    return [...new Set(this.records.map((r) => r.country).filter(Boolean))].sort((a, b) => a.localeCompare(b));
  }

  protected toRecord(dto: ServiceProviderDto, cached: ServiceProviderRecord | undefined): ServiceProviderRecord {
    return {
      ...(cached ?? blankServiceProvider()),
      name: dto.serviceProviderName,
      website: dto.website ?? '',
      contactPerson: dto.primaryContact ?? '',
      serviceType: dto.serviceType ?? '',
      email: dto.email ?? '',
      country: dto.country ?? '',
      phone: dto.phone ?? ''
    };
  }

  protected toRequest(r: ServiceProviderRecord) {
    return {
      serviceProviderName: r.name,
      code: r.code,
      website: r.website || null,
      primaryContact: r.contactPerson,
      serviceType: r.serviceType,
      email: r.email,
      country: r.country,
      phone: r.phone
    };
  }
}
