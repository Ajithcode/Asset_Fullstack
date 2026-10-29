import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, ClipboardCheck, Ellipsis, LucideAngularModule, Plus } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { RecordStatus, StockStatusRecord } from '../inventory.model';
import { ItemService, StockStatusService } from '../inventory.service';
import { StockStatusForm } from './stock-status-form/stock-status-form';

type SortKey = 'name' | 'code' | 'type';
interface Filters {
  status: '' | RecordStatus;
}

/** Master Data Management > Inventory > Stock Status */
@Component({
  selector: 'app-mdm-inventory-stock-status',
  imports: [FormsModule, RouterLink, LucideAngularModule, StockStatusForm],
  templateUrl: './stock-status.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../inventory.css']
})
export class StockStatusPage extends ListPage<StockStatusRecord, Filters, SortKey> {
  readonly service = inject(StockStatusService);
  private readonly items = inject(ItemService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly icons = { ClipboardCheck, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ status: '' });
  }

  keyOf(r: StockStatusRecord): string {
    return r.code;
  }

  nameOf(r: StockStatusRecord): string {
    return r.name;
  }

  protected all(): StockStatusRecord[] {
    return this.service.all();
  }

  protected matches(r: StockStatusRecord, f: Filters): boolean {
    return !f.status || r.status === f.status;
  }

  protected sortValue(r: StockStatusRecord, key: SortKey): string {
    return r[key];
  }

  protected removeRecords(codes: string[]): void {
    this.service.remove(codes);
  }

  // The default status and the four statuses items are graded with stay
  protected override deleteBlocker(r: StockStatusRecord): string | null {
    if (r.isDefault) return `${r.name} is the system default. Make another status the default first.`;
    const inUse = this.items.all().filter((i) => this.items.stockState(i) === r.name).length;
    return inUse ? `${inUse} item${inUse === 1 ? ' is' : 's are'} ${r.name}. Mark it Inactive instead.` : null;
  }

  makeDefault(r: StockStatusRecord): void {
    this.menuFor = null;
    if (r.status !== 'Active') {
      this.notify(`Activate ${r.name} before making it the default.`);
      return;
    }
    this.service.save({ ...r, isDefault: true }, r.code);
    this.notify(`${r.name} is now the system default.`);
  }

  onSaved(event: { record: StockStatusRecord; originalCode: string | null }): void {
    this.service.save(event.record, event.originalCode);
    this.carrySelection(event.originalCode, event.record.code);
    this.notify(`${event.record.name} ${event.originalCode ? 'updated' : 'added'}.`);
    this.closeForm();
  }

  toggleStatus(r: StockStatusRecord): void {
    this.menuFor = null;
    if (r.isDefault && r.status === 'Active') {
      this.notify(`${r.name} is the system default. Make another status the default first.`);
      return;
    }
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.service.save({ ...r, status }, r.code);
    this.notify(`${r.name} marked ${status}.`);
  }
}
