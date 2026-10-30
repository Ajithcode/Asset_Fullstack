import { Injectable } from '@angular/core';
import { seedTemplates } from './notification-templates.data';
import { NotificationTemplate } from './notification-templates.model';

// DEMO MODE: templates live in memory until the notification service API exists
@Injectable({ providedIn: 'root' })
export class NotificationTemplateService {
  private templates: NotificationTemplate[] = seedTemplates(Date.now());
  private nextId = this.templates.length + 1;

  all(): NotificationTemplate[] {
    return this.templates;
  }

  get(id: string): NotificationTemplate | undefined {
    return this.templates.find((t) => t.id === id);
  }

  codeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    return this.templates.some((t) => t.code.toUpperCase() === c && t.id !== exceptId);
  }

  newId(): string {
    return `NT-${String(this.nextId++).padStart(4, '0')}`;
  }

  /** Insert or replace (by id); new templates go to the top. */
  save(t: NotificationTemplate): NotificationTemplate {
    const copy = structuredClone(t);
    const i = this.templates.findIndex((x) => x.id === t.id);
    if (i >= 0) this.templates[i] = copy;
    else this.templates.unshift(copy);
    return copy;
  }

  remove(id: string): void {
    this.templates = this.templates.filter((t) => t.id !== id);
  }
}
