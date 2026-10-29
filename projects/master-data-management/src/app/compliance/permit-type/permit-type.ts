import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, ClipboardCheck, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { PERMIT_CATEGORIES } from '../compliance.data';
import { ComplianceStatus, PermitTypeRecord } from '../compliance.model';
import { PermitTypeService } from '../compliance.service';
import { PermitTypeForm } from './permit-type-form/permit-type-form';

type SortKey = 'name' | 'code' | 'category' | 'validDays';
interface Filters {
  search: string;
  category: string;
  status: '' | ComplianceStatus;
}

/** Master Data Management > Compliance > Permit Type */
@Component({
  selector: 'app-mdm-compliance-permit-type',
  imports: [FormsModule, RouterLink, LucideAngularModule, PermitTypeForm],
  templateUrl: './permit-type.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../compliance.css']
})
export class PermitTypePage extends ListPage<PermitTypeRecord, Filters, SortKey> {
  readonly service = inject(PermitTypeService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly permitCategories = PERMIT_CATEGORIES;
  readonly icons = { ClipboardCheck, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', category: '', status: '' });
    this.service.load();
  }

  keyOf(r: PermitTypeRecord): string {
    return r.code;
  }

  nameOf(r: PermitTypeRecord): string {
    return r.name;
  }

  protected all(): PermitTypeRecord[] {
    return this.service.all();
  }

  protected matches(r: PermitTypeRecord, f: Filters): boolean {
    const term = f.search.trim().toLowerCase();
    return (
      (!f.category || r.category === f.category) &&
      (!f.status || r.status === f.status) &&
      (!term || [r.name, r.code, r.category, r.description].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected sortValue(r: PermitTypeRecord, key: SortKey): string | number {
    return r[key];
  }

  protected removeRecords(codes: string[]) {
    return this.service.remove(codes);
  }

  duplicate(r: PermitTypeRecord): void {
    this.openAdd({ ...structuredClone(r), id: '', code: '', name: `${r.name} (Copy)`, status: 'Active' });
  }

  onSaved(event: { record: PermitTypeRecord; originalCode: string | null }): void {
    this.persist(this.service.save(event.record, event.originalCode), (saved) => {
      this.carrySelection(event.originalCode, saved.code);
      this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  toggleStatus(r: PermitTypeRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.persist(this.service.save({ ...r, status }, r.code), () => this.notify(`${r.name} marked ${status}.`));
  }
}
