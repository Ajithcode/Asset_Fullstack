import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, Award, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { ListPage } from '../../shared/list-page';
import { CERT_CATEGORIES } from '../compliance.data';
import { ComplianceStatus, CertificationTypeRecord } from '../compliance.model';
import { CertificationTypeService } from '../compliance.service';
import { CertificationTypeForm } from './certification-type-form/certification-type-form';

type SortKey = 'name' | 'code' | 'category' | 'validityMonths';
interface Filters {
  search: string;
  category: string;
  status: '' | ComplianceStatus;
}

/** Master Data Management > Compliance > Certification Type */
@Component({
  selector: 'app-mdm-compliance-certification-type',
  imports: [FormsModule, RouterLink, LucideAngularModule, CertificationTypeForm],
  templateUrl: './certification-type.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../compliance.css']
})
export class CertificationTypePage extends ListPage<CertificationTypeRecord, Filters, SortKey> {
  readonly service = inject(CertificationTypeService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly certCategories = CERT_CATEGORIES;
  readonly icons = { Award, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', category: '', status: '' });
    this.service.load();
  }

  keyOf(r: CertificationTypeRecord): string {
    return r.code;
  }

  nameOf(r: CertificationTypeRecord): string {
    return r.name;
  }

  protected all(): CertificationTypeRecord[] {
    return this.service.all();
  }

  protected matches(r: CertificationTypeRecord, f: Filters): boolean {
    const term = f.search.trim().toLowerCase();
    return (
      (!f.category || r.category === f.category) &&
      (!f.status || r.status === f.status) &&
      (!term || [r.name, r.code, r.category, r.issuingBody].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected sortValue(r: CertificationTypeRecord, key: SortKey): string | number {
    return r[key];
  }

  protected removeRecords(codes: string[]) {
    return this.service.remove(codes);
  }

  duplicate(r: CertificationTypeRecord): void {
    this.openAdd({ ...structuredClone(r), id: '', code: '', name: `${r.name} (Copy)`, status: 'Active' });
  }

  onSaved(event: { record: CertificationTypeRecord; originalCode: string | null }): void {
    this.persist(this.service.save(event.record, event.originalCode), (saved) => {
      this.carrySelection(event.originalCode, saved.code);
      this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
      this.closeForm();
    });
  }

  toggleStatus(r: CertificationTypeRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.persist(this.service.save({ ...r, status }, r.code), () => this.notify(`${r.name} marked ${status}.`));
  }
}
