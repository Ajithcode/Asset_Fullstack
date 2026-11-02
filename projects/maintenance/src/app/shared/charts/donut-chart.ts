import { Component, computed, input } from '@angular/core';

export interface DonutSegment {
  label: string;
  value: number;
  color: string;
}

/** Donut with a centre total and a legend of "value (pct%)" rows, as in the Maintenance mockups. */
@Component({
  selector: 'app-mnt-donut',
  template: `
    <div class="donut-wrap">
      <svg viewBox="0 0 120 120" class="donut" role="img" [attr.aria-label]="ariaLabel()">
        <circle cx="60" cy="60" [attr.r]="r" fill="none" stroke="#eef0f5" [attr.stroke-width]="stroke" />
        @for (s of arcs(); track s.label) {
          <circle cx="60" cy="60" [attr.r]="r" fill="none" [attr.stroke]="s.color" [attr.stroke-width]="stroke"
                  [attr.stroke-dasharray]="s.dash" [attr.stroke-dashoffset]="s.offset" transform="rotate(-90 60 60)">
            <title>{{ s.label }}: {{ s.value }} ({{ s.pct }}%)</title>
          </circle>
        }
        <text x="60" y="58" text-anchor="middle" class="d-total">{{ total() }}</text>
        <text x="60" y="74" text-anchor="middle" class="d-label">{{ centerLabel() }}</text>
      </svg>
      <ul class="legend">
        @for (s of arcs(); track s.label) {
          <li><i [style.background]="s.color"></i><span class="l-name">{{ s.label }}</span><span class="l-val">{{ s.value }} ({{ s.pct }}%)</span></li>
        }
      </ul>
    </div>
  `,
  styles: `
    :host { display: block; }
    .donut-wrap { display: flex; align-items: center; gap: 22px; padding: 4px 4px 8px; }
    .donut { width: 150px; height: 150px; flex: 0 0 auto; }
    .d-total { font-size: 19px; font-weight: 800; fill: #161427; }
    .d-label { font-size: 8.5px; fill: #4b4860; }
    .legend { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 14px; flex: 1; min-width: 0; font-size: 12.5px; }
    .legend li { display: flex; align-items: center; gap: 8px; }
    .legend i { width: 10px; height: 10px; border-radius: 50%; flex: 0 0 auto; }
    .l-name { flex: 1; color: #2c2a3d; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .l-val { color: #2c2a3d; white-space: nowrap; }
    @media (max-width: 520px) { .donut-wrap { flex-direction: column; } }
  `
})
export class DonutChart {
  readonly segments = input.required<DonutSegment[]>();
  readonly centerLabel = input('');
  readonly r = 46;
  readonly stroke = 18;

  readonly total = computed(() => this.segments().reduce((sum, s) => sum + s.value, 0));

  readonly arcs = computed(() => {
    const total = this.total();
    const circ = 2 * Math.PI * this.r;
    let acc = 0;
    return this.segments().map((s) => {
      const len = total ? (s.value / total) * circ : 0;
      const arc = { ...s, pct: total ? Math.round((s.value / total) * 100) : 0, dash: `${len} ${circ - len}`, offset: -acc };
      acc += len;
      return arc;
    });
  });

  ariaLabel(): string {
    return this.arcs().map((a) => `${a.label} ${a.value}`).join(', ');
  }
}
