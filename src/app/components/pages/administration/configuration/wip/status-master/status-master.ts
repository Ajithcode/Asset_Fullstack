import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ImportColumn, ImportFileModal } from 'shared-ui';
import { RowActions } from 'shared-ui';
import { StatusMaster } from './status-master.model';
import { StatusMasterService } from './status-master.service';

interface StatusMasterRow extends StatusMaster {
  selected?: boolean;
}

interface StatusMasterColumn {
  key: string;
  label: string;
  visible: boolean;
}

@Component({
  standalone: true,
  selector: 'app-wip-status-master',
  imports: [CommonModule, FormsModule, ImportFileModal, RowActions],
  templateUrl: './status-master.html',
  styleUrls: ['./status-master.css']
})
export class WipStatusMaster {
  searchTerm = '';

  columns: StatusMasterColumn[] = [
    { key: 'statusId', label: 'Status ID', visible: true },
    { key: 'assetId', label: 'Asset ID', visible: true },
    { key: 'assetName', label: 'Asset Name', visible: true },
    { key: 'statusName', label: 'Status Name', visible: true },
    { key: 'statusCode', label: 'Status Code', visible: true },
    { key: 'sequenceOrder', label: 'Sequence Order', visible: true },
    { key: 'isClosedStatus', label: 'Is Closed Status', visible: true },
    { key: 'colorCode', label: 'Color Code', visible: true },
    { key: 'allowedTransitions', label: 'Allowed Transitions', visible: true },
    { key: 'requiresApproval', label: 'Requires Approval', visible: true },
    { key: 'isDefault', label: 'Is Default', visible: true }
  ];

  showColumnPicker = false;

  readonly importColumns: ImportColumn[] = this.columns.map(({ key, label }) => ({ key, label }));
  showImportModal = false;

  records: StatusMasterRow[] = [];
  filteredRecords: StatusMasterRow[] = [];

  showFormModal = false;
  isEditMode = false;
  private editingRecord: StatusMasterRow | null = null;

  form: StatusMaster = this.emptyForm();

  constructor(private service: StatusMasterService) {
    this.refresh();
  }

  get statusNameSuggestions() {
    return this.service.statusNameSuggestions;
  }

  private emptyForm(): StatusMaster {
    return {
      statusId: '',
      assetId: '',
      assetName: '',
      statusName: '',
      statusCode: '',
      sequenceOrder: null,
      isClosedStatus: false,
      colorCode: '#4f2bd8',
      allowedTransitions: [],
      requiresApproval: false,
      isDefault: false
    };
  }

  private refresh(): void {
    this.records = this.service.getStatuses();
    this.onSearch();
  }

  isColumnVisible(key: string): boolean {
    return this.columns.find((c) => c.key === key)?.visible ?? true;
  }

  toggleColumnPicker(): void {
    this.showColumnPicker = !this.showColumnPicker;
  }

  closeColumnPicker(): void {
    this.showColumnPicker = false;
  }

  toggleColumn(col: StatusMasterColumn): void {
    col.visible = !col.visible;
  }

  get selectedRecords(): StatusMasterRow[] {
    return this.filteredRecords.filter((r) => r.selected);
  }

  get allSelected(): boolean {
    return this.filteredRecords.length > 0 && this.filteredRecords.every((r) => r.selected);
  }

  toggleSelectAll(): void {
    const next = !this.allSelected;
    this.filteredRecords.forEach((r) => (r.selected = next));
  }

  toggleSelectRecord(record: StatusMasterRow): void {
    record.selected = !record.selected;
  }

  onSearch(): void {
    this.filteredRecords = this.service.search(this.searchTerm) as StatusMasterRow[];
  }

  onRefresh(): void {
    this.searchTerm = '';
    this.refresh();
  }

  onCreate(): void {
    this.isEditMode = false;
    this.editingRecord = null;
    this.form = this.emptyForm();
    this.showFormModal = true;
  }

  onEdit(): void {
    if (this.selectedRecords.length !== 1) return;
    this.editRow(this.selectedRecords[0]);
  }

  editRow(record: StatusMasterRow): void {
    this.isEditMode = true;
    this.editingRecord = record;
    const { selected, ...rest } = record;
    this.form = { ...rest, allowedTransitions: [...rest.allowedTransitions] };
    this.showFormModal = true;
  }

  closeFormModal(): void {
    this.showFormModal = false;
    this.editingRecord = null;
  }

  submitForm(): void {
    if (this.isEditMode && this.editingRecord) {
      this.service.updateRecord(this.editingRecord.statusId, { ...this.form });
    } else {
      this.service.addRecord({ ...this.form });
    }
    this.refresh();
    this.closeFormModal();
  }

  onDelete(): void {
    if (this.selectedRecords.length === 0) return;
    this.service.deleteRecords(this.selectedRecords.map((r) => r.statusId));
    this.refresh();
  }

  deleteRow(record: StatusMasterRow): void {
    this.service.deleteRecords([record.statusId]);
    this.refresh();
  }

  onUpload(): void {
    this.showImportModal = true;
  }

  onImportRows(rows: Record<string, string>[]): void {
    const truthy = (value: string | undefined) =>
      ['true', 'yes', '1'].includes((value ?? '').trim().toLowerCase());

    rows.forEach((row) => {
      this.service.addRecord({
        statusId: row['statusId'] ?? '',
        assetId: row['assetId'] ?? '',
        assetName: row['assetName'] ?? '',
        statusName: row['statusName'] ?? '',
        statusCode: row['statusCode'] ?? '',
        sequenceOrder: row['sequenceOrder'] ? Number(row['sequenceOrder']) : null,
        isClosedStatus: truthy(row['isClosedStatus']),
        colorCode: row['colorCode'] ?? '',
        allowedTransitions: row['allowedTransitions']
          ? row['allowedTransitions'].split(',').map((v) => v.trim()).filter(Boolean)
          : [],
        requiresApproval: truthy(row['requiresApproval']),
        isDefault: truthy(row['isDefault'])
      });
    });
    this.refresh();
    this.showImportModal = false;
  }

  onDownload(): void {
  }

  statusName(statusId: string): string {
    return this.records.find((s) => s.statusId === statusId)?.statusName || statusId;
  }

  transitionNames(statusIds: string[]): string {
    return (statusIds ?? []).map((id) => this.statusName(id)).join(', ');
  }

  otherStatuses(): StatusMaster[] {
    const currentId = this.editingRecord?.statusId;
    return this.records.filter((s) => s.statusId !== currentId);
  }

  isTransitionSelected(targetId: string): boolean {
    return this.form.allowedTransitions.includes(targetId);
  }

  toggleTransition(targetId: string): void {
    this.form.allowedTransitions = this.form.allowedTransitions.includes(targetId)
      ? this.form.allowedTransitions.filter((id) => id !== targetId)
      : [...this.form.allowedTransitions, targetId];
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.closeColumnPicker();
  }
}
