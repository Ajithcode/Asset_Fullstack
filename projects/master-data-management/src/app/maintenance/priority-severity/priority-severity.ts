import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, ShieldAlert, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { WorkTypeService } from '../work-type/work-type.service';
import { PriorityForm } from './priority-form/priority-form';
import { PriorityFilters, PriorityRecord } from './priority-severity.model';
import { PriorityService } from './priority-severity.service';

type SortKey = 'name' | 'code' | 'kind';

/** Master Data Management > Maintenance > Priority / Severity */
@Component({
  selector: 'app-mdm-maintenance-priority-severity',
  imports: [FormsModule, RouterLink, LucideAngularModule, PriorityForm],
  templateUrl: './priority-severity.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../../people/people.css', '../maintenance.css']
})
export class PrioritySeverityPage extends ListPage<PriorityRecord, PriorityFilters, SortKey> {
  readonly service = inject(PriorityService);
  private readonly workTypes = inject(WorkTypeService);
  readonly kinds = ['Priority', 'Severity'];
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly statuses = ['Active', 'Inactive'];
  readonly icons = { ShieldAlert, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', kind: '', status: '' });
    this.service.load();
  }

  keyOf(r: PriorityRecord): string {
    return r.code;
  }

  nameOf(r: PriorityRecord): string {
    return r.name;
  }

  protected all(): PriorityRecord[] {
    return this.service.all();
  }

  protected matches(r: PriorityRecord, f: PriorityFilters): boolean {
    const term = f.search.trim().toLowerCase();
    return (!f.kind || r.kind === f.kind) && (!f.status || r.status === f.status) && (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)));
  }

  protected sortValue(r: PriorityRecord, key: SortKey): string | number {
    return r[key];
  }

  protected removeRecords(codes: string[]) {
    return this.service.remove(codes);
  }

  // Priorities set as a work type default can't be deleted
  protected override deleteBlocker(r: PriorityRecord): string | null {
    const count = r.kind === 'Priority' ? this.workTypes.usesPriority(r.name) : 0;
    return count ? `${r.name} is the default priority of ${count} work type${count === 1 ? '' : 's'}. Change them first.` : null;
  }

  duplicate(r: PriorityRecord): void {
    this.openAdd({ ...structuredClone(r), id: '', code: '', name: `${r.name} (Copy)`, status: 'Active' });
  }

  onSaved(event: { record: PriorityRecord; originalCode: string | null; originalName: string | null }): void {
    this.persist(this.service.save(event.record, event.originalCode), (saved) => {
      if (event.originalName && saved.kind === 'Priority') this.workTypes.renamePriority(event.originalName, saved.name);
      this.carrySelection(event.originalCode, saved.code);
      this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  toggleStatus(r: PriorityRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.persist(this.service.save({ ...r, status }, r.code), () => this.notify(`${r.name} marked ${status}.`));
  }
}
