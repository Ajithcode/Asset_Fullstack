import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  LucideAngularModule,
  ArrowRight,
  CircleAlert,
  CircleCheck,
  Download,
  FileText,
  History,
  Network,
  Settings,
  ShieldAlert,
  Wrench,
  CalendarDays,
  ChevronDown,
  ChevronRight,
  Files,
} from 'lucide-angular';
import type { AssetComponent } from '../asset-components';
import { buildMockServiceHistory, type ServiceRecord } from '../component-service-history/component-service-history';
import { buildMockDocuments, type ComponentDocument } from '../component-documents/component-documents';
import { formatSize, saveBlob } from '../file-preview/file-preview';

type ReplacementStatus = 'Overdue' | 'Due Soon' | 'On Track';
type HealthLabel = 'Good' | 'Fair' | 'Poor';

interface HealthSlice {
  key: 'operational' | 'maintenance' | 'failed' | 'inactive';
  label: string;
  count: number;
}

interface ActivityItem {
  component: AssetComponent;
  record: ServiceRecord;
}

interface DocumentItem {
  component: AssetComponent;
  doc: ComponentDocument;
}

/** Replacement due within this many days counts as "Due Soon". */
const DUE_SOON_DAYS = 60;
const DAY_MS = 86_400_000;

/** Status baseline for the health score, before life used is taken into account. */
const STATUS_SCORE: Record<string, number> = {
  Active: 100,
  'Under Maintenance': 60,
  Inactive: 50,
  'Needs Replacement': 20,
};

function todayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** Asset-wide summary of the component tree: status counts, health, replacements, recent work and documents. */
@Component({
  selector: 'app-component-overview',
  standalone: true,
  imports: [CommonModule, LucideAngularModule],
  templateUrl: './component-overview.html',
  // Reuses the page's card, table, pill and tree styles.
  styleUrls: ['../asset-components.css', './component-overview.css'],
})
export class ComponentOverview implements OnChanges {
  readonly icons = {
    ArrowRight, CalendarDays, ChevronDown, ChevronRight, CircleAlert, CircleCheck, Download, FileText, Files, History,
    Network, Settings, ShieldAlert, Wrench,
  };
  readonly formatSize = formatSize;

  @Input({ required: true }) components: AssetComponent[] = [];
  @Input({ required: true }) assetName = '';
  @Input({ required: true }) assetTag = '';
  /** Icon lookup shared with the parent page, so the tree matches the Components tab. */
  @Input({ required: true }) iconFor!: (c: AssetComponent) => typeof Settings;

  /** Open a component on the Components tab. */
  @Output() openComponent = new EventEmitter<AssetComponent>();
  /** Open a component-level tab (service, documents, …) focused on a component. */
  @Output() openTab = new EventEmitter<{ tab: 'service' | 'documents'; component: AssetComponent | null }>();

