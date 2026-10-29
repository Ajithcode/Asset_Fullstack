import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, LucideAngularModule, Plus, Ruler, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { UOM_TYPES } from '../inventory.data';
import { RecordStatus, UomRecord, UomType } from '../inventory.model';
import { ItemService, UomService } from '../inventory.service';
import { UomForm } from './uom-form/uom-form';

type SortKey = 'name' | 'code' | 'type';
interface Filters {
  search: string;
  type: '' | UomType;
  status: '' | RecordStatus;
}

/** Master Data Management > Inventory > Unit of Measure */
@Component({
  selector: 'app-mdm-inventory-uom',
  imports: [FormsModule, RouterLink, LucideAngularModule, UomForm],
  templateUrl: './uom.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../inventory.css']
})
export class UomPage extends ListPage<UomRecord, Filters, SortKey> {
  readonly service = inject(UomService);
  private readonly items = inject(ItemService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly types = UOM_TYPES;
  readonly icons = { Ruler, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', type: '', status: '' });
    this.service.load();
  }

  keyOf(r: UomRecord): string {
    return r.code;
  }

  nameOf(r: UomRecord): string {
    return r.name;
  }

  protected all(): UomRecord[] {
    return this.service.all();
  }

  protected matches(r: UomRecord, f: Filters): boolean {
    const term = f.search.trim().toLowerCase();
    return (!f.type || r.type === f.type) && (!f.status || r.status === f.status) && (!term || [r.name, r.code, r.symbol].some((v) => v.toLowerCase().includes(term)));
  }

  protected sortValue(r: UomRecord, key: SortKey): string {
    return r[key];
  }

  protected removeRecords(codes: string[]) {
    return this.service.remove(codes);
  }

  // Units used by items or as another unit's base can't be deleted
  protected override deleteBlocker(r: UomRecord): string | null {
    const items = this.items.usingUom(r.code).length;
    if (items) return `${r.name} is used by ${items} item${items === 1 ? '' : 's'}.`;
    const derived = this.service.derived(r.code);
    return derived.length ? `${derived[0].name} is based on ${r.name}. Change its base unit first.` : null;
  }

  /** "1 Box = 12 Nos" style text, or the factor alone for base units. */
  conversion(r: UomRecord): string {
    const base = this.service.find(r.baseUom);
    return base ? `${r.conversionFactor} ${base.symbol}` : String(r.conversionFactor);
  }

  onSaved(event: { record: UomRecord; originalCode: string | null }): void {
    this.persist(this.service.save(event.record, event.originalCode), (saved) => {
      if (event.originalCode) this.items.recode('uom', event.originalCode, saved.code);
      this.carrySelection(event.originalCode, saved.code);
      this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  toggleStatus(r: UomRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.persist(this.service.save({ ...r, status }, r.code), () => this.notify(`${r.name} marked ${status}.`));
  }
}
