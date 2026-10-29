import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, TriangleAlert, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { FAILURE_CATEGORIES, FAILURE_SEVERITIES } from './failure-code.data';
import { FailureCodeForm } from './failure-code-form/failure-code-form';
import { FailureCodeFilters, FailureCodeRecord } from './failure-code.model';
import { FailureCodeService } from './failure-code.service';

type SortKey = 'name' | 'code' | 'category' | 'severity';

/** Master Data Management > Maintenance > Failure Code */
@Component({
  selector: 'app-mdm-maintenance-failure-code',
  imports: [FormsModule, RouterLink, LucideAngularModule, FailureCodeForm],
  templateUrl: './failure-code.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../../people/people.css', '../maintenance.css']
})
export class FailureCodePage extends ListPage<FailureCodeRecord, FailureCodeFilters, SortKey> {
  readonly service = inject(FailureCodeService);
  readonly categories = FAILURE_CATEGORIES;
  readonly severities = FAILURE_SEVERITIES;
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly statuses = ['Active', 'Inactive'];
  readonly icons = { TriangleAlert, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', category: '', severity: '' });
    this.service.load();
  }

  keyOf(r: FailureCodeRecord): string {
    return r.code;
  }

  nameOf(r: FailureCodeRecord): string {
    return r.name;
  }

  protected all(): FailureCodeRecord[] {
    return this.service.all();
  }

  protected matches(r: FailureCodeRecord, f: FailureCodeFilters): boolean {
    const term = f.search.trim().toLowerCase();
    return (!f.category || r.category === f.category) && (!f.severity || r.severity === f.severity) && (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)));
  }

  protected sortValue(r: FailureCodeRecord, key: SortKey): string | number {
    return key === 'severity' ? FAILURE_SEVERITIES.indexOf(r.severity) : r[key];
  }

  protected removeRecords(codes: string[]) {
    return this.service.remove(codes);
  }

  duplicate(r: FailureCodeRecord): void {
    this.openAdd({ ...structuredClone(r), id: '', code: '', name: `${r.name} (Copy)`, status: 'Active' });
  }

  onSaved(event: { record: FailureCodeRecord; originalCode: string | null; originalName: string | null }): void {
    this.persist(this.service.save(event.record, event.originalCode), (saved) => {
      this.carrySelection(event.originalCode, saved.code);
      this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  toggleStatus(r: FailureCodeRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.persist(this.service.save({ ...r, status }, r.code), () => this.notify(`${r.name} marked ${status}.`));
  }
}
