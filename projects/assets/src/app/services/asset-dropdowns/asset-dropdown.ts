import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, forkJoin, map, of, retry, timer } from 'rxjs';
import { environment } from 'shared-ui';

/**
 * Dropdown data for the asset screens.
 * - Master data: {assetMasterDataApiUrl}swagger/index.html (api/master-data/*)
 * - Fixed lists: GET {assetRegistrationApiUrl}api/asset-registrations/options
 * Every call falls back to an empty list on error, so one failing source never blanks the others.
 */

/** One dropdown entry. `value` is the master record id, or the text itself for fixed lists. */
export interface DropdownOption {
  label: string;
  value: string;
  code?: string;
  /** Badge color, for conditions and criticalities. */
  color?: string;
  /** Id of the parent record: parent category, category of an asset type, department of an employee or team. */
  parentId?: string;
  /** The record flagged as the default choice (e.g. defaultCriticality). */
  isDefault?: boolean;
}

/** Lists from GET /api/asset-registrations/options, keyed by wizard step then list name. */
export interface AssetRegistrationOptions {
  basicInformation?: { statuses?: string[] };
  classification?: { functionPurposes?: string[]; taggingMethods?: string[]; complianceCategories?: string[]; lifecycleStages?: string[] };
  locationAssignment?: { ownershipTypes?: string[] };
  purchaseWarranty?: {
    quantityUnits?: string[];
    /** "AED - UAE Dirham" */
    currencies?: string[];
    paymentTerms?: string[];
    procurementMethods?: string[];
    warrantyTypes?: string[];
    warrantyPeriodUnits?: string[];
    supportCoverages?: string[];
  };
  additionalInformation?: {
    powerRatingUnits?: string[];
    voltageUnits?: string[];
    frequencyUnits?: string[];
    phases?: string[];
    fuelTypes?: string[];
    capacityUnits?: string[];
    dimensionUnits?: string[];
    weightUnits?: string[];
    temperatureUnits?: string[];
    ipRatings?: string[];
    noiseLevelUnits?: string[];
    lifespanUnits?: string[];
    operatingHoursUnits?: string[];
    operatingShifts?: string[];
    utilizations?: string[];
    maintenanceFrequencyUnits?: string[];
    complianceStandards?: string[];
    impactLevels?: string[];
    customFieldTypes?: string[];
  };
}

/** A site, building, floor, zone or room. Ids link the levels, except rooms, which name their parents. */
export interface LocationOption extends DropdownOption {
  /** Site: country name. */
  country?: string;
  /** Floor / zone: site id. */
  siteId?: string;
  /** Zone: building id. */
  buildingId?: string;
  /** Room: names of its zone, floor and building. */
  zoneName?: string;
  floorName?: string;
  buildingName?: string;
}

export interface LocationHierarchy {
  sites: LocationOption[];
  buildings: LocationOption[];
  floors: LocationOption[];
  zones: LocationOption[];
  rooms: LocationOption[];
}

/** `{ id, name }` link to another master record. */
interface MasterReference {
  id?: string | null;
  name?: string | null;
}

/** Fields shared by the master-data list responses; each resource names its label field differently. */
type MasterRecord = Record<string, unknown> & {
  id?: string;
  code?: string | null;
  status?: string | null;
  color?: string | null;
};

/** Plain text options, e.g. ['kVA', 'kW'] -> [{ label: 'kVA', value: 'kVA' }]. */
export function textOptions(list: readonly string[] | null | undefined): DropdownOption[] {
  return (list ?? []).map((label) => ({ label, value: label }));
}

@Injectable({ providedIn: 'root' })
export class AssetDropdownService {
  private masterUrl = `${environment.assetMasterDataApiUrl}api/master-data`;
  /** Asset service: categories and asset types are kept there (the same lists the Assets pages edit). */
  private assetUrl = `${environment.assetApiUrl}api`;
  private registrationUrl = `${environment.assetRegistrationApiUrl}api/asset-registrations`;

