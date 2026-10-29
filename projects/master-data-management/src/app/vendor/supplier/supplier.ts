import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, LucideAngularModule, Plus, Search, Star, Truck } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { badgeInk, badgeText } from '../../asset/manufacturer/manufacturer.data';
import { ListPage } from '../../shared/list-page';
import { SUPPLIER_CATEGORIES } from './supplier.data';
import { SupplierForm } from './supplier-form/supplier-form';
import { SupplierFilters, SupplierRecord } from './supplier.model';
import { SupplierService } from './supplier.service';

type SortKey = 'name' | 'code' | 'category' | 'country' | 'rating';

/** Master Data Management > Vendor > Supplier */
@Component({
  selector: 'app-mdm-vendor-supplier',
  imports: [FormsModule, RouterLink, LucideAngularModule, SupplierForm],
  templateUrl: './supplier.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../vendor.css']
})
export class SupplierPage extends ListPage<SupplierRecord, SupplierFilters, SortKey> {
  readonly service = inject(SupplierService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly categories = SUPPLIER_CATEGORIES;
  readonly badgeText = badgeText;
  readonly badgeInk = badgeInk;
  readonly icons = { Truck, Star, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', category: '', country: '', status: '' });
    this.service.load();
  }

  keyOf(r: SupplierRecord): string {
    return r.code;
  }

  nameOf(r: SupplierRecord): string {
    return r.name;
  }

  protected all(): SupplierRecord[] {
    return this.service.all();
  }

  protected matches(r: SupplierRecord, f: SupplierFilters): boolean {
    const term = f.search.trim().toLowerCase();
    return (
      (!f.category || r.category === f.category) &&
      (!f.country || r.country === f.country) &&
      (!f.status || r.status === f.status) &&
      (!term || [r.name, r.code, r.contactPerson, r.category, r.email].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected sortValue(r: SupplierRecord, key: SortKey): string | number {
    return r[key];
  }

  protected removeRecords(codes: string[]) {
    return this.service.remove(codes);
  }

  duplicate(r: SupplierRecord): void {
    this.openAdd({ ...structuredClone(r), id: '', code: '', name: `${r.name} (Copy)`, status: 'Active', logo: null });
  }

  onSaved(event: { record: SupplierRecord; originalCode: string | null }): void {
    this.persist(this.service.save(event.record, event.originalCode), (saved) => {
      this.carrySelection(event.originalCode, saved.code);
      this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  toggleStatus(r: SupplierRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.persist(this.service.save({ ...r, status }, r.code), () => this.notify(`${r.name} marked ${status}.`));
  }
}
