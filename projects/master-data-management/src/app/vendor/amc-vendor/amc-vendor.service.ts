import { Injectable, inject } from '@angular/core';
import { AssetModelService } from '../../asset/asset-model/asset-model.service';
import { MasterLookups } from '../../people/name-lookup';
import { MdmRef, apiText } from '../../people/people.data';
import { VendorBaseDto, vendorBaseFrom, vendorBaseRequest } from '../vendor.data';
import { VendorStore } from '../vendor-store';
import { AmcVendorRecord } from './amc-vendor.model';

interface AmcVendorDto extends VendorBaseDto {
  amcVendorName: string;
  startDate: string;
  endDate: string;
  primaryContact: string | null;
  assetType: MdmRef | null;
  email: string | null;
  contractType: string | null;
  logo: string | null;
  website: string | null;
  phone: string | null;
  contractNo: string | null;
  contractValue: number | null;
  currency: string | null;
  billingCycle: string | null;
  renewalNoticeDays: number;
  autoRenew: boolean;
  coveredModels: string[];
  visitsPerYear: number;
  responseHours: number;
  resolutionHours: number;
  uptimePercent: number;
  penaltyPerHour: number | null;
  notifyBeforeExpiry: boolean;
  serviceReportRequired: boolean;
}

/**
 * AMC vendors from /api/master-data/amc-vendors. The asset type is linked by name (with the asset type's
 * id when one of that name exists); covered models are asset model codes.
 */
@Injectable({ providedIn: 'root' })
export class AmcVendorService extends VendorStore<AmcVendorRecord, AmcVendorDto> {
  protected readonly resource = 'amc-vendors';
  private readonly models = inject(AssetModelService);
  private readonly lookups = inject(MasterLookups);

  constructor() {
    super();
    this.lookups.assetTypes.ensureLoaded();
  }

  /** Registered assets of the covered models. */
  coveredAssets(r: AmcVendorRecord): number {
    return r.coveredModels.reduce((sum, code) => sum + (this.models.find(code)?.assetCount ?? 0), 0);
  }

  protected toRecord(dto: AmcVendorDto): AmcVendorRecord {
    return {
      ...vendorBaseFrom(dto, {
        name: dto.amcVendorName,
        logo: dto.logo,
        website: dto.website,
        contactPerson: dto.primaryContact,
        email: dto.email,
        phone: dto.phone
      }),
      assetType: dto.assetType?.name ?? '',
      contractType: dto.contractType ?? '',
      startDate: dto.startDate,
      endDate: dto.endDate,
      contractNo: dto.contractNo ?? '',
      contractValue: dto.contractValue,
      currency: dto.currency ?? '',
      billingCycle: dto.billingCycle ?? '',
      renewalNoticeDays: dto.renewalNoticeDays ?? 0,
      autoRenew: dto.autoRenew,
      coveredModels: dto.coveredModels ?? [],
      visitsPerYear: dto.visitsPerYear ?? 0,
      responseHours: dto.responseHours ?? 0,
      resolutionHours: dto.resolutionHours ?? 0,
      uptimePercent: dto.uptimePercent ?? 0,
      penaltyPerHour: dto.penaltyPerHour,
      notifyBeforeExpiry: dto.notifyBeforeExpiry,
      serviceReportRequired: dto.serviceReportRequired
    };
  }

  protected toRequest(r: AmcVendorRecord): object {
    return {
      ...vendorBaseRequest(r),
      amcVendorName: r.name,
      startDate: r.startDate,
      endDate: r.endDate,
      primaryContact: apiText(r.contactPerson),
      assetType: this.lookups.assetTypes.ref(r.assetType),
      email: apiText(r.email),
      contractType: apiText(r.contractType),
      logo: r.logo,
      website: apiText(r.website),
      phone: apiText(r.phone),
      contractNo: apiText(r.contractNo),
      contractValue: r.contractValue,
      currency: apiText(r.currency),
      billingCycle: apiText(r.billingCycle),
      renewalNoticeDays: r.renewalNoticeDays,
      autoRenew: r.autoRenew,
      coveredModels: r.coveredModels,
      visitsPerYear: r.visitsPerYear,
      responseHours: r.responseHours,
      resolutionHours: r.resolutionHours,
      uptimePercent: r.uptimePercent,
      penaltyPerHour: r.penaltyPerHour,
      notifyBeforeExpiry: r.notifyBeforeExpiry,
      serviceReportRequired: r.serviceReportRequired
    };
  }
}
