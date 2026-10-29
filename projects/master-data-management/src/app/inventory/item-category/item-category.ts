import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, LayoutGrid, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { ItemCategoryRecord, RecordStatus } from '../inventory.model';
import { ItemCategoryService, ItemService } from '../inventory.service';
import { ItemCategoryForm } from './item-category-form/item-category-form';

type SortKey = 'name' | 'code' | 'parent';
interface Filters {
  search: string;
  status: '' | RecordStatus;
}

/** Master Data Management > Inventory > Item Category */
@Component({
  selector: 'app-mdm-inventory-item-category',
  imports: [FormsModule, RouterLink, LucideAngularModule, ItemCategoryForm],
  templateUrl: './item-category.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../inventory.css']
})
export class ItemCategoryPage extends ListPage<ItemCategoryRecord, Filters, SortKey> {
  readonly service = inject(ItemCategoryService);
  private readonly items = inject(ItemService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly icons = { LayoutGrid, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', status: '' });
    this.service.load();
  }

  keyOf(r: ItemCategoryRecord): string {
    return r.code;
  }

  nameOf(r: ItemCategoryRecord): string {
    return r.name;
  }

  protected all(): ItemCategoryRecord[] {
    return this.service.all();
  }

  protected matches(r: ItemCategoryRecord, f: Filters): boolean {
    const term = f.search.trim().toLowerCase();
    return (!f.status || r.status === f.status) && (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)));
  }

  protected sortValue(r: ItemCategoryRecord, key: SortKey): string {
    return key === 'parent' ? this.parentName(r) : r[key];
  }

  protected removeRecords(codes: string[]) {
    return this.service.remove(codes);
  }

  // Categories with items or sub-categories can't be deleted
  protected override deleteBlocker(r: ItemCategoryRecord): string | null {
    const items = this.items.usingCategory(r.code).length;
    if (items) return `${r.name} has ${items} item${items === 1 ? '' : 's'}. Move them to another category first.`;
    const children = this.service.children(r.code).length;
    return children ? `${r.name} has ${children} sub-categor${children === 1 ? 'y' : 'ies'}. Move or delete them first.` : null;
  }

  parentName(r: ItemCategoryRecord): string {
    return this.service.find(r.parentCode)?.name ?? '';
  }

  itemCount(r: ItemCategoryRecord): number {
    return this.items.usingCategory(r.code).length;
  }

  addChild(parent: ItemCategoryRecord): void {
    this.openAdd({ ...structuredClone(parent), id: '', code: '', name: '', description: '', parentCode: parent.code, status: 'Active' });
  }

  onSaved(event: { record: ItemCategoryRecord; originalCode: string | null }): void {
    this.persist(this.service.save(event.record, event.originalCode), (saved) => {
      if (event.originalCode) this.items.recode('category', event.originalCode, saved.code);
      this.carrySelection(event.originalCode, saved.code);
      this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  toggleStatus(r: ItemCategoryRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.persist(this.service.save({ ...r, status }, r.code), () => this.notify(`${r.name} marked ${status}.`));
  }
}
