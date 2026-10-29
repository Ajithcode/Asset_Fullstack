import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, Wrench, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { MaintenanceTypeService } from '../maintenance-type/maintenance-type.service';
import { WorkTypeForm } from './work-type-form/work-type-form';
import { WorkTypeFilters, WorkTypeRecord } from './work-type.model';
import { WorkTypeService } from './work-type.service';

type SortKey = 'name' | 'code' | 'maintenanceType' | 'durationHours';

/** Master Data Management > Maintenance > Work Type */
@Component({
  selector: 'app-mdm-maintenance-work-type',
  imports: [FormsModule, RouterLink, LucideAngularModule, WorkTypeForm],
  templateUrl: './work-type.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../../people/people.css', '../maintenance.css']
})
export class WorkTypePage extends ListPage<WorkTypeRecord, WorkTypeFilters, SortKey> {
  readonly service = inject(WorkTypeService);
  private readonly types = inject(MaintenanceTypeService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly statuses = ['Active', 'Inactive'];
  readonly icons = { Wrench, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', maintenanceType: '', status: '' });
    this.service.load();
  }

  get maintenanceTypes(): string[] {
    return this.types.all().map((t) => t.name);
  }

  keyOf(r: WorkTypeRecord): string {
    return r.code;
  }

  nameOf(r: WorkTypeRecord): string {
    return r.name;
  }

  protected all(): WorkTypeRecord[] {
    return this.service.all();
  }

  protected matches(r: WorkTypeRecord, f: WorkTypeFilters): boolean {
    const term = f.search.trim().toLowerCase();
    return (!f.maintenanceType || r.maintenanceType === f.maintenanceType) && (!f.status || r.status === f.status) && (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)));
  }

  protected sortValue(r: WorkTypeRecord, key: SortKey): string | number {
    return r[key];
  }

  protected removeRecords(codes: string[]) {
    return this.service.remove(codes);
  }

  duplicate(r: WorkTypeRecord): void {
    this.openAdd({ ...structuredClone(r), id: '', code: '', name: `${r.name} (Copy)`, status: 'Active' });
  }

  onSaved(event: { record: WorkTypeRecord; originalCode: string | null; originalName: string | null }): void {
    this.persist(this.service.save(event.record, event.originalCode), (saved) => {
      this.carrySelection(event.originalCode, saved.code);
      this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  toggleStatus(r: WorkTypeRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.persist(this.service.save({ ...r, status }, r.code), () => this.notify(`${r.name} marked ${status}.`));
  }
}
