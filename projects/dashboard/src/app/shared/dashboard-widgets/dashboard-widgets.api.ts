import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../../../src/environments/environment';

/** Raw shape returned by GET/POST/PUT {dashboardApiUrl}api/widgets */
interface WidgetDto {
  widgetId: string;
  dashboardId: string;
  moduleName: string;
  widgetList: string[];
}

/** Raw shape returned by {dashboardApiUrl}api/dashboards */
interface DashboardDto {
  dashboardId: string;
  dashboardName: string;
}

/** Request body for POST api/widgets */
interface CreateWidgetRequest {
  dashboardId: string;
  moduleName: string;
  widgetList: string[];
  createdBy: string;
  tenantId: string;
  clientId: string;
}

/** Request body for PUT api/widgets/{widgetId} */
interface UpdateWidgetRequest {
  dashboardId: string;
  moduleName: string;
  widgetList: string[];
  updatedBy: string;
}

/** Request body for POST api/dashboards */
interface CreateDashboardRequest {
  dashboardName: string;
  createdBy: string;
  tenantId: string;
  clientId: string;
}

const DEFAULT_DASHBOARD_NAME = 'Main Dashboard';

/**
 * Saves the dashboard's widget selection through the API gateway.
 * The backend keeps one widget record per module (moduleName + widgetList) under a dashboard,
 * and a widget record can only point at a dashboard that exists.
 */
@Injectable({ providedIn: 'root' })
export class DashboardWidgetsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.dashboardApiUrl}api`;
  private readonly tenancy = { tenantId: environment.tenantId, clientId: environment.clientId };

  private dashboardId?: Promise<string>;
  private loaded?: Promise<Map<string, string[]>>;
  /** widgetId of each module's saved record, so later saves update it instead of creating another. */
  private readonly recordIds = new Map<string, string>();
  private queue: Promise<unknown> = Promise.resolve();

  /** Saved widget lists by module name. */
  load(): Promise<Map<string, string[]>> {
    this.loaded ??= this.fetchRecords().catch((err) => {
      this.loaded = undefined; // let the next call retry
      throw err;
    });
    return this.loaded;
  }

  /**
   * Saves one module's widget list: creates its record the first time, updates it afterwards,
   * and deletes it when the list is empty. Saves run one at a time so two quick saves of a new
   * module can't both create a record.
   */
  save(moduleName: string, widgetList: string[]): Promise<void> {
    const run = this.queue.then(() => this.saveNow(moduleName, widgetList));
    this.queue = run.catch(() => undefined);
    return run;
  }

  private async saveNow(moduleName: string, widgetList: string[]): Promise<void> {
    await this.load();
    const dashboardId = await this.getDashboardId();
    const widgetId = this.recordIds.get(moduleName);

    if (!widgetList.length) {
      if (widgetId) await this.ignoreNotFound(firstValueFrom(this.http.delete(`${this.baseUrl}/widgets/${widgetId}`)));
      this.recordIds.delete(moduleName);
      return;
    }

    if (widgetId) {
      const request: UpdateWidgetRequest = { dashboardId, moduleName, widgetList, updatedBy: currentUser() };
      try {
        await firstValueFrom(this.http.put<WidgetDto>(`${this.baseUrl}/widgets/${widgetId}`, request));
        return;
      } catch (err) {
        // Deleted elsewhere since we loaded it: fall through and create it again
        if (status(err) !== 404) throw err;
        this.recordIds.delete(moduleName);
      }
    }

    const request: CreateWidgetRequest = { dashboardId, moduleName, widgetList, createdBy: currentUser(), ...this.tenancy };
    const created = await firstValueFrom(this.http.post<WidgetDto>(`${this.baseUrl}/widgets`, request));
    this.recordIds.set(moduleName, created.widgetId);
  }

  private async fetchRecords(): Promise<Map<string, string[]>> {
    const dashboardId = await this.getDashboardId();
    const records = await firstValueFrom(
      this.http.get<WidgetDto[]>(`${this.baseUrl}/widgets`, { params: { dashboardId, ...this.tenancy } }),
    );
    this.recordIds.clear();
    const lists = new Map<string, string[]>();
    for (const r of records) {
      // If a module somehow has several records, use the first and keep updating that one
      if (lists.has(r.moduleName)) continue;
      this.recordIds.set(r.moduleName, r.widgetId);
      lists.set(r.moduleName, r.widgetList ?? []);
    }
    return lists;
  }

  /** The tenant's dashboard, created on first use because widget records must reference one. */
  private getDashboardId(): Promise<string> {
    this.dashboardId ??= this.resolveDashboardId().catch((err) => {
      this.dashboardId = undefined;
      throw err;
    });
    return this.dashboardId;
  }

  private async resolveDashboardId(): Promise<string> {
    const dashboards = await firstValueFrom(
      this.http.get<DashboardDto[]>(`${this.baseUrl}/dashboards`, { params: this.tenancy }),
    );
    const existing = dashboards.find((d) => d.dashboardName === DEFAULT_DASHBOARD_NAME) ?? dashboards[0];
    if (existing) return existing.dashboardId;

    const request: CreateDashboardRequest = { dashboardName: DEFAULT_DASHBOARD_NAME, createdBy: currentUser(), ...this.tenancy };
    const created = await firstValueFrom(this.http.post<DashboardDto>(`${this.baseUrl}/dashboards`, request));
    return created.dashboardId;
  }

  private async ignoreNotFound(request: Promise<unknown>): Promise<void> {
    try {
      await request;
    } catch (err) {
      if (status(err) !== 404) throw err;
    }
  }
}

function status(error: unknown): number {
  return error instanceof HttpErrorResponse ? error.status : 0;
}

function currentUser(): string {
  try {
    return localStorage.getItem('userEmail') || 'unknown';
  } catch {
    return 'unknown';
  }
}
