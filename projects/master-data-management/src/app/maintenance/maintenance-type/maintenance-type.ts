import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, Settings, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { TaskTemplateService } from '../task-template/task-template.service';
import { WorkTypeService } from '../work-type/work-type.service';
import { MaintenanceTypeForm } from './maintenance-type-form/maintenance-type-form';
import { MaintenanceTypeFilters, MaintenanceTypeRecord } from './maintenance-type.model';
import { MaintenanceTypeService } from './maintenance-type.service';

type SortKey = 'name' | 'code';

/** Master Data Management > Maintenance > Maintenance Type */
@Component({
  selector: 'app-mdm-maintenance-maintenance-type',
  imports: [FormsModule, RouterLink, LucideAngularModule, MaintenanceTypeForm],
  templateUrl: './maintenance-type.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../../people/people.css', '../maintenance.css']
})
export class MaintenanceTypePage extends ListPage<MaintenanceTypeRecord, MaintenanceTypeFilters, SortKey> {
  readonly service = inject(MaintenanceTypeService);
  private readonly workTypes = inject(WorkTypeService);
  private readonly templates = inject(TaskTemplateService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly statuses = ['Active', 'Inactive'];
  readonly icons = { Settings, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', status: '' });
    this.service.load();
  }

  keyOf(r: MaintenanceTypeRecord): string {
    return r.code;
  }

  nameOf(r: MaintenanceTypeRecord): string {
    return r.name;
  }

  protected all(): MaintenanceTypeRecord[] {
    return this.service.all();
  }

  protected matches(r: MaintenanceTypeRecord, f: MaintenanceTypeFilters): boolean {
    const term = f.search.trim().toLowerCase();
    return (!f.status || r.status === f.status) && (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)));
  }

  protected sortValue(r: MaintenanceTypeRecord, key: SortKey): string | number {
    return r[key];
  }

  protected removeRecords(codes: string[]) {
    return this.service.remove(codes);
  }

  // Types used by work types or task templates can't be deleted
  protected override deleteBlocker(r: MaintenanceTypeRecord): string | null {
    const count = this.workTypes.usesType(r.name) + this.templates.usesType(r.name);
    return count ? `${r.name} is used by ${count} work types and task templates. Mark it Inactive instead.` : null;
  }

  duplicate(r: MaintenanceTypeRecord): void {
    this.openAdd({ ...structuredClone(r), id: '', code: '', name: `${r.name} (Copy)`, status: 'Active' });
  }

  onSaved(event: { record: MaintenanceTypeRecord; originalCode: string | null; originalName: string | null }): void {
    this.persist(this.service.save(event.record, event.originalCode), (saved) => {
      // Work types and task templates refer to the type by name
      if (event.originalName) {
        this.workTypes.renameType(event.originalName, saved.name);
        this.templates.renameType(event.originalName, saved.name);
      }
      this.carrySelection(event.originalCode, saved.code);
      this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  toggleStatus(r: MaintenanceTypeRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.persist(this.service.save({ ...r, status }, r.code), () => this.notify(`${r.name} marked ${status}.`));
  }
}
