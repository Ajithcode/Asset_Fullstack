import { Component, computed, input } from '@angular/core';

export interface BarSeries {
  name: string;
  color: string;
  values: number[];
}

/** Monthly stacked bars with an optional line series and a legend underneath. */
@Component({
  selector: 'app-mnt-bars',
  template: `
    <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" class="bars" role="img" [attr.aria-label]="'Chart of ' + names()">
      @for (t of ticks(); track t.v) {
        <line [attr.x1]="padL" [attr.x2]="W - 6" [attr.y1]="t.y" [attr.y2]="t.y" class="grid" />
        <text [attr.x]="padL - 8" [attr.y]="t.y + 3" text-anchor="end" class="axis">{{ t.v }}</text>
      }
      @for (col of columns(); track col.i) {
        @for (seg of col.segs; track seg.name) {
          <rect [attr.x]="col.x" [attr.y]="seg.y" [attr.width]="barW()" [attr.height]="seg.h" [attr.fill]="seg.color" [attr.opacity]="soft() ? 0.45 : 1">
            <title>{{ col.label }} · {{ seg.name }}: {{ seg.v }}</title>
          </rect>
        }
        <text [attr.x]="col.x + barW() / 2" [attr.y]="H - 6" text-anchor="middle" class="axis">{{ col.label }}</text>
      }
      @if (linePath(); as path) {
        <path [attr.d]="path" fill="none" [attr.stroke]="line()!.color" stroke-width="1.6" />
        @for (p of linePoints(); track p.i) {
          <circle [attr.cx]="p.x" [attr.cy]="p.y" r="3" [attr.fill]="line()!.color"><title>{{ p.label }} · {{ line()!.name }}: {{ p.v }}</title></circle>
        }
      }
    </svg>
    <div class="legend">
      @for (s of series(); track s.name) { <span><i [style.background]="s.color" [style.opacity]="soft() ? 0.45 : 1"></i>{{ s.name }}</span> }
      @if (line(); as l) { <span><i class="ln" [style.background]="l.color"></i>{{ l.name }}</span> }
    </div>
  `,
  styles: `
    :host { display: block; }
    .bars { width: 100%; height: auto; display: block; }
    .grid { stroke: #eceaf3; stroke-width: 1; }
    .axis { font-size: 10px; fill: #4b4860; }
    .legend { display: flex; flex-wrap: wrap; justify-content: center; gap: 18px; margin-top: 6px; font-size: 12px; color: #2c2a3d; }
    .legend span { display: inline-flex; align-items: center; gap: 6px; }
    .legend i { width: 10px; height: 10px; border-radius: 50%; }
    .legend i.ln { height: 3px; width: 16px; border-radius: 2px; }
  `
})
export class BarChart {
  readonly labels = input.required<string[]>();
  readonly series = input.required<BarSeries[]>();
  /** Optional line drawn over the bars (e.g. actual failures). */
  readonly line = input<BarSeries | null>(null);
  /** Light bars (used when a line carries the comparison). */
  readonly soft = input(false);

  readonly W = 460;
  readonly H = 170;
  readonly padL = 30;
  private readonly padT = 8;
  private readonly padB = 22;

  readonly names = computed(() => this.series().map((s) => s.name).join(', '));

  private readonly max = computed(() => {
    const n = this.labels().length;
    let max = 0;
    for (let i = 0; i < n; i++) max = Math.max(max, this.series().reduce((sum, s) => sum + (s.values[i] ?? 0), 0));
    for (const v of this.line()?.values ?? []) max = Math.max(max, v);
    // Round up to a tidy axis maximum divisible into 4 or 5 steps.
    const step = max <= 5 ? 1 : max <= 20 ? 5 : max <= 50 ? 10 : Math.ceil(max / 50) * 10;
    return Math.max(step * Math.ceil(max / step), step * 4);
  });

  private readonly plotH = this.H - this.padT - this.padB;
  private readonly slot = computed(() => (this.W - this.padL - 6) / Math.max(1, this.labels().length));
  readonly barW = computed(() => Math.max(6, Math.min(18, this.slot() * 0.55)));

  readonly ticks = computed(() => {
    const max = this.max();
    const step = max / 5;
    return Array.from({ length: 6 }, (_, i) => ({ v: Math.round(step * i), y: this.padT + this.plotH - (this.plotH * i) / 5 }));
  });

  private y(v: number): number {
    return this.padT + this.plotH - (v / this.max()) * this.plotH;
  }

  readonly columns = computed(() =>
    this.labels().map((label, i) => {
      const x = this.padL + this.slot() * i + (this.slot() - this.barW()) / 2;
      let base = 0;
      const segs = this.series().map((s) => {
        const v = s.values[i] ?? 0;
        const seg = { name: s.name, color: s.color, v, y: this.y(base + v), h: (v / this.max()) * this.plotH };
        base += v;
        return seg;
      });
      return { i, label, x, segs: segs.filter((s) => s.v > 0) };
    })
  );

  readonly linePoints = computed(() => {
    const l = this.line();
    if (!l) return [];
    return this.labels().map((label, i) => ({ i, label, v: l.values[i] ?? 0, x: this.padL + this.slot() * i + this.slot() / 2, y: this.y(l.values[i] ?? 0) }));
  });

  readonly linePath = computed(() => {
    const pts = this.linePoints();
    return pts.length ? pts.map((p, i) => `${i ? 'L' : 'M'}${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ') : '';
  });
}
