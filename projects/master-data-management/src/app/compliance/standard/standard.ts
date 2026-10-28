import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, FileText, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { STANDARD_CATEGORIES, STANDARD_ORGANIZATIONS } from '../compliance.data';
import { ComplianceStatus, StandardRecord } from '../compliance.model';
import { CertificationTypeService, RegulationService, StandardService } from '../compliance.service';
import { StandardForm } from './standard-form/standard-form';

type SortKey = 'name' | 'code' | 'organization' | 'category';
interface Filters {
  search: string;
  organization: string;
  category: string;
  status: '' | ComplianceStatus;
}

/** Master Data Management > Compliance > Standard */
@Component({
  selector: 'app-mdm-compliance-standard',
  imports: [FormsModule, RouterLink, LucideAngularModule, StandardForm],
  templateUrl: './standard.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../compliance.css']
})
export class StandardPage extends ListPage<StandardRecord, Filters, SortKey> {
  readonly service = inject(StandardService);
  private readonly certifications = inject(CertificationTypeService);
  private readonly regulations = inject(RegulationService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly standardCategories = STANDARD_CATEGORIES;
  readonly standardOrganizations = STANDARD_ORGANIZATIONS;
  readonly icons = { FileText, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', organization: '', category: '', status: '' });
  }

  keyOf(r: StandardRecord): string {
    return r.code;
  }

  nameOf(r: StandardRecord): string {
    return r.name;
  }

  protected all(): StandardRecord[] {
    return this.service.all();
  }

  protected matches(r: StandardRecord, f: Filters): boolean {
    const term = f.search.trim().toLowerCase();
    return (
      (!f.organization || r.organization === f.organization) &&
      (!f.category || r.category === f.category) &&
      (!f.status || r.status === f.status) &&
      (!term || [r.name, r.code, r.organization, r.category, r.title].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected sortValue(r: StandardRecord, key: SortKey): string | number {
    return r[key];
  }

  protected removeRecords(codes: string[]): void {
    this.service.remove(codes);
  }

  // Standards referenced by certification types or regulations can't be deleted
  protected override deleteBlocker(r: StandardRecord): string | null {
    const users = [...this.certifications.usingStandard(r.code), ...this.regulations.usingStandard(r.code)];
    return users.length ? `${r.name} is used by ${users[0].name}${users.length > 1 ? ` and ${users.length - 1} more` : ''}. Remove it there first.` : null;
  }

  duplicate(r: StandardRecord): void {
    this.openAdd({ ...structuredClone(r), code: '', name: `${r.name} (Copy)`, status: 'Active' });
  }

  onSaved(event: { record: StandardRecord; originalCode: string | null }): void {
    this.service.save(event.record, event.originalCode);
    if (event.originalCode) {
      this.certifications.recodeStandard(event.originalCode, event.record.code);
      this.regulations.recodeStandard(event.originalCode, event.record.code);
    }
    this.carrySelection(event.originalCode, event.record.code);
    this.notify(`${event.record.name} ${event.originalCode ? 'updated' : 'added'}.`);
    this.closeForm();
  }

  toggleStatus(r: StandardRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.service.save({ ...r, status }, r.code);
    this.notify(`${r.name} marked ${status}.`);
  }
}
