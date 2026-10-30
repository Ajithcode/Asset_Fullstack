import { Component, EventEmitter, HostListener, Input, OnChanges, Output, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Box, ChevronDown, Copy, LucideAngularModule, Monitor, Pencil, Search, Trash2, Wrench } from 'lucide-angular';
import { formatDateTime } from '../../../organization/location/location.format';
import { sampleTag } from '../../asset-model/asset-model.data';
import { AssetModelService } from '../../asset-model/asset-model.service';
import { LEVEL_ICONS } from '../../level-icons';
import { SCALE } from '../criticality.data';
import { CriticalityRecord, Scale } from '../criticality.model';
import { CritFormTab } from '../crit-form/crit-form';

type DetailTab = 'overview' | 'risk' | 'maintenance' | 'assets' | 'documents' | 'activity' | 'settings';

// Risk matrix shading from the lowest (likelihood + impact = 0) to the highest score (8)
const MATRIX_SHADES = ['#c9f0d8', '#d9f2cf', '#f3f4c8', '#fdf0c2', '#fde4b0', '#fdd7b4', '#fcc9c4', '#f9b4b4', '#f59e9e'];

function seed(text: string): number {
  return [...text].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 7);
}

/** Panel under the list showing the selected criticality level. */
@Component({
  selector: 'app-crit-detail',
  imports: [NgTemplateOutlet, LucideAngularModule],
  templateUrl: './crit-detail.html',
  styleUrls: [
    '../../../shared/styles/mdm-detail.css',
    '../../../shared/styles/mdm-detail-extras.css',
    '../../asset-model/model-detail/model-detail.css',
    '../../asset-type/type-detail/type-detail.css',
    '../../level-detail.css'
  ]
})
export class CritDetail implements OnChanges {
  @Input({ required: true }) level!: CriticalityRecord;
  @Output() edit = new EventEmitter<{ level: CriticalityRecord; tab?: CritFormTab }>();
  @Output() duplicate = new EventEmitter<CriticalityRecord>();
  @Output() makeDefault = new EventEmitter<CriticalityRecord>();
  @Output() toggleStatus = new EventEmitter<CriticalityRecord>();
  @Output() remove = new EventEmitter<CriticalityRecord>();

  readonly models = inject(AssetModelService);
  readonly levelIcons = LEVEL_ICONS;
  readonly icons = { Pencil, Copy, Search, ChevronDown, Box, Monitor, Wrench, Trash2 };
  readonly formatDateTime = formatDateTime;
  // Likelihood runs top (Very High) to bottom; impact runs left (Very Low) to right
  readonly likelihoodRows = [...SCALE].reverse();
  readonly impactCols = SCALE;
  readonly impactFields: { key: keyof CriticalityRecord['businessImpact']; label: string }[] = [
    { key: 'safety', label: 'Safety Impact' },
    { key: 'operational', label: 'Operational Impact' },
    { key: 'financial', label: 'Financial Impact' },
    { key: 'regulatory', label: 'Regulatory Impact' },
    { key: 'reputation', label: 'Reputation Impact' },
    { key: 'environmental', label: 'Environmental Impact' }
  ];
  readonly tabs: { id: DetailTab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'risk', label: 'Risk & Impact' },
    { id: 'maintenance', label: 'Maintenance & Lifecycle' },
    { id: 'assets', label: 'Assets' },
    { id: 'documents', label: 'Documents' },
    { id: 'activity', label: 'Activity Log' },
    { id: 'settings', label: 'Settings' }
  ];

  tab: DetailTab = 'overview';
  moreOpen = false;

  ngOnChanges(): void {
    this.moreOpen = false;
  }

  get activeAssets(): number {
    const a = this.level.assets;
    return a.total - a.underMaintenance - a.retired;
  }

  shade(likelihood: Scale, impact: Scale): string {
    return MATRIX_SHADES[SCALE.indexOf(likelihood) + SCALE.indexOf(impact)];
  }

  isMarked(likelihood: Scale, impact: Scale): boolean {
    return this.level.likelihood === likelihood && this.level.impact === impact;
  }

  // DEMO MODE: sample assets for this level, taken from asset models that have assets
  get sampleAssets() {
    const withAssets = this.models.all().filter((m) => m.assetCount > 0 && m.status === 'Active');
    const start = seed(this.level.code) % Math.max(1, withAssets.length - 8);
    const count = Math.min(8, this.level.assets.total);
    return withAssets.slice(start, start + count).map((m, i) => ({
      tag: sampleTag(m.taggingFormat).replace(/\d+$/, (n) => String(Number(n) + i * 17).padStart(n.length, '0')),
      model: m.name,
      manufacturer: m.manufacturer,
      type: m.assetType,
      state: i < this.level.assets.underMaintenance ? 'Under Maintenance' : 'Active'
    }));
  }

  get documentRows() {
    const l = this.level;
    const kinds = ['Criticality Assessment Method', 'Risk Register Extract', 'Maintenance Strategy Guideline', 'Approval Record'];
    return kinds.slice(0, l.documents).map((kind, i) => ({ name: `${l.name} - ${kind}.pdf`, kind, date: (i % 2 ? l.updatedOn : l.createdOn).slice(0, 10) }));
  }

  get activity() {
    const l = this.level;
    const list = [
      { when: l.updatedOn, who: 'Rex Kumar', text: 'Updated risk and maintenance settings' },
      { when: l.createdOn, who: l.createdBy || 'Rex Kumar', text: `Created criticality level ${l.code}` }
    ];
    if (l.isDefault) list.unshift({ when: l.updatedOn, who: 'Rex Kumar', text: 'Set as the default criticality' });
    return list;
  }

  editAt(tab: CritFormTab): void {
    this.edit.emit({ level: this.level, tab });
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.moreOpen = false;
  }
}
