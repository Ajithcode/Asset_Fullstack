import { Component, EventEmitter, HostListener, Input, OnChanges, Output, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Building2, ChevronDown, Expand, LucideAngularModule, MapPin, Pencil, Plus } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { formatDateTime } from '../../location/location.format';
import { COUNTRIES } from '../../organization/organization.data';
import { OrganizationService } from '../../organization/organization.service';
import { SiteRecord } from '../site.model';
import { SiteService } from '../site.service';
import { SiteMap } from '../site-map/site-map';
import { SiteFormTab } from '../site-form/site-form';

type DetailTab = 'overview' | 'buildings' | 'assets' | 'departments' | 'contacts' | 'documents' | 'map' | 'operations' | 'activity';

interface HierarchyRow {
  key: string;
  label: string;
  level: number;
  current: boolean;
  siteId: string | null;
}

const MAX_IMAGE_BYTES = 2 * 1024 * 1024;
// Sites listed under the department in the hierarchy card
const SIBLING_PREVIEW = 4;

function seed(text: string): number {
  return [...text].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 23);
}

/** Panel under the list showing the selected site. */
@Component({
  selector: 'app-site-detail',
  imports: [DecimalPipe, RouterLink, LucideAngularModule, SiteMap],
  templateUrl: './site-detail.html',
  styleUrls: ['../../../shared/styles/mdm-detail.css', './site-detail.css']
})
export class SiteDetail implements OnChanges {
  @Input({ required: true }) site!: SiteRecord;
  @Output() edit = new EventEmitter<{ site: SiteRecord; tab: SiteFormTab }>();
  @Output() toggleStatus = new EventEmitter<SiteRecord>();
  @Output() remove = new EventEmitter<SiteRecord>();
  @Output() select = new EventEmitter<string>();
  @Output() addImage = new EventEmitter<{ site: SiteRecord; image: string }>();
  @Output() imageError = new EventEmitter<string>();

  readonly service = inject(SiteService);
  readonly organizations = inject(OrganizationService);
  readonly icons = { Pencil, MapPin, ChevronDown, Building2, Plus, Expand };
  readonly formatDateTime = formatDateTime;
  readonly bfzPath = `${MASTER_DATA_BASE_PATH}/organization/building-floor-zone`;
  readonly tabs: { id: DetailTab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'buildings', label: 'Buildings & Zones' },
    { id: 'assets', label: 'Assets' },
    { id: 'departments', label: 'Departments' },
    { id: 'contacts', label: 'Contacts' },
    { id: 'documents', label: 'Documents' },
    { id: 'map', label: 'Maps & Location' },
    { id: 'operations', label: 'Operational Details' },
    { id: 'activity', label: 'Activity Log' }
  ];

  tab: DetailTab = 'overview';
  moreOpen = false;
  hierarchy: HierarchyRow[] = [];

  ngOnChanges(): void {
    this.moreOpen = false;
    this.hierarchy = this.buildHierarchy();
  }

  get flag(): string {
    return COUNTRIES.find((c) => c.name === this.site.address.country)?.flag ?? '';
  }

  get parent(): SiteRecord | undefined {
    return this.service.find(this.site.parentId);
  }

  get images(): string[] {
    return [...(this.site.image ? [this.site.image] : []), ...this.site.images];
  }

  get buildings() {
    return this.service.buildingsOf(this.site.id);
  }

  dial(country: string): string {
    return COUNTRIES.find((c) => c.name === country)?.dial ?? '';
  }

  // DEMO MODE: sample rows for tabs owned by other modules, derived from the site code
  get sampleAssets() {
    const s = seed(this.site.code);
    const names: [string, string][] = [['Diesel Generator', 'Power'], ['Chiller', 'HVAC'], ['Forklift', 'Material Handling'], ['CCTV Camera', 'Security'], ['Fire Pump', 'Safety'], ['UPS', 'Electrical']];
    return names.map(([name, category], i) => ({ tag: `AST-${((s + i * 7919) % 90000) + 10000}`, name: `${name} ${i + 1}`, category, status: ['In Use', 'In Use', 'Idle', 'Under Maintenance'][(s + i) % 4] }));
  }

  get sampleDepartments() {
    const s = seed(this.site.code);
    const all = [this.site.department, 'Maintenance', 'Quality', 'HSE', 'IT'].filter((d, i, a) => d && a.indexOf(d) === i);
    return all.slice(0, 2 + (s % 3)).map((name, i) => ({ name, head: ['Rex Kumar', 'Aisha Rahman', 'Omar Haddad', 'Priya Nair'][(s + i) % 4], people: 6 + ((s + i * 17) % 80) }));
  }

  get sampleDocuments() {
    return [
      { name: `${this.site.name} - Site Plan.pdf`, kind: 'Drawing', size: '4.8 MB', date: this.site.updatedOn.slice(0, 10) },
      { name: `${this.site.name} - Trade License.pdf`, kind: 'License', size: '310 KB', date: this.site.createdOn.slice(0, 10) },
      { name: `${this.site.name} - Fire NOC.pdf`, kind: 'Certificate', size: '540 KB', date: this.site.updatedOn.slice(0, 10) }
    ];
  }

  get activity() {
    const list = [
      { when: this.site.updatedOn, who: 'Rex Kumar', text: 'Updated site details' },
      { when: this.site.createdOn, who: 'Aisha Rahman', text: `Created site ${this.site.code}` }
    ];
    if (this.site.status === 'Inactive') list.unshift({ when: this.site.updatedOn, who: 'Rex Kumar', text: 'Marked site as Inactive' });
    return list;
  }

  viewOnMap(): void {
    this.tab = 'map';
  }

  onImagePicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!['image/png', 'image/jpeg'].includes(file.type) || file.size > MAX_IMAGE_BYTES) {
      this.imageError.emit('Use a PNG or JPG image of 2 MB or less.');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => this.addImage.emit({ site: this.site, image: String(reader.result) });
    reader.readAsDataURL(file);
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.moreOpen = false;
  }

  /** Top organization > business unit > department > sites in that department (this one first). */
  private buildHierarchy(): HierarchyRow[] {
    const s = this.site;
    const root = this.organizations.ancestry(s.organizationCode)[0];
    const rows: HierarchyRow[] = [
      { key: 'org', label: root?.name ?? this.service.organizationName(s.organizationCode), level: 1, current: false, siteId: null }
    ];
    if (s.businessUnit) rows.push({ key: 'bu', label: s.businessUnit, level: 2, current: false, siteId: null });
    if (s.department) rows.push({ key: 'dept', label: s.department, level: rows.length + 1, current: false, siteId: null });
    const level = rows.length + 1;
    const siblings = this.service
      .all()
      .filter((r) => r.id !== s.id && r.businessUnit === s.businessUnit && r.department === s.department && !r.parentId)
      .slice(0, SIBLING_PREVIEW - 1);
    [s, ...siblings].forEach((r) => {
      rows.push({ key: r.id, label: r.name, level, current: r.id === s.id, siteId: r.id });
      if (r.id === s.id) {
        this.service.childSites(s.id).forEach((c) => rows.push({ key: c.id, label: c.name, level: level + 1, current: false, siteId: c.id }));
      }
    });
    return rows;
  }
}
