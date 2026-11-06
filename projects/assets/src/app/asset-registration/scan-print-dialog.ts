import { Component, ElementRef, EventEmitter, HostListener, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, ArrowLeft, Keyboard, Printer, ScanBarcode, Wifi, X } from 'lucide-angular';

export type ScanMode = 'barcode' | 'rfid' | 'normal';
type Step = 'choose' | 'input' | 'print';

/** What the dialog hands back: the identifiers (possibly changed) and how the label was printed. */
export interface ScanPrintResult {
  mode: ScanMode;
  assetTag: string;
  barcode: string;
  rfidTag: string;
  printer: string;
  copies: number;
}

interface PrinterOption {
  id: string;
  label: string;
  /** Opens the browser's print dialog instead of sending to a label printer. */
  browser?: boolean;
}

/**
 * Quick Actions → Submit: choose Barcode scanner, RFID reader or Normal (typed) entry, confirm the identifiers
 * (filled from the asset form), then preview and print the asset label.
 * DEMO MODE: no scanner, reader or label printer driver is connected; a scanner in keyboard mode types into the
 * focused box, and label printers are simulated (the browser printer really prints).
 */
@Component({
  selector: 'app-scan-print-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  template: `
    <div class="sp-overlay" (click)="closed.emit()">
      <div class="sp-modal" role="dialog" aria-modal="true" aria-labelledby="sp-title" (click)="$event.stopPropagation()">
        <div class="sp-head">
          <button *ngIf="step !== 'choose'" type="button" class="sp-icon-btn" (click)="back()" aria-label="Back"><lucide-icon [img]="icons.ArrowLeft" [size]="18"></lucide-icon></button>
          <h2 id="sp-title">{{ title }}</h2>
          <span class="sp-steps" aria-hidden="true"><i *ngFor="let s of steps" [class.on]="s === step"></i></span>
          <button type="button" class="sp-icon-btn" (click)="closed.emit()" aria-label="Close"><lucide-icon [img]="icons.X" [size]="18"></lucide-icon></button>
        </div>

        <!-- 1. Choose the scanner -->
        <div *ngIf="step === 'choose'" class="sp-body">
          <p class="sp-note">Choose the scanner type for <strong>{{ assetName || 'this asset' }}</strong>?</p>
          <div class="sp-options" role="radiogroup" aria-label="Scanner">
            <button *ngFor="let o of modes" type="button" role="radio" class="sp-option" [attr.aria-checked]="mode === o.key" (click)="choose(o.key)">
              <span class="sp-option-ic"><lucide-icon [img]="o.icon" [size]="24"></lucide-icon></span>
              <strong>{{ o.label }}</strong>
              <span>{{ o.hint }}</span>
            </button>
          </div>
        </div>

        <!-- 2. Input for the chosen scanner -->
        <form *ngIf="step === 'input'" class="sp-body" (ngSubmit)="toPrint()">
          <div class="sp-asset"><strong>{{ assetName || 'Unnamed asset' }}</strong><span>{{ assetTag || 'No tag yet' }}</span></div>

          <ng-container *ngIf="mode === 'barcode'">
            <p class="sp-status"><span class="sp-dot"></span>Scanner ready. Scan the label, or type the code. The scanner types into the box below.</p>
            <label class="sp-field"><span>Barcode / QR Code <em>*</em></span>
              <input class="sp-control sp-big" name="barcode" [(ngModel)]="barcode" autocomplete="off" placeholder="Scan now..." /></label>
            <label class="sp-field"><span>Symbology</span>
              <select class="sp-control" name="symbology" [(ngModel)]="symbology"><option *ngFor="let s of symbologies" [value]="s">{{ s }}</option></select></label>
          </ng-container>

          <ng-container *ngIf="mode === 'rfid'">
            <p class="sp-status"><span class="sp-dot"></span>Reader ready. Hold the tag near the reader, or type the EPC.</p>
            <label class="sp-field"><span>RFID Tag ID (EPC) <em>*</em></span>
              <input class="sp-control sp-big sp-mono" name="rfid" [(ngModel)]="rfidTag" autocomplete="off" placeholder="Waiting for tag..." /></label>
            <div class="sp-grid">
              <label class="sp-field"><span>Tag Type</span>
                <select class="sp-control" name="tagType" [(ngModel)]="tagType"><option *ngFor="let t of tagTypes" [value]="t">{{ t }}</option></select></label>
              <label class="sp-field"><span>Read Mode</span>
                <select class="sp-control" name="readMode" [(ngModel)]="readMode"><option value="Single">Single read</option><option value="Continuous">Continuous</option></select></label>
            </div>
          </ng-container>

          <ng-container *ngIf="mode === 'normal'">
            <p class="sp-status sp-status--plain">Type or correct the identifiers. Leave a field empty if the asset doesn't have one.</p>
            <label class="sp-field"><span>Asset Tag / Asset ID <em>*</em></span><input class="sp-control" name="tag" [(ngModel)]="tag" autocomplete="off" /></label>
            <label class="sp-field"><span>Barcode / QR Code</span><input class="sp-control" name="barcodeN" [(ngModel)]="barcode" autocomplete="off" /></label>
            <label class="sp-field"><span>RFID Tag ID</span><input class="sp-control sp-mono" name="rfidN" [(ngModel)]="rfidTag" autocomplete="off" /></label>
          </ng-container>

          <p *ngIf="error" class="sp-error" role="alert">{{ error }}</p>
          <div class="sp-foot">
            <button type="button" class="sp-btn sp-btn--ghost" (click)="back()">Back</button>
            <button type="submit" class="sp-btn sp-btn--primary">Next: Print Label</button>
          </div>
        </form>

        <!-- 3. Label preview and printer -->
        <div *ngIf="step === 'print'" class="sp-body">
          <div class="sp-print">
            <div class="sp-label" [class.sp-label--wide]="size === '100 × 50 mm'" aria-label="Label preview">
              <svg class="sp-qr" [attr.viewBox]="'-1 -1 ' + (qrSize + 2) + ' ' + (qrSize + 2)" shape-rendering="crispEdges" aria-hidden="true">
                <rect x="-1" y="-1" [attr.width]="qrSize + 2" [attr.height]="qrSize + 2" fill="#fff"></rect>
                <path [attr.d]="qrPath" fill="#0f172a"></path>
              </svg>
              <div class="sp-label-text">
                <strong>{{ assetName || 'Unnamed asset' }}</strong>
                <span class="sp-label-tag">{{ tag }}</span>
                <span *ngIf="mode !== 'rfid' && barcode">{{ barcode }}</span>
                <span *ngIf="mode !== 'barcode' && rfidTag" class="sp-mono">RFID {{ rfidTag }}</span>
                <span *ngIf="location">{{ location }}</span>
              </div>
            </div>
            <p class="sp-hint">Preview only. The QR pattern is the demo code shown on the form.</p>
          </div>
          <div class="sp-grid">
            <label class="sp-field sp-span"><span>Printer</span>
              <select class="sp-control" [(ngModel)]="printer"><option *ngFor="let p of printers" [value]="p.id">{{ p.label }}</option></select></label>
            <label class="sp-field"><span>Label Size</span>
              <select class="sp-control" [(ngModel)]="size"><option *ngFor="let s of sizes" [value]="s">{{ s }}</option></select></label>
            <label class="sp-field"><span>Copies</span><input class="sp-control" type="number" min="1" max="50" [(ngModel)]="copies" /></label>
          </div>
          <p *ngIf="error" class="sp-error" role="alert">{{ error }}</p>
          <div class="sp-foot">
            <button type="button" class="sp-btn sp-btn--ghost" (click)="back()">Back</button>
            <button type="button" class="sp-btn sp-btn--primary" (click)="print()"><lucide-icon [img]="icons.Printer" [size]="16"></lucide-icon>Print Label</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .sp-overlay { position: fixed; inset: 0; z-index: 10001; display: flex; align-items: center; justify-content: center; padding: 16px; background: rgba(17, 24, 39, 0.45); }
    .sp-modal { width: 100%; max-width: 560px; max-height: calc(100vh - 32px); overflow-y: auto; border-radius: 12px; background: #fff; box-shadow: 0 24px 60px rgba(15, 23, 42, 0.25); font-size: 13.5px; color: #111827; }
    .sp-head { display: flex; align-items: center; gap: 10px; padding: 14px 18px; border-bottom: 1px solid #e5e7eb; }
    .sp-head h2 { flex: 1; margin: 0; font-size: 17px; font-weight: 800; }
    .sp-steps { display: flex; gap: 5px; }
    .sp-steps i { width: 8px; height: 8px; border-radius: 50%; background: #e5e7eb; }
    .sp-steps i.on { background: #4f2bd8; }
    .sp-icon-btn { display: inline-flex; align-items: center; justify-content: center; width: 30px; height: 30px; border: 0; border-radius: 6px; background: none; color: #374151; cursor: pointer; }
    .sp-icon-btn:hover { background: #f3f4f6; }
    .sp-body { display: flex; flex-direction: column; gap: 12px; padding: 16px 18px 18px; }
    .sp-note, .sp-hint { margin: 0; color: #4b5563; }
    .sp-hint { font-size: 12px; text-align: center; }
    .sp-options { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; }
    .sp-option { display: flex; flex-direction: column; align-items: center; gap: 6px; padding: 16px 10px; border: 1px solid #c9bdf7; border-radius: 10px; background: #fff; font: inherit; text-align: center; color: #111827; cursor: pointer; }
    .sp-option:hover, .sp-option[aria-checked='true'] { border-color: #4f2bd8; background: #f3f0ff; }
    .sp-option span:last-child { font-size: 12px; color: #6b7280; }
    .sp-option-ic { display: inline-flex; align-items: center; justify-content: center; width: 46px; height: 46px; border-radius: 10px; background: #f3f0ff; color: #4f2bd8; }
    .sp-asset { display: flex; justify-content: space-between; gap: 10px; padding: 10px 12px; border-radius: 8px; background: #f7f7fa; }
    .sp-asset span { color: #4f2bd8; font-weight: 700; }
    .sp-status { display: flex; align-items: center; gap: 8px; margin: 0; padding: 9px 12px; border: 1px solid #bbe5c8; border-radius: 8px; background: #effaf2; color: #166534; }
    .sp-status--plain { border-color: #e5e7eb; background: #fafafb; color: #4b5563; }
    .sp-dot { flex: 0 0 auto; width: 9px; height: 9px; border-radius: 50%; background: #16a34a; animation: sp-pulse 1.4s ease-in-out infinite; }
    @keyframes sp-pulse { 50% { opacity: 0.3; } }
    @media (prefers-reduced-motion: reduce) { .sp-dot { animation: none; } }
    .sp-field { display: flex; flex-direction: column; gap: 6px; min-width: 0; }
    .sp-field > span { font-size: 13px; color: #374151; }
    .sp-field em { font-style: normal; color: #dc2626; }
    .sp-control { width: 100%; height: 38px; box-sizing: border-box; padding: 0 12px; border: 1px solid #d1d5db; border-radius: 6px; background: #fff; font: inherit; color: #111827; outline: none; }
    .sp-control:focus { border-color: #4f2bd8; box-shadow: 0 0 0 3px rgba(79, 43, 216, 0.12); }
    .sp-big { height: 46px; font-size: 16px; font-weight: 600; }
    .sp-mono { font-family: ui-monospace, Consolas, monospace; letter-spacing: 0.02em; }
    .sp-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
    .sp-span { grid-column: 1 / -1; }
    .sp-error { margin: 0; color: #dc2626; }
    .sp-foot { display: flex; justify-content: flex-end; gap: 10px; padding-top: 4px; }
    .sp-btn { display: inline-flex; align-items: center; gap: 8px; height: 38px; padding: 0 16px; border: 1px solid transparent; border-radius: 7px; font: inherit; font-weight: 600; cursor: pointer; }
    .sp-btn--primary { background: #4f2bd8; color: #fff; }
    .sp-btn--primary:hover { background: #4122b8; }
    .sp-btn--ghost { border-color: #c9bdf7; background: #fff; color: #4f2bd8; }
    .sp-print { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 16px; border-radius: 10px; background: #f3f4f6; }
    .sp-label { display: flex; align-items: center; gap: 12px; width: 260px; padding: 12px; border: 1px dashed #9ca3af; border-radius: 6px; background: #fff; }
    .sp-label--wide { width: 360px; max-width: 100%; }
    .sp-qr { flex: 0 0 auto; width: 76px; height: 76px; }
    .sp-label-text { display: flex; flex-direction: column; gap: 2px; min-width: 0; font-size: 11.5px; color: #374151; overflow-wrap: anywhere; }
    .sp-label-text strong { font-size: 13px; color: #111827; }
    .sp-label-tag { font-size: 15px; font-weight: 800; color: #111827; }
    @media (max-width: 520px) { .sp-options, .sp-grid { grid-template-columns: minmax(0, 1fr); } }
  `],
})
export class ScanPrintDialog implements OnInit {
  readonly icons = { ArrowLeft, Keyboard, Printer, ScanBarcode, Wifi, X };
  /** Scanner types: No (no scanner, typed by hand), Non (non-contact RFID reader), Normal (normal barcode / QR scanner). */
  readonly modes: { key: ScanMode; label: string; hint: string; icon: typeof ScanBarcode }[] = [
    { key: 'normal', label: 'No', hint: 'No scanner, type it in', icon: Keyboard },
    { key: 'rfid', label: 'Non', hint: 'Non-contact RFID reader', icon: Wifi },
    { key: 'barcode', label: 'Normal', hint: 'Normal barcode / QR scanner', icon: ScanBarcode },
  ];
  readonly steps: Step[] = ['choose', 'input', 'print'];
  readonly symbologies = ['Code 128', 'QR Code', 'Data Matrix', 'EAN-13'];
  readonly tagTypes = ['UHF (EPC Gen2)', 'HF (ISO 15693)', 'NFC'];
  readonly printers: PrinterOption[] = [
    { id: 'zebra-zd421', label: 'Zebra ZD421 · Stores (label printer)' },
    { id: 'brother-ql820', label: 'Brother QL-820NWB · Facilities office' },
    { id: 'zebra-zt411-rfid', label: 'Zebra ZT411 RFID · Warehouse (encodes RFID)' },
    { id: 'browser', label: 'This computer (browser print)', browser: true },
  ];
  readonly sizes = ['50 × 25 mm', '62 × 29 mm', '100 × 50 mm'];

  @Input() assetName = '';
  @Input() assetTag = '';
  @Input() initialBarcode = '';
  @Input() initialRfid = '';
  @Input() location = '';
  @Input() qrPath = '';
  @Input() qrSize = 25;
  @Output() completed = new EventEmitter<ScanPrintResult>();
  @Output() closed = new EventEmitter<void>();

  step: Step = 'choose';
  mode: ScanMode = 'normal';
  tag = '';
  barcode = '';
  rfidTag = '';
  symbology = 'Code 128';
  tagType = 'UHF (EPC Gen2)';
  readMode = 'Single';
  printer = 'zebra-zd421';
  size = '50 × 25 mm';
  copies = 1;
  error = '';

  private readonly host = inject(ElementRef<HTMLElement>);

  ngOnInit(): void {
    // Start from the asset's current data
    this.tag = this.assetTag;
    this.barcode = this.initialBarcode;
    this.rfidTag = this.initialRfid;
  }

  get title(): string {
    if (this.step === 'choose') return 'Choose Scanner';
    if (this.step === 'print') return 'Print Label';
    return `Scanner: ${this.modes.find((m) => m.key === this.mode)!.label}`;
  }

  choose(mode: ScanMode): void {
    this.mode = mode;
    this.error = '';
    this.step = 'input';
    // RFID labels go to the encoding printer by default
    this.printer = mode === 'rfid' ? 'zebra-zt411-rfid' : 'zebra-zd421';
    setTimeout(() => (this.host.nativeElement.querySelector('.sp-big, .sp-control') as HTMLInputElement | null)?.select());
  }

  back(): void {
    this.error = '';
    this.step = this.step === 'print' ? 'input' : 'choose';
  }

  toPrint(): void {
    this.barcode = this.barcode.trim();
    this.rfidTag = this.rfidTag.trim().toUpperCase().replace(/\s+/g, '');
    this.tag = this.tag.trim();
    if (this.mode === 'barcode' && !this.barcode) return void (this.error = 'Scan or enter the barcode.');
    if (this.mode === 'rfid') {
      if (!this.rfidTag) return void (this.error = 'Read or enter the RFID tag.');
      if (!/^[0-9A-F]+$/.test(this.rfidTag) || this.rfidTag.length % 4 !== 0) return void (this.error = 'An EPC is hexadecimal (0–9, A–F) in groups of 4, e.g. 24 characters.');
    }
    if (this.mode === 'normal' && !this.tag) return void (this.error = 'Enter the asset tag.');
    this.error = '';
    this.step = 'print';
  }

  print(): void {
    const copies = Math.floor(Number(this.copies));
    if (!(copies >= 1 && copies <= 50)) return void (this.error = 'Copies must be between 1 and 50.');
    const printer = this.printers.find((p) => p.id === this.printer)!;
    if (printer.browser && !this.browserPrint(copies)) return void (this.error = 'The print window was blocked. Allow pop-ups for this site and try again.');
    this.completed.emit({ mode: this.mode, assetTag: this.tag, barcode: this.barcode, rfidTag: this.rfidTag, printer: printer.label, copies });
  }

  /** Prints the label from a separate window so only the label is printed. */
  private browserPrint(copies: number): boolean {
    const svg = this.host.nativeElement.querySelector('.sp-label')?.outerHTML ?? '';
    const win = window.open('', '_blank', 'width=480,height=600');
    if (!win) return false;
    const css = `body{margin:0;font-family:Arial,sans-serif}.sp-label{display:flex;align-items:center;gap:12px;width:260px;padding:12px;margin:8px;border:1px solid #999;break-inside:avoid}.sp-qr{width:76px;height:76px}.sp-label-text{display:flex;flex-direction:column;gap:2px;font-size:11px}.sp-label-text strong{font-size:13px}.sp-label-tag{font-size:15px;font-weight:800}`;
    win.document.write(`<!doctype html><html><head><title>${this.esc(this.tag)} label</title><style>${css}</style></head><body>${svg.repeat(copies)}</body></html>`);
    win.document.close();
    win.focus();
    win.print();
    return true;
  }

  private esc(s: string): string {
    return s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
