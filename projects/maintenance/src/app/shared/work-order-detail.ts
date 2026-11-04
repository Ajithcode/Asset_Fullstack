import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  LucideAngularModule,
  Building2,
  CalendarDays,
  ChevronDown,
  FileText,
  MapPin,
  Pencil,
  Trash2,
  UserRound,
  Wrench,
  X
} from 'lucide-angular';
import { findCatalogAsset } from './asset-catalog';
import { MaintenanceApi, apiErrorMessage } from './maintenance-api';
import { WO_STATUSES, WorkOrder, WorkOrderDocument, WorkOrderLabour, WorkOrderPart, WorkOrderTask } from './maintenance.models';
import { woStatus } from './derive';
import { downloadCsv, fmtDate, fmtDateTime, pillClass } from './mnt-utils';

type DetailTab = 'details' | 'tasks' | 'parts' | 'labour' | 'cost' | 'documents' | 'history';

/** Slide-over work order panel from the Work Orders mockup. Edits in the tabs save straight to the API. */
@Component({
  selector: 'app-mnt-work-order-detail',
  imports: [FormsModule, RouterLink, LucideAngularModule],
  templateUrl: './work-order-detail.html',
  styleUrls: ['./mnt-base.css', './work-order-detail.css'],
  host: { '(document:click)': 'menu.set(null)' }
})
export class WorkOrderDetail {
  private readonly api = inject(MaintenanceApi);
  readonly workOrder = input.required<WorkOrder>();
  readonly closed = output<void>();
  readonly edit = output<WorkOrder>();
  readonly duplicate = output<WorkOrder>();
  readonly notify = output<string>();

  readonly icons = { Building2, CalendarDays, ChevronDown, FileText, MapPin, Pencil, Trash2, UserRound, Wrench, X };
  readonly fmtDate = fmtDate;
  readonly fmtDateTime = fmtDateTime;
  readonly pillClass = pillClass;
  readonly statuses = WO_STATUSES;

  readonly tab = signal<DetailTab>('details');
  readonly menu = signal<'status' | 'more' | null>(null);
  readonly confirmDelete = signal(false);
  readonly busy = signal(false);

  newTask = '';
  newPart: WorkOrderPart = { partNumber: '', partName: '', quantity: 1, unitCost: 0 };
  newLabour: WorkOrderLabour = { technician: '', role: '', hours: 1, hourlyRate: 0 };
  newDoc: WorkOrderDocument = { name: '', url: '' };
  additionalCost: number | null = null;

  readonly status = computed(() => woStatus(this.workOrder()));
  readonly asset = computed(() => findCatalogAsset(this.workOrder().assetId));
  readonly location = computed(() => [this.workOrder().site, this.workOrder().building].filter((v) => !!v).join(' - ') || '—');
  readonly duration = computed(() => {
    const w = this.workOrder();
    if (w.actualStartDate && w.actualEndDate) {
      const h = (new Date(w.actualEndDate).getTime() - new Date(w.actualStartDate).getTime()) / 3_600_000;
      return `${+h.toFixed(1)} Hours (Actual)`;
    }
    return w.estimatedHours ? `${w.estimatedHours} Hours (Planned)` : '—';
  });
  readonly sourceText = computed(() => {
    const w = this.workOrder();
    const label = w.source === 'PM Schedule' ? 'Scheduled (PM Plan)' : w.source || 'Manual';
    return w.sourceReference ? `${label} · ${w.sourceReference}` : label;
  });

  toggleMenu(m: 'status' | 'more', ev: Event): void {
    ev.stopPropagation();
    this.menu.update((cur) => (cur === m ? null : m));
  }

  /** Saves a change to this work order (PUT with the full record). */
  private patch(changes: Partial<WorkOrder>, message: string): void {
    this.busy.set(true);
    this.api.workOrders.save({ ...this.workOrder(), ...changes }).subscribe({
      next: () => {
        this.busy.set(false);
        this.notify.emit(message);
      },
      error: (e) => {
        this.busy.set(false);
        this.notify.emit(apiErrorMessage(e));
      }
    });
  }

