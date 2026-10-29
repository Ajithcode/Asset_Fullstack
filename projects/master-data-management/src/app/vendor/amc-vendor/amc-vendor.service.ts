import { Injectable, inject } from '@angular/core';
import { AssetModelService } from '../../asset/asset-model/asset-model.service';
import { AssetTypeService } from '../../asset/asset-type/asset-type.service';
import { ApiCodeStore, MasterDto } from '../../shared/api-code-store';
import { blankAmcVendor } from './amc-vendor.data';
import { AmcVendorRecord } from './amc-vendor.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

interface AmcVendorDto extends MasterDto {
  amcVendorName: string;
  // "YYYY-MM-DD"
  startDate: string;
  endDate: string;
  primaryContact: string | null;
  assetType: ReferenceDto | null;
  email: string | null;
  contractType: string | null;
  // Asset model codes
  coveredModels: string[];
}

/**
 * AMC vendors from {projectApiUrl}api/master-data/amc-vendors. The asset type is shown by name and
 * linked by id. Contract value, billing, service levels, address, documents and settings are
 * UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class AmcVendorService extends ApiCodeStore<AmcVendorRecord, AmcVendorDto> {
  private readonly models = inject(AssetModelService);
  private readonly types = inject(AssetTypeService);

  constructor() {
    super('amc-vendors');
    this.load();
  }

  /** Registered assets of the covered models. */
  coveredAssets(r: AmcVendorRecord): number {
    return r.coveredModels.reduce((sum, code) => sum + (this.models.find(code)?.assetCount ?? 0), 0);
  }

  protected toRecord(dto: AmcVendorDto, cached: AmcVendorRecord | undefined): AmcVendorRecord {
    return {
      ...(cached ?? blankAmcVendor()),
      name: dto.amcVendorName,
      startDate: dto.startDate,
      endDate: dto.endDate,
      contactPerson: dto.primaryContact ?? '',
      assetType: dto.assetType?.name ?? '',
      email: dto.email ?? '',
      contractType: dto.contractType ?? '',
      coveredModels: dto.coveredModels
    };
  }

  protected toRequest(r: AmcVendorRecord) {
    const type = this.types.findByName(r.assetType);
    return {
      amcVendorName: r.name,
      code: r.code,
      startDate: r.startDate,
      endDate: r.endDate,
      primaryContact: r.contactPerson,
      assetType: type ? { id: type.id, name: type.name } : null,
      email: r.email,
      contractType: r.contractType,
      coveredModels: r.coveredModels
    };
  }
}
