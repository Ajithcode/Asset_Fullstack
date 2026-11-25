import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, Input, inject } from '@angular/core';
import { ExportFormat, ExportRequest, ExportService, PDF_LIMIT_MESSAGE } from './export.service';
import { pushToast } from 'shared-ui';

/**
 * "Export ▾" with exactly two options, CSV and PDF. Same look on every page:
 *   <app-export-menu [source]="exportRequest" />   // exportRequest = () => ExportRequest (read when the user clicks)
 * Shows a spinner and disables itself while exporting; failures and the PDF row limit show a toast.
 */
@Component({
  standalone: true,
  selector: 'app-export-menu',
  imports: [CommonModule],
  template: `
    <span class="xm">
      <button type="button" class="xm-btn" [class.xm-btn--sm]="small" aria-haspopup="menu" [attr.aria-expanded]="open" [disabled]="busy || disabled" (click)="toggle($event)">
        <span *ngIf="busy" class="xm-spin" aria-hidden="true"></span>
        <svg *ngIf="!busy" viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3v12"/><path d="m7 10 5 5 5-5"/><path d="M5 21h14"/></svg>
        <span>{{ busy ? 'Exporting…' : label }}</span>
        <svg *ngIf="!busy" viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>
      </button>
      <span *ngIf="open && !busy" class="xm-menu" role="menu">
        <button type="button" role="menuitem" (click)="pick('csv')">CSV</button>
        <button type="button" role="menuitem" (click)="pick('pdf')">PDF</button>
      </span>
    </span>
    <div *ngIf="toast" class="xm-toast" [class.xm-toast--bad]="toastBad" role="status" aria-live="polite">{{ toast }}</div>
  `,
  styles: `
    :host { display: inline-block; }
    .xm { position: relative; display: inline-block; }
    .xm-btn { display: inline-flex; align-items: center; gap: 6px; height: 36px; padding: 0 14px; border: 1px solid #d6d9e2; border-radius: 8px; background: #fff; color: #1f2937; font: inherit; font-size: 0.812rem; font-weight: 600; cursor: pointer; }
    .xm-btn--sm { height: 30px; padding: 0 10px; font-size: 0.75rem; }
    .xm-btn:hover:not(:disabled) { background: #f5f3ff; border-color: #c9c3f6; }
    .xm-btn:disabled { opacity: .65; cursor: default; }
    .xm-menu { position: absolute; right: 0; top: calc(100% + 4px); z-index: 2500; min-width: 120px; display: flex; flex-direction: column; padding: 4px; border: 1px solid #e5e7eb; border-radius: 10px; background: #fff; box-shadow: 0 12px 28px rgba(15, 23, 42, .16); }
    .xm-menu button { text-align: left; padding: 8px 12px; border: 0; border-radius: 7px; background: none; font: inherit; font-size: 0.812rem; color: #1f2937; cursor: pointer; }
    .xm-menu button:hover { background: #f5f3ff; color: #5b21b6; }
    .xm-spin { width: 14px; height: 14px; border: 2px solid #c9c3f6; border-top-color: #5b21b6; border-radius: 50%; animation: xm-r .7s linear infinite; }
    @keyframes xm-r { to { transform: rotate(360deg); } }
    .xm-toast { position: fixed; right: 20px; bottom: 24px; z-index: 4000; max-width: 380px; padding: 11px 16px; border-radius: 10px; background: #1f2937; color: #fff; font-size: 0.812rem; box-shadow: 0 12px 28px rgba(15, 23, 42, .3); }
    .xm-toast--bad { background: #b91c1c; }
  `,
})
export class ExportMenu {
  private readonly exporter = inject(ExportService);
  private readonly host = inject(ElementRef<HTMLElement>);
  /** Builds the export when the user picks a format (reads the page's current filters and sort). */
  @Input({ required: true }) source!: () => ExportRequest;
  @Input() label = 'Export';
  @Input() small = false;
  @Input() disabled = false;
  open = false;
  busy = false;
  toast = '';
  toastBad = false;
  private toastTimer?: ReturnType<typeof setTimeout>;

  toggle(e: Event): void {
    e.stopPropagation();
    this.open = !this.open;
  }

  @HostListener('document:click', ['$event'])
  onDoc(e: Event): void {
    if (this.open && !this.host.nativeElement.contains(e.target as Node)) this.open = false;
  }

  @HostListener('document:keydown.escape')
  onEsc(): void {
    this.open = false;
  }

  async pick(format: ExportFormat): Promise<void> {
    this.open = false;
    if (this.busy) return;
    this.busy = true;
    try {
      const res = await this.exporter.run(format, this.source());
      if (format === 'pdf' && res.truncated) this.show(PDF_LIMIT_MESSAGE, false);
      else if (!res.rows) this.show('Nothing to export: no rows match the current filters.', false);
    } catch (e) {
      const msg = e instanceof Error && e.message && !/^Http failure/.test(e.message) ? e.message : '';
      this.show(`Export failed${msg ? ': ' + msg : '. Please try again.'}`, true);
    } finally {
      this.busy = false;
    }
  }

  private show(text: string, bad: boolean): void {
    // shared toast: durations, hover-pause and close button live in shared-ui (toast.ts)
    pushToast(text, bad);
  }
}