  constructor(private http: HttpClient) {}

  // ===== Fixed lists (asset registration API) =====

  /** GET /api/asset-registrations/options */
  getRegistrationOptions(): Observable<AssetRegistrationOptions> {
    return this.http
      .get<AssetRegistrationOptions>(`${this.registrationUrl}/options`)
      // Retried a couple of times (e.g. while the backend restarts) before giving up with no lists
      .pipe(retry({ count: 2, delay: (_err, n) => timer(1000 * n) }), catchError(() => of({} as AssetRegistrationOptions)));
  }

  // ===== Master data =====

  /** GET /api/master-data/criticalities — isDefault marks the record with defaultCriticality: true. */
  getCriticalities(): Observable<DropdownOption[]> {
    return this.list('criticalities', 'criticalityName', (r) => ({ isDefault: r['defaultCriticality'] === true }));
  }

  /** GET {asset service}/api/asset-categories — top-level categories have no parentId; sub-categories carry their parent's id. */
  getAssetCategories(): Observable<DropdownOption[]> {
    return this.list(`${this.assetUrl}/asset-categories`, 'name', (r) => ({ parentId: this.text(r['parentCategoryId']) }));
  }

  /** GET {asset service}/api/asset-categories?parentCategoryId={id} — sub-categories of one category. */
  getSubCategories(categoryId: string): Observable<DropdownOption[]> {
    return this.list(`${this.assetUrl}/asset-categories`, 'name', () => ({ parentId: categoryId }), { parentCategoryId: categoryId });
  }

  /** GET {asset service}/api/asset-category-types?categoryId= — the types of a category (or sub-category); parentId is its id. */
  getAssetTypes(categoryId?: string): Observable<DropdownOption[]> {
    return this.list(`${this.assetUrl}/asset-category-types`, 'typeName', (r) => ({ parentId: this.text(r['categoryId']) }), { categoryId });
  }

  /** GET /api/master-data/asset-models — parentId is the manufacturer id. */
  getAssetModels(manufacturerId?: string): Observable<DropdownOption[]> {
    return this.list('asset-models', 'modelName', (r) => ({ parentId: this.refId(r['manufacturer']) }), { manufacturerId });
  }

  /** GET /api/master-data/manufacturers */
  getManufacturers(): Observable<DropdownOption[]> {
    return this.list('manufacturers', 'manufacturerName');
  }

  /** GET /api/master-data/business-units */
  getBusinessUnits(): Observable<DropdownOption[]> {
    return this.list('business-units', 'businessUnitName');
  }

  /** GET /api/master-data/departments */
  getDepartments(): Observable<DropdownOption[]> {
    return this.list('departments', 'departmentName');
  }

  /** GET /api/master-data/employees — parentId is the department id. */
  getEmployees(departmentId?: string): Observable<DropdownOption[]> {
    return this.list('employees', 'employeeName', (r) => ({ parentId: this.refId(r['department']) }), { departmentId });
  }

  /** GET /api/master-data/teams — parentId is the department id. */
  getTeams(departmentId?: string): Observable<DropdownOption[]> {
    return this.list('teams', 'teamName', (r) => ({ parentId: this.refId(r['department']) }), { departmentId });
  }

  /** GET /api/master-data/suppliers */
  getSuppliers(): Observable<DropdownOption[]> {
    return this.list('suppliers', 'supplierName');
  }

  /** GET /api/master-data/service-providers */
  getServiceProviders(): Observable<DropdownOption[]> {
    return this.list('service-providers', 'serviceProviderName');
  }

  /** GET /api/master-data/uoms */
  getUoms(type?: string): Observable<DropdownOption[]> {
    return this.list('uoms', 'uomName', undefined, { type });
  }

  /** GET /api/master-data/shifts */
  getShifts(): Observable<DropdownOption[]> {
    return this.list('shifts', 'shiftName');
  }

