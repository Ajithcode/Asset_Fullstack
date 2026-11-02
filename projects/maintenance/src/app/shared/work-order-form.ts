import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, X } from 'lucide-angular';
import { AssetPicker } from './asset-picker';
import { CatalogAsset } from './asset-catalog';
import { MaintenanceApi, apiErrorMessage, nextCode } from './maintenance-api';
import { PRIORITIES, WORK_TYPES, WO_CATEGORIES, WO_SOURCES, WO_STATUSES, WorkOrder } from './maintenance.models';
import { day, today } from './derive';

interface WorkOrderFormValue {
  workOrderId: string;
  title: string;
  assetId: string;
  assetName: string;
  workType: string;
  category: string;
  priority: string;
  status: string;
  dueDate: string;
  plannedStart: string;
  plannedEnd: string;
  estimatedHours: number | null;
  assignedTechnician: string;
  team: string;
  vendor: string;
  site: string;
  building: string;
  area: string;
  department: string;
  source: string;
  sourceReference: string;
  description: string;
  notes: string;
}

/** 'YYYY-MM-DDTHH:mm' for datetime-local inputs (local time). */
function localDateTime(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value);
  if (isNaN(d.getTime())) return '';
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

const toIsoOrNull = (local: string) => (local ? new Date(local).toISOString() : null);

/** Create / edit work order drawer. Pass `workOrder` to edit, or `seed` to prefill a new one. */
@Component({
  selector: 'app-mnt-work-order-form',
  imports: [FormsModule, LucideAngularModule, AssetPicker],
  templateUrl: './work-order-form.html',
  styleUrl: './mnt-base.css'
})
export class WorkOrderForm implements OnInit {
  private readonly api = inject(MaintenanceApi);
  readonly workOrder = input<WorkOrder | null>(null);
  readonly seed = input<Partial<WorkOrder> | null>(null);
  readonly saved = output<WorkOrder>();
  readonly closed = output<void>();

  readonly X = X;
  readonly types = WORK_TYPES;
  readonly categories = WO_CATEGORIES;
  readonly priorities = PRIORITIES;
  readonly statuses = WO_STATUSES;
  readonly sources = WO_SOURCES;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  form!: WorkOrderFormValue;

  readonly people = () =>
    [...new Set(this.api.workOrders.items().map((w) => w.assignedTechnician).filter((t): t is string => !!t))].sort();
  readonly teams = () => [...new Set(this.api.workOrders.items().map((w) => w.team).filter((t): t is string => !!t))].sort();

  ngOnInit(): void {
    const w = { ...(this.seed() ?? {}), ...(this.workOrder() ?? {}) } as Partial<WorkOrder>;
    this.form = {
      workOrderId: w.workOrderId || nextCode('WO', this.api.workOrders.items().map((x) => x.workOrderId)),
      title: w.title ?? '',
      assetId: w.assetId ?? '',
      assetName: w.assetName ?? '',
      workType: w.workType ?? 'Corrective',
      category: w.category ?? 'Routine',
      priority: w.priority ?? 'Medium',
      status: w.status ?? 'Open',
      dueDate: day(w.dueDate ?? w.scheduledDate) || (this.workOrder() ? '' : today()),
      plannedStart: localDateTime(w.plannedStartDate),
      plannedEnd: localDateTime(w.plannedEndDate),
      estimatedHours: w.estimatedHours ?? null,
      assignedTechnician: w.assignedTechnician ?? '',
      team: w.team ?? '',
      vendor: w.vendor ?? '',
      site: w.site ?? '',
      building: w.building ?? '',
      area: w.area ?? '',
      department: w.department ?? '',
      source: w.source ?? 'Manual',
      sourceReference: w.sourceReference ?? '',
      description: w.description ?? '',
      notes: w.notes ?? ''
    };
  }

  onAsset(a: CatalogAsset): void {
    this.form.site = a.site;
    this.form.building = a.building;
    this.form.area = a.area;
    this.form.department = a.department;
    if (!this.form.title.trim()) this.form.title = `${a.name} - `;
  }

  close(): void {
    if (!this.saving()) this.closed.emit();
  }

  save(): void {
    const f = this.form;
    const problem = !f.workOrderId.trim() ? 'Work order # is required.'
      : !f.title.trim() ? 'Title is required.'
      : !f.assetId.trim() ? 'Asset tag is required.'
      : f.plannedStart && f.plannedEnd && f.plannedEnd < f.plannedStart ? 'Planned end cannot be before planned start.'
      : null;
    if (problem) return this.error.set(problem);

    const existing = this.workOrder();
    const due = f.dueDate ? `${f.dueDate}T00:00:00Z` : null;
    const body: Partial<WorkOrder> = {
      ...(existing ?? {}),
      workOrderId: f.workOrderId.trim(),
      title: f.title.trim(),
      assetId: f.assetId.trim(),
      assetName: f.assetName.trim(),
      workType: f.workType,
      category: f.category,
      priority: f.priority,
      // Naming a technician on an Open order schedules it.
      status: f.status === 'Open' && f.assignedTechnician.trim() && !existing ? 'Scheduled' : f.status,
      dueDate: due,
      scheduledDate: due,
      plannedStartDate: toIsoOrNull(f.plannedStart),
      plannedEndDate: toIsoOrNull(f.plannedEnd),
      estimatedHours: f.estimatedHours === null || (f.estimatedHours as unknown) === '' ? null : Number(f.estimatedHours),
      assignedTechnician: f.assignedTechnician.trim() || null,
      team: f.team.trim() || null,
      vendor: f.vendor.trim() || null,
      site: f.site.trim() || null,
      building: f.building.trim() || null,
      area: f.area.trim() || null,
      department: f.department.trim() || null,
      source: f.source,
      sourceReference: f.sourceReference.trim() || null,
      description: f.description.trim() || null,
      notes: f.notes.trim() || null
    };
    if (!existing) {
      const seed = this.seed();
      body.tasks = seed?.tasks ?? [];
    }

    this.saving.set(true);
    this.error.set(null);
    this.api.workOrders.save(body).subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.saved.emit(saved);
      },
      error: (e) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(e));
      }
    });
  }
}