  setStatus(status: string): void {
    this.menu.set(null);
    this.patch({ status }, `${this.workOrder().workOrderId} set to ${status}.`);
  }

  // ----- tasks
  toggleTask(i: number): void {
    const tasks = this.workOrder().tasks.map((t, j): WorkOrderTask => (j === i ? { ...t, isDone: !t.isDone } : t));
    this.patch({ tasks }, 'Task updated.');
  }

  addTask(): void {
    const title = this.newTask.trim();
    if (!title) return;
    this.newTask = '';
    this.patch({ tasks: [...this.workOrder().tasks, { title, isDone: false, assignedTo: this.workOrder().assignedTechnician }] }, 'Task added.');
  }

  removeTask(i: number): void {
    this.patch({ tasks: this.workOrder().tasks.filter((_, j) => j !== i) }, 'Task removed.');
  }

  // ----- parts
  addPart(): void {
    const p = this.newPart;
    if (!p.partName.trim() || p.quantity <= 0) return this.notify.emit('Part name and a quantity above zero are required.');
    this.patch({ parts: [...this.workOrder().parts, { ...p, partName: p.partName.trim(), quantity: +p.quantity, unitCost: +p.unitCost || 0 }] }, 'Part added.');
    this.newPart = { partNumber: '', partName: '', quantity: 1, unitCost: 0 };
  }

  removePart(i: number): void {
    this.patch({ parts: this.workOrder().parts.filter((_, j) => j !== i) }, 'Part removed.');
  }

  // ----- labour
  addLabour(): void {
    const l = this.newLabour;
    if (!l.technician.trim() || l.hours <= 0) return this.notify.emit('Technician and hours above zero are required.');
    this.patch({ labour: [...this.workOrder().labour, { ...l, technician: l.technician.trim(), hours: +l.hours, hourlyRate: +l.hourlyRate || 0 }] }, 'Labour added.');
    this.newLabour = { technician: '', role: '', hours: 1, hourlyRate: 0 };
  }

  removeLabour(i: number): void {
    this.patch({ labour: this.workOrder().labour.filter((_, j) => j !== i) }, 'Labour removed.');
  }

  saveAdditionalCost(): void {
    const v = Number(this.additionalCost);
    if (this.additionalCost === null || isNaN(v) || v < 0) return;
    this.patch({ additionalCost: v }, 'Additional cost saved.');
    this.additionalCost = null;
  }

  // ----- documents
  addDocument(): void {
    const name = (this.newDoc.name ?? '').trim();
    if (!name) return this.notify.emit('Document name is required.');
    const ext = /\.([a-z0-9]+)$/i.exec(name)?.[1]?.toUpperCase() ?? 'LINK';
    const doc: WorkOrderDocument = { name, url: (this.newDoc.url ?? '').trim() || null, fileType: ext, uploadedAt: new Date().toISOString() };
    this.patch({ documents: [...this.workOrder().documents, doc] }, 'Document added.');
    this.newDoc = { name: '', url: '' };
  }

  removeDocument(i: number): void {
    this.patch({ documents: this.workOrder().documents.filter((_, j) => j !== i) }, 'Document removed.');
  }

  // ----- more actions
  exportOne(): void {
    this.menu.set(null);
    const w = this.workOrder();
    downloadCsv(`${w.workOrderId}.csv`, [
      { 'Work Order': w.workOrderId, Title: w.title, Asset: w.assetName, 'Asset Tag': w.assetId, Type: w.workType, Priority: w.priority, Status: this.status(), 'Due Date': (w.dueDate ?? '').slice(0, 10), 'Assigned To': w.assignedTechnician ?? '', Progress: `${w.progress}%`, 'Total Cost': w.totalCost }
    ]);
  }

  print(): void {
    this.menu.set(null);
    window.print();
  }

  remove(): void {
    const w = this.workOrder();
    this.api.workOrders.remove(w.id).subscribe({
      next: () => {
        this.notify.emit(`${w.workOrderId} deleted.`);
        this.closed.emit();
      },
      error: (e) => this.notify.emit(apiErrorMessage(e))
    });
  }
}