  /** GET /api/master-data/standards */
  getStandards(): Observable<DropdownOption[]> {
    return this.list('standards', 'standardName');
  }

  // ===== Location hierarchy (Country -> Site -> Building -> Floor -> Zone -> Room) =====

  /** GET /api/master-data/sites — country is the site's country name (there is no countries endpoint). */
  getSites(): Observable<LocationOption[]> {
    return this.list<LocationOption>('sites', 'siteName', (r) => ({ country: this.text(r['country']) }));
  }

  /** GET /api/master-data/buildings — parentId is the site id. */
  getBuildings(siteId?: string): Observable<LocationOption[]> {
    return this.list<LocationOption>('buildings', 'name', (r) => ({ parentId: this.refId(r['site']) }), { siteId });
  }

  /** GET /api/master-data/floors — parentId is the building id. */
  getFloors(buildingId?: string): Observable<LocationOption[]> {
    return this.list<LocationOption>(
      'floors',
      'name',
      (r) => ({ parentId: this.text(r['buildingId']), siteId: this.refId(r['site']) }),
      { buildingId },
    );
  }

  /** GET /api/master-data/zones — parentId is the floor id; zones without a floor hang off their building or site. */
  getZones(floorId?: string): Observable<LocationOption[]> {
    return this.list<LocationOption>(
      'zones',
      'name',
      (r) => ({ parentId: this.text(r['floorId']), buildingId: this.text(r['buildingId']), siteId: this.refId(r['site']) }),
      { floorId },
    );
  }

  /** GET /api/master-data/locations (rooms) — linked to their zone / floor / building by name, not id. */
  getRooms(): Observable<LocationOption[]> {
    return this.list<LocationOption>('locations', 'locationName', (r) => ({
      zoneName: this.text(r['zone']),
      floorName: this.text(r['floor']),
      buildingName: this.text(r['building']),
    }));
  }

  /** Every location level at once, for the step 3 tree and dropdowns. */
  getLocationHierarchy(): Observable<LocationHierarchy> {
    return forkJoin({
      sites: this.getSites(),
      buildings: this.getBuildings(),
      floors: this.getFloors(),
      zones: this.getZones(),
      rooms: this.getRooms(),
    });
  }

  // ===== Helpers =====

  /**
   * Loads one master resource and maps it to dropdown options, skipping inactive and deleted records.
   * No clientId filter: records are currently saved with clientId null, and ?clientId=CLT-1001 returns none of them.
   */
  private list<T extends DropdownOption = DropdownOption>(
    resource: string,
    labelField: string,
    extra?: (record: MasterRecord) => Partial<T>,
    filters: Record<string, string | undefined> = {},
  ): Observable<T[]> {
    const params: Record<string, string> = {};
    for (const [key, value] of Object.entries(filters)) if (value) params[key] = value;
    // A full URL (Asset service lists) is used as is; anything else is a master data resource
    const url = /^https?:\/\//i.test(resource) ? resource : `${this.masterUrl}/${resource}`;
    return this.http.get<MasterRecord[] | { items?: MasterRecord[] }>(url, { params }).pipe(
      map((res) => (Array.isArray(res) ? res : res?.items ?? [])),
      map((records) =>
        records
          .filter((r) => r.id && r['isDeleted'] !== true && !/^(inactive|disabled|deleted)$/i.test(r.status ?? ''))
          .map((r) => {
            const label = String(r[labelField] ?? r['name'] ?? r.code ?? r.id);
            const option = { label, value: String(r.id), ...extra?.(r) } as T;
            if (r.code) option.code = r.code;
            if (r.color) option.color = r.color;
            return option;
          }),
      ),
      catchError(() => of([])),
    );
  }

  private text(value: unknown): string | undefined {
    return typeof value === 'string' && value ? value : undefined;
  }

  private refId(ref: unknown): string | undefined {
    return (ref as MasterReference | null | undefined)?.id ?? undefined;
  }
}
