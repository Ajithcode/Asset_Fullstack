import { Component, EventEmitter, HostListener, Input, OnChanges, Output, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ArrowRight, Boxes, Building2, ChevronDown, FileText, History, LucideAngularModule, MapPin, Network, Pencil, Users } from 'lucide-angular';
import { formatDateTime } from '../../location/location.format';
import { OrganizationService } from '../../organization/organization.service';
import { DepartmentService } from '../../department/department.service';
import { SiteService } from '../../site/site.service';
import { MASTER_ICONS } from '../../../shared/master-icons';
import { BusinessUnitRecord } from '../business-unit.model';
import { BusinessUnitService } from '../business-unit.service';

type DetailTab = 'overview' | 'sites' | 'departments' | 'assets' | 'people' | 'costCenters' | 'documents' | 'activity';

interface HierarchyRow {
  key: string;
  label: string;
  level: number;
  current: boolean;
  kind: 'org' | 'unit' | 'site';
  code: string | null;
}

// Sites listed under the unit in the hierarchy card
const SITE_PREVIEW = 4;

function seed(text: string): number {
  return [...text].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 29);
}

/** Panel under the list showing the selected business unit. */
@Component({
  selector: 'app-bu-detail',
  imports: [DecimalPipe, LucideAngularModule],
  templateUrl: './bu-detail.html',
  styleUrls: ['../../../shared/styles/mdm-detail.css', '../../../shared/styles/mdm-detail-extras.css']
})
export class BuDetail implements OnChanges {
  @Input({ required: true }) unit!: BusinessUnitRecord;
  @Output() edit = new EventEmitter<BusinessUnitRecord>();
  @Output() toggleStatus = new EventEmitter<BusinessUnitRecord>();
  @Output() remove = new EventEmitter<BusinessUnitRecord>();
  @Output() select = new EventEmitter<string>();
  @Output() addSubUnit = new EventEmitter<BusinessUnitRecord>();

  readonly service = inject(BusinessUnitService);
  readonly sites = inject(SiteService);
  readonly departmentService = inject(DepartmentService);
  readonly organizations = inject(OrganizationService);
  readonly logoIcons = MASTER_ICONS;
  readonly icons = { Pencil, ChevronDown, Network, MapPin, Boxes, Users, FileText, History, ArrowRight, Building2 };
  readonly formatDateTime = formatDateTime;
  readonly tabs: { id: DetailTab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'sites', label: 'Sites' },
    { id: 'departments', label: 'Departments' },
    { id: 'assets', label: 'Assets' },
    { id: 'people', label: 'People' },
    { id: 'costCenters', label: 'Cost Centers' },
    { id: 'documents', label: 'Documents' },
    { id: 'activity', label: 'Activity Log' }
  ];

  tab: DetailTab = 'overview';
  moreOpen = false;
  hierarchy: HierarchyRow[] = [];

  ngOnChanges(): void {
    this.moreOpen = false;
    this.hierarchy = this.buildHierarchy();
  }

  get organizationName(): string {
    return this.organizations.find(this.unit.organizationCode)?.name ?? this.unit.organizationCode;
  }

  get parentUnit(): BusinessUnitRecord | undefined {
    return this.service.find(this.unit.parentCode);
  }

  get unitSites() {
    return this.sites.ofBusinessUnit(this.unit.name);
  }

  get assetTotal(): number {
    return this.unitSites.reduce((sum, s) => sum + s.assets, 0);
  }

  get peopleTotal(): number {
    return this.unitSites.reduce((sum, s) => sum + s.people, 0);
  }

  get unitDepartments() {
    return this.departmentService.ofBusinessUnit(this.unit.code);
  }

  get assetRows() {
    const s = seed(this.unit.code);
    const names: [string, string][] = [['CNC Machine', 'Machining'], ['Forklift', 'Material Handling'], ['Chiller', 'HVAC'], ['Laptop', 'IT'], ['UPS', 'Electrical'], ['Fire Pump', 'Safety']];
    return names.map(([name, category], i) => ({
      tag: `AST-${((s + i * 7919) % 90000) + 10000}`,
      name: `${name} ${i + 1}`,
      category,
      site: this.unitSites[i % Math.max(this.unitSites.length, 1)]?.name ?? '-'
    }));
  }

  get peopleRows() {
    const s = seed(this.unit.code);
    const people: [string, string][] = [[this.unit.head || 'Rex Kumar', 'Business Unit Head'], ['Omar Haddad', 'Operations Manager'], ['Priya Nair', 'Analyst'], ['Murugan S', 'Technician'], ['Fatima Ali', 'Engineer']];
    return people.map(([name, role], i) => ({ name, role, site: this.unitSites[(s + i) % Math.max(this.unitSites.length, 1)]?.name ?? '-' }));
  }

  get costCenterRows() {
    const main = this.unit.costCenter || `CC-${this.unit.code}-001`;
    const prefix = main.replace(/-\d+$/, '');
    return [
      { code: main, name: `${this.unit.name} - Main`, budget: 250000 + (seed(main) % 20) * 25000 },
      ...this.unitSites.slice(0, 3).map((site, i) => ({ code: `${prefix}-${String(i + 2).padStart(3, '0')}`, name: site.name, budget: 60000 + (seed(site.code) % 20) * 10000 }))
    ];
  }

  get documents() {
    return [
      { name: `${this.unit.name} - Charter.pdf`, kind: 'Policy', size: '820 KB', date: this.unit.createdOn.slice(0, 10) },
      { name: `${this.unit.name} - Budget ${new Date().getFullYear()}.xlsx`, kind: 'Budget', size: '1.4 MB', date: this.unit.updatedOn.slice(0, 10) },
      { name: `${this.unit.name} - Org Chart.pdf`, kind: 'Org Chart', size: '360 KB', date: this.unit.updatedOn.slice(0, 10) }
    ];
  }

  get activity() {
    const list = [
      { when: this.unit.updatedOn, who: 'Rex Kumar', text: 'Updated business unit details' },
      { when: this.unit.createdOn, who: 'Aisha Rahman', text: `Created business unit ${this.unit.code}` }
    ];
    if (this.unit.status === 'Inactive') list.unshift({ when: this.unit.updatedOn, who: 'Rex Kumar', text: 'Marked business unit as Inactive' });
    return list;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.moreOpen = false;
  }

  /** Organization chain > parent units > this unit > its sub-units and sites. */
  private buildHierarchy(): HierarchyRow[] {
    const u = this.unit;
    const rows: HierarchyRow[] = this.organizations
      .ancestry(u.organizationCode)
      .map((o, i) => ({ key: `org:${o.code}`, label: o.name, level: i + 1, current: false, kind: 'org' as const, code: null }));
    const chain: BusinessUnitRecord[] = [];
    for (let p = this.service.find(u.parentCode); p && chain.length < 10; p = this.service.find(p.parentCode)) chain.unshift(p);
    [...chain, u].forEach((b) =>
      rows.push({ key: `bu:${b.code}`, label: b.name, level: rows.length + 1, current: b.code === u.code, kind: 'unit', code: b.code })
    );
    const level = rows.length + 1;
    this.service.children(u.code).forEach((c) => rows.push({ key: `bu:${c.code}`, label: c.name, level, current: false, kind: 'unit', code: c.code }));
    this.unitSites.slice(0, SITE_PREVIEW).forEach((s) => rows.push({ key: `site:${s.id}`, label: s.name, level, current: false, kind: 'site', code: null }));
    return rows;
  }
}
