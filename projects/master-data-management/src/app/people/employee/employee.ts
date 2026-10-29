import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, LucideAngularModule, Plus, Search, Users } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { concat, tap } from 'rxjs';
import { DepartmentService } from '../../organization/department/department.service';
import { ListPage } from '../../shared/list-page';
import { avatarColor, initials } from '../people.data';
import { TeamService } from '../team/team.service';
import { blankEmployee } from './employee.data';
import { EmployeeForm } from './employee-form/employee-form';
import { EmployeeFilters, EmployeeRecord, EmployeeStatus } from './employee.model';
import { EmployeeService } from './employee.service';

type SortKey = 'id' | 'name' | 'department' | 'designation';

/** Master Data Management > People > Employee */
@Component({
  selector: 'app-mdm-people-employee',
  imports: [FormsModule, RouterLink, LucideAngularModule, EmployeeForm],
  templateUrl: './employee.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../people.css']
})
export class EmployeePage extends ListPage<EmployeeRecord, EmployeeFilters, SortKey> {
  readonly service = inject(EmployeeService);
  private readonly teams = inject(TeamService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  private readonly departmentService = inject(DepartmentService);
  readonly statuses: EmployeeStatus[] = ['Active', 'On Leave', 'Inactive'];
  readonly initials = initials;
  readonly avatarColor = avatarColor;
  readonly icons = { Users, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', department: '', status: '' });
    this.service.load();
    this.sortKey = 'id';
  }

  keyOf(r: EmployeeRecord): string {
    return r.id;
  }

  nameOf(r: EmployeeRecord): string {
    return r.name;
  }

  protected all(): EmployeeRecord[] {
    return this.service.all();
  }

  protected matches(r: EmployeeRecord, f: EmployeeFilters): boolean {
    const term = f.search.trim().toLowerCase();
    return (
      (!f.department || r.department === f.department) &&
      (!f.status || r.status === f.status) &&
      (!term || [r.id, r.name, r.department, r.designation, r.email].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected sortValue(r: EmployeeRecord, key: SortKey): string {
    return r[key];
  }

  get departments(): string[] {
    return this.departmentService.names();
  }

  protected removeRecords(ids: string[]) {
    return concat(...ids.map((id) => this.service.remove(this.service.find(id)!))).pipe(tap({ complete: () => this.teams.removeMembers(ids) }));
  }

  // Team leaders and managers with reports must be replaced first
  protected override deleteBlocker(r: EmployeeRecord): string | null {
    const led = this.teams.teamsLedBy(r.id);
    if (led.length) return `${r.name} leads ${led[0].name}. Choose another team leader first.`;
    const reports = this.service.reportsOf(r.id).length;
    return reports ? `${reports} employee${reports === 1 ? ' reports' : 's report'} to ${r.name}. Change their manager first.` : null;
  }

  addNew(): void {
    this.openAdd(blankEmployee(this.service.nextId()));
  }

  onSaved(event: { record: EmployeeRecord; originalId: string | null }): void {
    this.persist(this.service.save(event.record, event.originalId), (saved) => {
      if (event.originalId) this.teams.renameMember(event.originalId, saved.id);
      this.carrySelection(event.originalId, saved.id);
      this.notify(`${saved.name} ${event.originalId ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  setStatus(r: EmployeeRecord, status: EmployeeStatus): void {
    this.menuFor = null;
    this.persist(this.service.save({ ...r, status }, r.id), () => this.notify(`${r.name} marked ${status}.`));
  }
}
