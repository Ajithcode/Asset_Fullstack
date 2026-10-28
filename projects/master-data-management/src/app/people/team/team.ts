import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, LucideAngularModule, Plus, Search, Users } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { EmployeeService } from '../employee/employee.service';
import { PEOPLE_DEPARTMENTS } from '../people.data';
import { TechnicianService } from '../technician/technician.service';
import { TeamForm } from './team-form/team-form';
import { TeamFilters, TeamRecord } from './team.model';
import { TeamService } from './team.service';

type SortKey = 'name' | 'code' | 'department' | 'members';

/** Master Data Management > People > Team */
@Component({
  selector: 'app-mdm-people-team',
  imports: [FormsModule, RouterLink, LucideAngularModule, TeamForm],
  templateUrl: './team.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../people.css']
})
export class TeamPage extends ListPage<TeamRecord, TeamFilters, SortKey> {
  readonly service = inject(TeamService);
  private readonly employees = inject(EmployeeService);
  private readonly technicians = inject(TechnicianService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly departments = PEOPLE_DEPARTMENTS;
  readonly statuses: TeamRecord['status'][] = ['Active', 'On Leave', 'Inactive'];
  readonly icons = { Users, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', department: '', status: '' });
  }

  keyOf(r: TeamRecord): string {
    return r.code;
  }

  nameOf(r: TeamRecord): string {
    return r.name;
  }

  protected all(): TeamRecord[] {
    return this.service.all();
  }

  protected matches(r: TeamRecord, f: TeamFilters): boolean {
    const term = f.search.trim().toLowerCase();
    return (
      (!f.department || r.department === f.department) &&
      (!f.status || r.status === f.status) &&
      (!term || [r.name, r.code, r.department, this.leaderName(r)].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected sortValue(r: TeamRecord, key: SortKey): string | number {
    return key === 'members' ? r.members.length : r[key];
  }

  protected removeRecords(codes: string[]): void {
    this.service.remove(codes);
  }

  /** Name of an employee (EMP…) or technician (TECH…). */
  personName(id: string): string {
    return (id.startsWith('TECH') ? this.technicians.find(id)?.name : this.employees.find(id)?.name) ?? id;
  }

  leaderName(r: TeamRecord): string {
    return r.leaderId ? this.personName(r.leaderId) : '';
  }

  duplicate(r: TeamRecord): void {
    this.openAdd({ ...structuredClone(r), code: '', name: `${r.name} (Copy)`, status: 'Active' });
  }

  onSaved(event: { record: TeamRecord; originalCode: string | null }): void {
    this.service.save(event.record, event.originalCode);
    this.carrySelection(event.originalCode, event.record.code);
    this.notify(`${event.record.name} ${event.originalCode ? 'updated' : 'added'}.`);
    this.closeForm();
  }

  setStatus(r: TeamRecord, status: TeamRecord['status']): void {
    this.menuFor = null;
    this.service.save({ ...r, status }, r.code);
    this.notify(`${r.name} marked ${status}.`);
  }
}
