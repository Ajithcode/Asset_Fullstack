import { Component, EventEmitter, HostListener, Input, OnChanges, Output, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Box, ChevronDown, Copy, LucideAngularModule, Pencil, Search, ShieldCheck, TriangleAlert, Wrench } from 'lucide-angular';
import { formatDateTime } from '../../../organization/location/location.format';
import { sampleTag } from '../../asset-model/asset-model.data';
import { AssetModelService } from '../../asset-model/asset-model.service';
import { LEVEL_ICONS } from '../../level-icons';
import { ConditionRecord } from '../condition.model';
import { ConditionService } from '../condition.service';
import { CondFormTab } from '../cond-form/cond-form';

type DetailTab = 'overview' | 'health' | 'usage' | 'assets' | 'documents' | 'activity' | 'settings';

function seed(text: string): number {
  return [...text].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 7);
}

/** Panel under the list showing the selected condition. */
@Component({
  selector: 'app-cond-detail',
  imports: [DecimalPipe, LucideAngularModule],
  templateUrl: './cond-detail.html',
  styleUrls: [
    '../../../shared/styles/mdm-detail.css',
    '../../../shared/styles/mdm-detail-extras.css',
    '../../asset-model/model-detail/model-detail.css',
    '../../asset-type/type-detail/type-detail.css',
    '../../level-detail.css'
  ]
})
export class CondDetail implements OnChanges {
  @Input({ required: true }) condition!: ConditionRecord;
  @Output() edit = new EventEmitter<{ condition: ConditionRecord; tab?: CondFormTab }>();
  @Output() duplicate = new EventEmitter<ConditionRecord>();
  @Output() makeDefault = new EventEmitter<ConditionRecord>();
  @Output() toggleStatus = new EventEmitter<ConditionRecord>();
  @Output() remove = new EventEmitter<ConditionRecord>();

  readonly service = inject(ConditionService);
  readonly models = inject(AssetModelService);
  readonly levelIcons = LEVEL_ICONS;
  readonly icons = { Pencil, Copy, Search, ChevronDown, Box, Wrench, TriangleAlert, ShieldCheck };
  readonly formatDateTime = formatDateTime;
  readonly characteristicFields: { key: keyof ConditionRecord['characteristics']; label: string }[] = [
    { key: 'physicalState', label: 'Physical State' },
    { key: 'functionality', label: 'Functionality' },
    { key: 'appearance', label: 'Appearance' },
    { key: 'maintenanceNeed', label: 'Maintenance Need' },
    { key: 'expectedRemainingLife', label: 'Expected Remaining Life' }
  ];
  readonly tabs: { id: DetailTab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'health', label: 'Health & Visual' },
    { id: 'usage', label: 'Usage & Rules' },
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

  names(codes: string[]): string {
    return codes.map((c) => this.service.nameOf(c)).join(', ') || 'None';
  }

  // DEMO MODE: sample assets in this condition, taken from asset models that have assets
  get sampleAssets() {
    const withAssets = this.models.all().filter((m) => m.assetCount > 0);
    const start = seed(this.condition.code) % Math.max(1, withAssets.length - 8);
    return withAssets.slice(start, start + Math.min(8, this.condition.usage.assets)).map((m, i) => ({
      tag: sampleTag(m.taggingFormat).replace(/\d+$/, (n) => String(Number(n) + i * 23).padStart(n.length, '0')),
      model: m.name,
      manufacturer: m.manufacturer,
      type: m.assetType
    }));
  }

  get documentRows() {
    const c = this.condition;
    const kinds = ['Condition Assessment Guide', 'Inspection Photo Examples'];
    return kinds.slice(0, c.documents).map((kind, i) => ({ name: `${c.name} - ${kind}.pdf`, kind, date: (i % 2 ? c.updatedOn : c.createdOn).slice(0, 10) }));
  }

  get activity() {
    const c = this.condition;
    const list = [
      { when: c.updatedOn, who: 'Rex Kumar', text: 'Updated transition rules' },
      { when: c.createdOn, who: c.createdBy || 'Rex Kumar', text: `Created condition ${c.code}` }
    ];
    if (c.status === 'Inactive') list.unshift({ when: c.updatedOn, who: 'Rex Kumar', text: 'Marked condition as Inactive' });
    return list;
  }

  editAt(tab: CondFormTab): void {
    this.edit.emit({ condition: this.condition, tab });
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.moreOpen = false;
  }
}
