import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, LucideAngularModule, Plus, RefreshCw, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { ConfigStatus, SyncStatusRecord } from '../system-config.model';
import { SyncStatusService, SystemSettingsService } from '../system-config.service';
import { SyncStatusForm } from './sync-status-form/sync-status-form';

type SortKey = 'name' | 'code';
interface Filters {
  search: string;
  status: '' | ConfigStatus;
}

/** Master Data Management > System Configuration > API Sync Status */
@Component({
  selector: 'app-mdm-system-configuration-api-sync-status',
  imports: [FormsModule, RouterLink, LucideAngularModule, SyncStatusForm],
  templateUrl: './api-sync-status.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../system-config.css']
})
export class ApiSyncStatusPage extends ListPage<SyncStatusRecord, Filters, SortKey> {
  readonly service = inject(SyncStatusService);
  private readonly settings = inject(SystemSettingsService);
  readonly basePath = MASTER_DATA_BASE_PATH;

  readonly icons = { RefreshCw, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', status: '' });
  }

  keyOf(r: SyncStatusRecord): string {
    return r.code;
  }

  nameOf(r: SyncStatusRecord): string {
    return r.name;
  }

  protected all(): SyncStatusRecord[] {
    return this.service.all();
  }

  protected matches(r: SyncStatusRecord, f: Filters): boolean {
    const term = f.search.trim().toLowerCase();
    return (
      (!f.status || r.status === f.status) &&
      (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected sortValue(r: SyncStatusRecord, key: SortKey): string | number {
    return r[key];
  }

  protected removeRecords(codes: string[]): void {
    this.service.remove(codes);
  }

  // The status used for failed syncs in Integration settings can't be deleted
  protected override deleteBlocker(r: SyncStatusRecord): string | null {
    return this.settings.usesSyncStatus(r.code) ? `${r.name} is the failed-sync status in System Settings › Integration. Pick another one there first.` : null;
  }

  duplicate(r: SyncStatusRecord): void {
    this.openAdd({ ...structuredClone(r), code: '', name: `${r.name} (Copy)`, status: 'Active' });
  }

  onSaved(event: { record: SyncStatusRecord; originalCode: string | null }): void {
    this.service.save(event.record, event.originalCode);
    this.carrySelection(event.originalCode, event.record.code);
    this.notify(`${event.record.name} ${event.originalCode ? 'updated' : 'added'}.`);
    this.closeForm();
  }

  toggleStatus(r: SyncStatusRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.service.save({ ...r, status }, r.code);
    this.notify(`${r.name} marked ${status}.`);
  }
}
