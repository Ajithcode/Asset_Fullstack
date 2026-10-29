import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, HardHat, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { PersonStatus, avatarColor, initials } from '../people.data';
import { SkillService } from '../skill/skill.service';
import { TeamService } from '../team/team.service';
import { blankTechnician } from './technician.data';
import { TechnicianForm } from './technician-form/technician-form';
import { TechnicianFilters, TechnicianRecord } from './technician.model';
import { TechnicianService } from './technician.service';

type SortKey = 'id' | 'name' | 'primarySkill' | 'team';

/** Master Data Management > People > Technician */
@Component({
  selector: 'app-mdm-people-technician',
  imports: [FormsModule, RouterLink, LucideAngularModule, TechnicianForm],
  templateUrl: './technician.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../people.css']
})
export class TechnicianPage extends ListPage<TechnicianRecord, TechnicianFilters, SortKey> {
  readonly service = inject(TechnicianService);
  readonly skills = inject(SkillService);
  private readonly teams = inject(TeamService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly statuses: PersonStatus[] = ['Active', 'On Duty', 'On Leave', 'Inactive'];
  readonly initials = initials;
  readonly avatarColor = avatarColor;
  readonly icons = { HardHat, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', status: '', skill: '' });
    this.sortKey = 'id';
  }

  keyOf(r: TechnicianRecord): string {
    return r.id;
  }

  nameOf(r: TechnicianRecord): string {
    return r.name;
  }

  protected all(): TechnicianRecord[] {
    return this.service.all();
  }

  protected matches(r: TechnicianRecord, f: TechnicianFilters): boolean {
    const term = f.search.trim().toLowerCase();
    return (
      (!f.status || r.status === f.status) &&
      (!f.skill || r.primarySkill === f.skill || r.secondarySkills.includes(f.skill)) &&
      (!term || [r.id, r.name, r.primarySkill, ...r.secondarySkills, this.teamName(r)].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected sortValue(r: TechnicianRecord, key: SortKey): string {
    return key === 'team' ? this.teamName(r) : r[key];
  }

  protected removeRecords(ids: string[]): void {
    this.service.remove(ids);
    this.teams.removeMembers(ids);
  }

  protected override deleteBlocker(r: TechnicianRecord): string | null {
    const led = this.teams.teamsLedBy(r.id);
    return led.length ? `${r.name} leads ${led[0].name}. Choose another team leader first.` : null;
  }

  // Teams own membership; a technician's team is the first team listing them
  teamName(r: TechnicianRecord): string {
    return this.teams.primaryTeamOf(r.id)?.name ?? '';
  }

  addNew(): void {
    this.openAdd(blankTechnician(this.service.nextId()));
  }

  onSaved(event: { record: TechnicianRecord; originalId: string | null; teamCode: string }): void {
    this.service.save(event.record, event.originalId);
    if (event.originalId) this.teams.renameMember(event.originalId, event.record.id);
    this.teams.setPrimaryTeam(event.record.id, event.teamCode || null);
    this.carrySelection(event.originalId, event.record.id);
    this.notify(`${event.record.name} ${event.originalId ? 'updated' : 'added'}.`);
    this.closeForm();
  }

  setStatus(r: TechnicianRecord, status: PersonStatus): void {
    this.menuFor = null;
    this.service.save({ ...r, status, availableForDispatch: status === 'Active' || status === 'On Duty' ? r.availableForDispatch : false }, r.id);
    this.notify(`${r.name} marked ${status}.`);
  }
}
