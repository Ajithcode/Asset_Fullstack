import { Component, EventEmitter, HostListener, Input, OnChanges, Output, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Boxes, ChevronDown, Copy, FileText, LucideAngularModule, Pencil, Search, Shapes, Wrench } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { formatDateTime } from '../../../organization/location/location.format';
import { sampleTag } from '../../asset-model/asset-model.data';
import { MODEL_ICONS } from '../../asset-model/asset-model.icons';
import { AssetModelService } from '../../asset-model/asset-model.service';
import { AssetTypeRecord } from '../asset-type.model';
import { TypeFormTab } from '../type-form/type-form';

type DetailTab = 'overview' | 'subcategories' | 'assets' | 'fields' | 'defaults' | 'documents' | 'activity' | 'lifecycle' | 'financial' | 'settings';

/** Panel under the list showing the selected asset type. */
@Component({
  selector: 'app-type-detail',
  imports: [DecimalPipe, RouterLink, LucideAngularModule],
  templateUrl: './type-detail.html',
  styleUrls: [
    '../../../shared/styles/mdm-detail.css',
    '../../../shared/styles/mdm-detail-extras.css',
    '../../asset-model/model-detail/model-detail.css',
    './type-detail.css'
  ]
})
export class TypeDetail implements OnChanges {
  @Input({ required: true }) type!: AssetTypeRecord;
  @Output() edit = new EventEmitter<{ type: AssetTypeRecord; tab?: TypeFormTab }>();
  @Output() duplicate = new EventEmitter<AssetTypeRecord>();
  @Output() toggleStatus = new EventEmitter<AssetTypeRecord>();
  @Output() remove = new EventEmitter<AssetTypeRecord>();

  readonly models = inject(AssetModelService);
  readonly typeIcons = MODEL_ICONS;
  readonly icons = { Pencil, Copy, Search, ChevronDown, Shapes, Boxes, Wrench, FileText };
  readonly formatDateTime = formatDateTime;
  readonly modelPath = `${MASTER_DATA_BASE_PATH}/asset/asset-model`;
  readonly tabs: { id: DetailTab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'subcategories', label: 'Subcategories' },
    { id: 'assets', label: 'Assets' },
    { id: 'fields', label: 'Custom Fields' },
    { id: 'defaults', label: 'Default Values' },
    { id: 'documents', label: 'Documents' },
    { id: 'activity', label: 'Activity Log' },
    { id: 'lifecycle', label: 'Lifecycle & Maintenance' },
    { id: 'financial', label: 'Financial' },
    { id: 'settings', label: 'Settings' }
  ];

  tab: DetailTab = 'overview';
  moreOpen = false;

  ngOnChanges(): void {
    this.moreOpen = false;
  }

  get icon() {
    return this.typeIcons[this.type.icon];
  }

  get sample(): string {
    return sampleTag(this.type.taggingFormat);
  }

  get currencyCode(): string {
    return this.type.currency.slice(0, 3);
  }

  // Asset models refer to their type by name
  get typeModels() {
    return this.models.ofType(this.type.name);
  }

  get assetCount(): number {
    return this.typeModels.reduce((sum, m) => sum + m.assetCount, 0);
  }

  /** How many of this type's models are filed under each subcategory. */
  get subcategoryRows() {
    const models = this.typeModels;
    return this.type.subcategories.map((name) => ({
      name,
      models: models.filter((m) => m.subcategories.some((s) => s.toLowerCase() === name.toLowerCase())).length
    }));
  }

  // DEMO MODE: sample rows for data owned by the Documents module
  get documentRows() {
    const t = this.type;
    const kinds: [string, string, string][] = [
      ['Specification Sheet', 'pdf', '1.2 MB'],
      ['Maintenance Checklist', 'pdf', '420 KB'],
      ['Warranty Terms', 'pdf', '310 KB'],
      ['Depreciation Policy', 'docx', '96 KB'],
      ['Safety Instructions', 'pdf', '780 KB'],
      ['Approved Vendors', 'xlsx', '64 KB'],
      ['Disposal Procedure', 'pdf', '210 KB'],
      ['Tagging Guide', 'pdf', '150 KB'],
      ['Inspection Template', 'xlsx', '58 KB']
    ];
    return kinds.slice(0, t.documents).map(([name, ext, size], i) => ({
      name: `${t.name} - ${name}.${ext}`,
      kind: name,
      size,
      date: (i % 2 ? t.updatedOn : t.createdOn).slice(0, 10)
    }));
  }

  get activity() {
    const t = this.type;
    const list = [
      { when: t.updatedOn, who: 'Rex Kumar', text: 'Updated asset type defaults' },
      { when: t.createdOn, who: t.createdBy || 'Rex Kumar', text: `Created asset type ${t.code}` }
    ];
    if (t.status === 'Inactive') list.unshift({ when: t.updatedOn, who: 'Rex Kumar', text: 'Marked asset type as Inactive' });
    return list;
  }

  editAt(tab: TypeFormTab): void {
    this.edit.emit({ type: this.type, tab });
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.moreOpen = false;
  }
}