  expanded = new Set<number>();
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['components']) return;
    // Open the first assembly, as in the design.
    this.expanded.clear();
    const first = this.components.find((c) => c.children.length);
    if (first) this.expanded.add(first.id);
    // The activity feed and document list read the per-component sample data, so make sure it exists.
    for (const c of this.all) {
      c.serviceHistory ??= buildMockServiceHistory(c);
      c.documents ??= buildMockDocuments(c);
    }
  }

  /** Every component in the tree, depth first. */
  get all(): AssetComponent[] {
    const out: AssetComponent[] = [];
    const visit = (nodes: AssetComponent[]) => nodes.forEach((n) => (out.push(n), visit(n.children)));
    visit(this.components);
    return out;
  }

  countDescendants(c: AssetComponent): number {
    return c.children.reduce((sum, ch) => sum + 1 + this.countDescendants(ch), 0);
  }

  toggle(c: AssetComponent, event: Event): void {
    event.stopPropagation();
    if (this.expanded.has(c.id)) this.expanded.delete(c.id);
    else this.expanded.add(c.id);
  }

  // ===== KPIs & health =====

  countByStatus(status: string): number {
    return this.all.filter((c) => c.status === status).length;
  }

  get slices(): HealthSlice[] {
    const slices: HealthSlice[] = [
      { key: 'operational', label: 'Operational', count: this.countByStatus('Active') },
      { key: 'maintenance', label: 'Under Maintenance', count: this.countByStatus('Under Maintenance') },
      { key: 'failed', label: 'Failed / Needs Replacement', count: this.countByStatus('Needs Replacement') },
    ];
    const inactive = this.countByStatus('Inactive');
    if (inactive) slices.push({ key: 'inactive', label: 'Inactive', count: inactive });
    return slices;
  }

  percent(count: number): number {
    return this.all.length ? Math.round((count / this.all.length) * 100) : 0;
  }

  /**
   * Per-component score: a status baseline (Active 100 … Needs Replacement 20), reduced by up to half
   * as tracked life is used up. The asset figure is the average across components.
   */
  score(c: AssetComponent): number {
    const base = STATUS_SCORE[c.status] ?? 50;
    if (!c.lifeTotal) return base;
    const used = Math.min(1, c.lifeUsed / c.lifeTotal);
    return Math.round(base * (1 - used / 2));
  }

  get averageHealth(): number {
    const all = this.all;
    return all.length ? Math.round(all.reduce((sum, c) => sum + this.score(c), 0) / all.length) : 0;
  }

  get healthLabel(): HealthLabel {
    const s = this.averageHealth;
    return s >= 75 ? 'Good' : s >= 50 ? 'Fair' : 'Poor';
  }

  /** SVG donut segments as stroke-dasharray arcs on a circle of circumference 100, with 1-unit gaps. */
  get arcs(): { key: HealthSlice['key']; label: string; count: number; dash: string; offset: number }[] {
    const total = this.all.length;
    const visible = this.slices.filter((s) => s.count > 0);
    const gap = visible.length > 1 ? 1 : 0;
    let start = 0;
    return visible.map((s) => {
      const len = (s.count / total) * 100;
      const arc = { key: s.key, label: s.label, count: s.count, dash: `${Math.max(0, len - gap)} ${100 - Math.max(0, len - gap)}`, offset: 25 - start };
      start += len;
      return arc;
    });
  }

  // ===== Replacement schedule =====

  replacementStatus(iso: string): ReplacementStatus {
    const days = Math.round((new Date(iso + 'T00:00').getTime() - new Date(todayIso() + 'T00:00').getTime()) / DAY_MS);
    if (days < 0) return 'Overdue';
    return days <= DUE_SOON_DAYS ? 'Due Soon' : 'On Track';
  }

  get replacements(): AssetComponent[] {
    return this.all.filter((c) => c.expectedReplacement).sort((a, b) => a.expectedReplacement.localeCompare(b.expectedReplacement));
  }

  statusClass(status: string): string {
    return 'rs--' + status.toLowerCase().replace(/\s+/g, '-');
  }

  // ===== Activity & documents =====

  get activity(): ActivityItem[] {
    return this.all
      .flatMap((component) => (component.serviceHistory ?? []).map((record) => ({ component, record })))
      .sort((a, b) => b.record.date.localeCompare(a.record.date))
      .slice(0, 4);
  }

  activityDot(r: ServiceRecord): string {
    if (r.status !== 'Completed') return 'dot--muted';
    return r.type === 'Corrective' ? 'dot--bad' : 'dot--good';
  }

  get recentDocuments(): DocumentItem[] {
    return this.all
      .flatMap((component) => (component.documents ?? []).filter((d) => d.status === 'Active').map((doc) => ({ component, doc })))
      .sort((a, b) => b.doc.uploadedAt.localeCompare(a.doc.uploadedAt))
      .slice(0, 3);
  }

  download(item: DocumentItem): void {
    if (!item.doc.file) {
      this.showToast('This is a sample document with no file attached. Upload a file on the Documents tab to download it.');
      return;
    }
    saveBlob(item.doc.file, item.doc.fileName);
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3200);
  }
}
