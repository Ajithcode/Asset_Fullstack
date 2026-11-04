import { AfterViewInit, Component, ElementRef, output, viewChild } from '@angular/core';

/** Freehand signature pad (mouse, pen and touch through pointer events); emits a PNG data URL. */
@Component({
  selector: 'app-insp-signature-pad',
  standalone: true,
  template: `
    <canvas #pad
      (pointerdown)="start($event)" (pointermove)="draw($event)" (pointerup)="end()" (pointerleave)="end()" (pointercancel)="end()"
      (touchstart)="$event.preventDefault()" aria-label="Signature drawing area"></canvas>
    <div class="row">
      <span class="hint">{{ empty ? 'Sign above with mouse, pen or finger' : 'Looks good? Apply it as the specimen.' }}</span>
      <button type="button" class="b" (click)="clear()" [disabled]="empty">Clear</button>
      <button type="button" class="b b-p" (click)="apply()" [disabled]="empty">Use Signature</button>
    </div>
  `,
  styles: [
    `:host { display: flex; flex-direction: column; gap: 8px; width: 100%; }
     canvas { width: 100%; height: 130px; border: 1px dashed #cdbcfa; border-radius: 8px; background: #fff; touch-action: none; cursor: crosshair; }
     .row { display: flex; align-items: center; gap: 8px; }
     .hint { flex: 1; font-size: 12px; color: #6b6880; }
     .b { height: 30px; padding: 0 12px; border: 1px solid #d6d3e0; border-radius: 6px; background: #fff; font: inherit; font-size: 12.5px; font-weight: 600; cursor: pointer; }
     .b:disabled { opacity: 0.5; cursor: default; }
     .b-p { background: #5b21e6; border-color: #5b21e6; color: #fff; }`
  ]
})
export class SignaturePad implements AfterViewInit {
  readonly signed = output<string>();
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('pad');
  private ctx: CanvasRenderingContext2D | null = null;
  private drawing = false;
  empty = true;

  ngAfterViewInit(): void {
    const c = this.canvas().nativeElement;
    const ratio = window.devicePixelRatio || 1;
    c.width = Math.max(1, c.offsetWidth) * ratio;
    c.height = Math.max(1, c.offsetHeight) * ratio;
    this.ctx = c.getContext('2d');
    if (!this.ctx) return;
    this.ctx.scale(ratio, ratio);
    this.ctx.lineWidth = 2.4;
    this.ctx.lineCap = 'round';
    this.ctx.lineJoin = 'round';
    this.ctx.strokeStyle = '#1b1640';
  }

  private point(e: PointerEvent): [number, number] {
    const r = this.canvas().nativeElement.getBoundingClientRect();
    return [e.clientX - r.left, e.clientY - r.top];
  }

  start(e: PointerEvent): void {
    if (!this.ctx) return;
    e.preventDefault();
    this.canvas().nativeElement.setPointerCapture?.(e.pointerId);
    this.drawing = true;
    const [x, y] = this.point(e);
    this.ctx.beginPath();
    this.ctx.moveTo(x, y);
  }

  draw(e: PointerEvent): void {
    if (!this.drawing || !this.ctx) return;
    const [x, y] = this.point(e);
    this.ctx.lineTo(x, y);
    this.ctx.stroke();
    this.empty = false;
  }

  end(): void {
    this.drawing = false;
  }

  clear(): void {
    const c = this.canvas().nativeElement;
    this.ctx?.clearRect(0, 0, c.width, c.height);
    this.empty = true;
  }

  apply(): void {
    if (!this.empty) this.signed.emit(this.canvas().nativeElement.toDataURL('image/png'));
  }
}
