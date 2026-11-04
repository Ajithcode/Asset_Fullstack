import { Component, HostListener, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Archive,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleCheck,
  Clock,
  CloudUpload,
  Ellipsis,
  Eye,
  FileText,
  Layers,
  LucideAngularModule,
  Maximize2,
  RefreshCw,
  Building2
} from 'lucide-angular';
import { MapCanvas } from '../map-canvas/map-canvas';
import { MapVersion, MapStatus, STATUS_TONE, fmtDate, fmtTime } from '../maps.data';
import { MapsStore } from '../maps.store';
import { SiteTree } from '../site-tree/site-tree';
import { UploadDialog } from '../upload-dialog/upload-dialog';

/** Versions & Publishing: version history of the current plan, publish / archive, and who can see it. */
@Component({
  standalone: true,
  selector: 'app-maps-versions',
  imports: [FormsModule, LucideAngularModule, SiteTree, MapCanvas, UploadDialog],
  templateUrl: './versions.html',
  styleUrls: ['../../shared/admin-workspace.css', '../maps.css']
})
export class VersionsTab {
  readonly store = inject(MapsStore);
  readonly icons = {
    Archive, Building2, ChevronDown, ChevronLeft, ChevronRight, CircleCheck, Clock, CloudUpload, Ellipsis, Eye, FileText, Layers, Maximize2, RefreshCw
  };
  readonly pageSizes = [5, 10, 20];

  rightTab: 'details' | 'access' = 'details';
  selectedVersion = this.store.currentVersion?.version ?? this.store.versions[0]?.version;
  checked = new Set<string>();
  menuFor: string | null = null;
  headMenuOpen = false;
  uploadOpen = false;
  previewOpen = false;
  page = 0;
  pageSize = 10;

  access = { audience: 'All Users', roles: ['Administrator', 'Facility Manager'], download: true, watermark: false, approval: true };
  readonly roles = ['Administrator', 'Facility Manager', 'Technician', 'Security', 'Auditor', 'Viewer'];

  date = fmtDate;
  time = fmtTime;

  tone(s: MapStatus) {
    return STATUS_TONE[s];
  }

  get selected(): MapVersion | undefined {
    return this.store.versions.find((v) => v.version === this.selectedVersion);
  }

  get paged(): MapVersion[] {
    return this.store.versions.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.store.versions.length / this.pageSize));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i);
  }

  toggle(v: string): void {
    if (this.checked.has(v)) this.checked.delete(v);
    else this.checked.add(v);
  }

  toggleMenu(event: MouseEvent, v: string): void {
    event.stopPropagation();
    this.menuFor = this.menuFor === v ? null : v;
  }

  toggleHeadMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.headMenuOpen = !this.headMenuOpen;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuFor = null;
    this.headMenuOpen = false;
  }

  publish(v: MapVersion): void {
    if (this.access.approval && v.status === 'Draft' && !confirm(`Publishing needs approval. Publish ${v.version} now as an administrator?`)) return;
    this.store.publish(v.version);
    this.selectedVersion = v.version;
    this.store.notify(`${v.version} published - previous version archived`);
  }

  setDraft(v: MapVersion): void {
    if (v.current) {
      this.store.notify('The current version cannot go back to draft - publish another version first');
      return;
    }
    this.store.setVersionStatus(v.version, 'Draft');
    this.store.notify(`${v.version} set as draft`);
  }

  archive(v: MapVersion): void {
    if (v.current) {
      this.store.notify('The current version cannot be archived - publish another version first');
      return;
    }
    this.store.setVersionStatus(v.version, 'Archived');
    this.store.notify(`${v.version} archived`);
  }

  archiveChecked(): void {
    const targets = this.store.versions.filter((v) => this.checked.has(v.version) && !v.current);
    targets.forEach((v) => this.store.setVersionStatus(v.version, 'Archived'));
    this.checked.clear();
    this.store.notify(`${targets.length} version${targets.length === 1 ? '' : 's'} archived`);
  }

  download(v: MapVersion): void {
    this.store.notify(`Downloading ${v.fileName}`);
  }

  uploaded(version: string): void {
    this.uploadOpen = false;
    this.selectedVersion = version;
    this.store.notify(`${version} uploaded as a draft`);
  }

  toggleRole(r: string): void {
    this.access.roles = this.access.roles.includes(r) ? this.access.roles.filter((x) => x !== r) : [...this.access.roles, r];
  }

  saveAccess(): void {
    this.store.notify('Publishing & access settings saved');
  }
}
