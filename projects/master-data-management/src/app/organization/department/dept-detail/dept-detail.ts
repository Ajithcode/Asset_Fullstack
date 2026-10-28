import { Component, EventEmitter, HostListener, Input, OnChanges, Output, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ArrowRight, Boxes, Building2, ChevronDown, FileText, History, LucideAngularModule, MapPin, Network, Pencil, User, Users, Wrench } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { MASTER_ICONS } from '../../../shared/master-icons';
import { EMPLOYEES } from '../../business-unit/business-unit.data';
import { BusinessUnitService } from '../../business-unit/business-unit.service';
import { formatDateTime } from '../../location/location.format';
import { SiteService } from '../../site/site.service';
import { DepartmentRecord } from '../department.model';
import { DepartmentService } from '../department.service';

type DetailTab = 'overview' | 'team' | 'assets' | 'sites' | 'costCenter' | 'documents' | 'activity';

interface HierarchyRow {
  key: string;
  label: string;
  level: number;
  current: boolean;
  code: string | null;
}

function seed(text: string): number {
  return [...text].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 31);
}

/** Panel under the list showing the selected department. */
@Component({
  selector: 'app-dept-detail',
  imports: [DecimalPipe, RouterLink, LucideAngularModule],
  templateUrl: './dept-detail.html',
  styleUrls: ['../../../shared/styles/mdm-detail.css', '../../../shared/styles/mdm-detail-extras.css', './dept-detail.css']
})
export class DeptDetail implements OnChanges {
  @Input({ required: true }) department!: DepartmentRecord;
  @Output() edit = new EventEmitter<DepartmentRecord>();
  @Output() toggleStatus = new EventEmitter<DepartmentRecord>();
  @Output() remove = new EventEmitter<DepartmentRecord>();
  @Output() select = new EventEmitter<string>();
  @Output() addSub = new EventEmitter<DepartmentRecord>();

  readonly service = inject(DepartmentService);
  readonly businessUnits = inject(BusinessUnitService);
  readonly siteService = inject(SiteService);
  readonly logoIcons = MASTER_ICONS;
  readonly icons = { Pencil, ChevronDown, Users, Boxes, Wrench, MapPin, FileText, History, Network, ArrowRight, Building2, User };
  readonly formatDateTime = formatDateTime;
  readonly businessUnitPath = `${MASTER_DATA_BASE_PATH}/organization/business-unit`;
  readonly tabs: { id: DetailTab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'team', label: 'Team' },
    { id: 'assets', label: 'Assets' },
    { id: 'sites', label: 'Sites' },
    { id: 'costCenter', label: 'Cost Center' },
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

  get unit() {
    return this.businessUnits.find(this.department.businessUnitCode);
  }

  get parent(): DepartmentRecord | undefined {
    return this.service.find(this.department.parentCode);
  }

  // Sites refer to departments by name
  get sites() {
    return this.siteService.all().filter((s) => s.department === this.department.name);
  }

  get subDepartments(): DepartmentRecord[] {
    return this.service.children(this.department.code);
  }

  // DEMO MODE: sample rows for data owned by other modules, derived from the department code
  get team() {
    const s = seed(this.department.code);
    const roles = ['Engineer', 'Senior Engineer', 'Technician', 'Analyst', 'Coordinator', 'Supervisor'];
    const head = this.department.head ? [{ name: this.department.head, role: 'Department Head' }] : [];
    const deputy = this.department.deputy ? [{ name: this.department.deputy, role: 'Deputy Head' }] : [];
    const others = Array.from({ length: 5 }, (_, i) => ({ name: EMPLOYEES[(s + i * 5) % EMPLOYEES.length], role: roles[(s + i) % roles.length] }));
    const seen = new Set<string>();
    return [...head, ...deputy, ...others].filter((p) => !seen.has(p.name) && seen.add(p.name));
  }

  get assetRows() {
    const s = seed(this.department.code);
    const names: [string, string][] = [['Workstation', 'IT'], ['CAD Software Licence', 'Software'], ['Test Rig', 'Equipment'], ['Laptop', 'IT'], ['Calibration Kit', 'Tools'], ['3D Printer', 'Equipment']];
    return names.map(([name, category], i) => ({
      tag: `AST-${((s + i * 7919) % 90000) + 10000}`,
      name: `${name} ${i + 1}`,
      category,
      holder: this.team[i % this.team.length]?.name ?? '-'
    }));
  }

  get documents() {
    const d = this.department;
    return [
      { name: `${d.name} - Roles & Responsibilities.pdf`, kind: 'Policy', size: '540 KB', date: d.createdOn.slice(0, 10) },
      { name: `${d.name} - Budget.xlsx`, kind: 'Budget', size: '980 KB', date: d.updatedOn.slice(0, 10) },
      { name: `${d.name} - SOPs.pdf`, kind: 'Procedure', size: '2.1 MB', date: d.updatedOn.slice(0, 10) }
    ];
  }

  get activity() {
    const d = this.department;
    const list = [
      { when: d.updatedOn, who: 'Rex Kumar', text: 'Updated department details' },
      { when: d.createdOn, who: 'Aisha Rahman', text: `Created department ${d.code}` }
    ];
    if (d.status === 'Inactive') list.unshift({ when: d.updatedOn, who: 'Rex Kumar', text: 'Marked department as Inactive' });
    return list;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.moreOpen = false;
  }

  /** Business unit > parent departments > this department > its sub-departments. */
  private buildHierarchy(): HierarchyRow[] {
    const d = this.department;
    const rows: HierarchyRow[] = [];
    const unit = this.unit;
    if (unit) rows.push({ key: `bu:${unit.code}`, label: `${unit.name} (${unit.code})`, level: 1, current: false, code: null });
    [...this.service.ancestry(d.parentCode), d].forEach((x) =>
      rows.push({ key: x.code, label: `${x.name} (${x.code})`, level: rows.length + 1, current: x.code === d.code, code: x.code })
    );
    const level = rows.length + 1;
    this.subDepartments.forEach((c) => rows.push({ key: c.code, label: `${c.name} (${c.code})`, level, current: false, code: c.code }));
    return rows;
  }
}
