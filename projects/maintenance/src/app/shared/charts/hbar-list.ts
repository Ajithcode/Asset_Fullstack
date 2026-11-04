import { Component, computed, input } from '@angular/core';

export interface HBarItem {
  label: string;
  value: number;
}

const RAMP = ['#ef4444', '#f97316', '#fb923c', '#f59e0b', '#fbbf24', '#fcd34d'];

/** Ranked horizontal bars (red for the largest, fading to yellow), e.g. Top Assets at Risk. */
@Component({
  selector: 'app-mnt-hbars',
  template: `
    <ul class="hb">
      @for (row of rows(); track row.label) {
        <li>
          <span class="hb-l">{{ row.label }}</span>
          <span class="hb-track"><span class="hb-fill" [style.width.%]="row.pct" [style.background]="row.color"></span></span>
          <span class="hb-v">{{ row.value }}</span>
        </li>
      } @empty {
        <li class="hb-empty">No data for this period.</li>
      }
    </ul>
  `,
  styles: `
    .hb { list-style: none; margin: 0; padding: 4px 0; display: flex; flex-direction: column; gap: 13px; font-size: 12.5px; }
    .hb li { display: grid; grid-template-columns: 110px minmax(0, 1fr) 28px; gap: 10px; align-items: center; }
    .hb-l { color: #2c2a3d; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .hb-track { height: 16px; }
    .hb-fill { display: block; height: 100%; min-width: 4px; }
    .hb-v { font-weight: 600; }
    .hb-empty { display: block !important; color: #6b6880; }
  `
})
export class HBarList {
  readonly items = input.required<HBarItem[]>();
  readonly limit = input(6);

  readonly rows = computed(() => {
    const sorted = [...this.items()].filter((i) => i.value > 0).sort((a, b) => b.value - a.value).slice(0, this.limit());
    const max = sorted[0]?.value || 1;
    return sorted.map((r, i) => ({ ...r, pct: (r.value / max) * 100, color: RAMP[Math.min(i, RAMP.length - 1)] }));
  });
}
