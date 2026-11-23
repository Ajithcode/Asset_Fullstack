import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, concatMap, from, map, of, tap, toArray } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { Tone, WfGraph, Workflow, WorkflowIcon, WorkflowStatus, WorkflowVersion } from './workflow-builder.model';

/** WorkflowBuilderResponse from the Workflows service. */
interface WorkflowBuilderDto {
  id: string;
  workflowName: string;
  module: string | null;
  triggerEvent: string | null;
  version: string | null;
  startFromTemplate: boolean;
  status: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
  isDeleted: boolean;
  /** JSON of WorkflowDesign. */
  design: string | null;
}

/** What the builder keeps in the API's design field: everything the contract has no column for. */
interface WorkflowDesign {
  code: string;
  owner: string;
  description: string;
  icon: WorkflowIcon;
  tone: Tone;
  graph: WfGraph;
  versions: WorkflowVersion[];
}

/** Body of PUT {workflowsApiUrl}api/workflow-builders/{id} */
interface UpdateWorkflowBuilderRequest {
  workflowName: string;
  module: string;
  triggerEvent: string | null;
  version: string;
  startFromTemplate: boolean;
  status: WorkflowStatus;
  design: string;
  updatedBy: string | null;
}

/** Body of POST {workflowsApiUrl}api/workflow-builders */
type CreateWorkflowBuilderRequest = Omit<UpdateWorkflowBuilderRequest, 'updatedBy'> & {
  createdBy: string | null;
  tenantId: string;
  clientId: string;
};

const LOCAL_PREFIX = 'NEW-';
const STATUSES: WorkflowStatus[] = ['Active', 'Draft', 'Inactive'];

function parseDesign(json: string | null): Partial<WorkflowDesign> {
  if (!json) return {};
  try {
    const value = JSON.parse(json);
    return value && typeof value === 'object' ? (value as Partial<WorkflowDesign>) : {};
  } catch {
    return {};
  }
}

function toWorkflow(dto: WorkflowBuilderDto): Workflow {
  const d = parseDesign(dto.design);
  const graph: WfGraph = d.graph && Array.isArray(d.graph.nodes) && Array.isArray(d.graph.edges) ? d.graph : { nodes: [], edges: [] };
  return {
    id: dto.id,
    code: d.code ?? '',
    name: dto.workflowName,
    category: dto.module ?? '',
    owner: d.owner ?? dto.createdBy ?? '',
    description: d.description ?? '',
    status: STATUSES.find((s) => s.toLowerCase() === (dto.status ?? '').toLowerCase()) ?? 'Draft',
    updated: (dto.updatedAt ?? dto.createdAt ?? new Date().toISOString()).slice(0, 10),
    version: dto.version || 'v0.1',
    versions: Array.isArray(d.versions) ? d.versions : [],
    graph,
    // Instance and approval counts per workflow have no API yet
    runningInstances: 0,
    pendingApprovals: 0,
    icon: d.icon ?? 'zap',
    tone: d.tone ?? 'purple'
  };
}

function designOf(wf: Workflow): string {
  const design: WorkflowDesign = {
    code: wf.code,
    owner: wf.owner,
    description: wf.description,
    icon: wf.icon,
    tone: wf.tone,
    graph: wf.graph,
    versions: wf.versions
  };
  return JSON.stringify(design);
}

/** First readable message from a ValidationProblem / error body / network failure. */
export function workflowApiError(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    const first = body?.errors && (Object.values(body.errors) as string[][])[0]?.[0];
    if (first) return first;
    if (typeof body?.message === 'string') return body.message;
    if (err.status === 0) return 'The Workflows service could not be reached.';
    if (err.status === 403) return 'Your role does not allow this change.';
  }
  return 'The Workflows service returned an error.';
}

/** Workflow designs stored in the Workflows service (GET / POST / PUT / DELETE api/workflow-builders). */
@Injectable({ providedIn: 'root' })
export class WorkflowService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.workflowsApiUrl}api/workflow-builders`;
  private workflows: Workflow[] = [];
  private nextId = 1;

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

  /** Temporary id for a workflow that has not been sent to the API yet. */
  newId(): string {
    return `${LOCAL_PREFIX}${String(this.nextId++).padStart(4, '0')}`;
  }

  /** GET api/workflow-builders; only this tenant's workflows are listed. */
  load(): Observable<Workflow[]> {
    return this.http.get<WorkflowBuilderDto[]>(this.apiUrl).pipe(
      map((list) =>
        (list ?? [])
          .filter((dto) => !dto.isDeleted && (!dto.tenantId || dto.tenantId === environment.tenantId))
          .map(toWorkflow)
          .sort((a, b) => b.updated.localeCompare(a.updated))
      ),
      tap((list) => (this.workflows = list))
    );
  }

  /** POST for a new workflow, PUT for a stored one. Emits the stored copy (a new one gets the server id). */
  save(wf: Workflow): Observable<Workflow> {
    const fields = {
      workflowName: wf.name.trim(),
      module: wf.category,
      triggerEvent: wf.graph.nodes.find((n) => n.type === 'trigger')?.config.triggerEvent || null,
      version: wf.version,
      startFromTemplate: false,
      status: wf.status,
      design: designOf(wf)
    };
    const request$ = wf.id.startsWith(LOCAL_PREFIX)
      ? this.http.post<WorkflowBuilderDto>(this.apiUrl, {
          ...fields,
          createdBy: this.auth.getUserEmail(),
          tenantId: environment.tenantId,
          clientId: environment.clientId
        } satisfies CreateWorkflowBuilderRequest)
      : this.http.put<WorkflowBuilderDto>(`${this.apiUrl}/${encodeURIComponent(wf.id)}`, {
          ...fields,
          updatedBy: this.auth.getUserEmail()
        } satisfies UpdateWorkflowBuilderRequest);
    return request$.pipe(
      map((dto) => ({ ...toWorkflow(dto), runningInstances: wf.runningInstances, pendingApprovals: wf.pendingApprovals })),
      tap((stored) => {
        const i = this.workflows.findIndex((w) => w.id === wf.id);
        if (i >= 0) this.workflows[i] = stored;
        else this.workflows.unshift(stored);
      })
    );
  }

  /** Saves several new workflows one after another (import); emits the stored copies that succeeded. */
  saveMany(list: Workflow[]): Observable<Workflow[]> {
    if (!list.length) return of([]);
    return from(list).pipe(
      concatMap((wf) => this.save(wf)),
      toArray()
    );
  }

  /** DELETE api/workflow-builders/{id}; a workflow never sent to the API is only dropped locally. */
  remove(id: string): Observable<void> {
    const request$ = id.startsWith(LOCAL_PREFIX) ? of(undefined) : this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(id)}`);
    return request$.pipe(tap(() => (this.workflows = this.workflows.filter((w) => w.id !== id))));
  }
}
