import { Injectable, signal } from '@angular/core';
import { CURRENT_USER, todayIso } from '../shared/admin-common';
import { TEMPLATES } from './checklist-templates.data';
import { CATEGORY_CODES, ChecklistTemplate, TemplateStatus, bumpVersion } from './checklist-templates.model';

// DEMO MODE: checklist templates live in memory until the inspection API exists
@Injectable({ providedIn: 'root' })
export class ChecklistTemplatesService {
  private readonly store = signal<ChecklistTemplate[]>(structuredClone(TEMPLATES));
  readonly templates = this.store.asReadonly();
  private seq = TEMPLATES.length;
  private idSeq = 1000;

  get(id: string): ChecklistTemplate | undefined {
    return this.store().find((t) => t.id === id);
  }

  /** Unique id for new sections / items */
  newId(prefix: string): string {
    this.idSeq += 1;
    return `${prefix}-${this.idSeq}`;
  }

  codeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    return this.store().some((t) => t.id !== exceptId && t.code.toUpperCase() === c);
  }

  /** Next free CHK-<CAT>-nnn code for a category */
  nextCode(category: string): string {
    const prefix = `CHK-${CATEGORY_CODES[category] ?? 'GEN'}-`;
    let n = 1;
    while (this.codeTaken(prefix + String(n).padStart(3, '0'), null)) n++;
    return prefix + String(n).padStart(3, '0');
  }

  /**
   * Insert or update. Saving a draft marks the template Draft; publishing makes it Active and
   * bumps the version. Both add a version-history row.
   */
  save(draft: ChecklistTemplate, mode: 'draft' | 'publish' | 'settings'): ChecklistTemplate {
    const copy = structuredClone(draft);
    copy.name = copy.name.trim();
    copy.code = copy.code.trim().toUpperCase();
    copy.updated = todayIso();
    const isNew = !copy.id;
    if (isNew) {
      this.seq += 1;
      copy.id = `CHT-${String(this.seq).padStart(3, '0')}`;
    }
    if (mode === 'publish') {
      if (!isNew) copy.version = bumpVersion(copy.version);
      copy.status = 'Active';
    } else if (mode === 'draft') {
      copy.status = 'Draft';
    }
    if (mode !== 'settings' || isNew) {
      // Older published versions are superseded
      const retired: TemplateStatus = 'Inactive';
      const note = mode === 'publish' ? 'Published' : isNew ? 'Created template' : 'Draft saved';
      const history = copy.versions.filter((v) => v.version !== copy.version).map((v) => (v.status === 'Active' ? { ...v, status: retired } : v));
      copy.versions = [{ version: copy.version, date: copy.updated, user: CURRENT_USER.name, status: copy.status, note }, ...history];
    }
    this.store.update((list) => (isNew ? [copy, ...list] : list.map((t) => (t.id === copy.id ? copy : t))));
    return copy;
  }

  setStatus(id: string, status: TemplateStatus): void {
    this.store.update((list) => list.map((t) => (t.id === id ? { ...t, status, updated: todayIso() } : t)));
  }

  remove(id: string): void {
    this.store.update((list) => list.filter((t) => t.id !== id));
  }

  /** Copy as a new draft with a fresh code; not stored until saved */
  cloneOf(source: ChecklistTemplate, name = `${source.name} (Copy)`): ChecklistTemplate {
    const copy = structuredClone(source);
    copy.id = '';
    copy.name = name;
    copy.code = this.nextCode(source.category);
    copy.version = 'v1.0';
    copy.status = 'Draft';
    copy.owner = CURRENT_USER.name;
    copy.uses = 0;
    copy.executions = [];
    copy.versions = [];
    copy.sections.forEach((s) => {
      s.id = this.newId('S');
      s.items.forEach((i) => (i.id = this.newId('I')));
    });
    return copy;
  }
}
