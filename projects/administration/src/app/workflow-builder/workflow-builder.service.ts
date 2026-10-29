import { Injectable } from '@angular/core';
import { seedWorkflows } from './workflow-builder.data';
import { Workflow } from './workflow-builder.model';

// DEMO MODE: workflows live in memory until the workflow engine API exists
@Injectable({ providedIn: 'root' })
export class WorkflowService {
  private workflows: Workflow[] = seedWorkflows();
  private nextId = this.workflows.length + 1;

  all(): Workflow[] {
    return this.workflows;
  }

  get(id: string): Workflow | undefined {
    return this.workflows.find((w) => w.id === id);
  }

  codeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    return this.workflows.some((w) => w.code.toUpperCase() === c && w.id !== exceptId);
  }

  newId(): string {
    return `WF-${String(this.nextId++).padStart(4, '0')}`;
  }

  /** Insert or replace (by id); new workflows go to the top of the list. */
  save(wf: Workflow): Workflow {
    const copy = structuredClone(wf);
    const i = this.workflows.findIndex((w) => w.id === wf.id);
    if (i >= 0) this.workflows[i] = copy;
    else this.workflows.unshift(copy);
    return copy;
  }

  remove(id: string): void {
    this.workflows = this.workflows.filter((w) => w.id !== id);
  }
}
