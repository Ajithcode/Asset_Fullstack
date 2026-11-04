import { Component, computed, input } from '@angular/core';

let nextId = 0;

/** Circular organisation seal drawn from the stamp name; shows the uploaded image instead when there is one. */
@Component({
  selector: 'app-insp-stamp-seal',
  standalone: true,
  template: `
    @if (image()) {
      <img [src]="image()" [attr.width]="size()" [attr.height]="size()" [alt]="name() + ' stamp'" />
    } @else {
      <svg viewBox="0 0 120 120" [attr.width]="size()" [attr.height]="size()" role="img" [attr.aria-label]="name() + ' stamp'">
        <defs>
          <path [attr.id]="uid + 't'" d="M 19 60 A 41 41 0 0 1 101 60" />
          <path [attr.id]="uid + 'b'" d="M 14 60 A 46 46 0 0 0 106 60" />
        </defs>
        <circle cx="60" cy="60" r="56" fill="#fff" stroke="#3b2ea8" stroke-width="3" />
        <circle cx="60" cy="60" r="35" fill="none" stroke="#3b2ea8" stroke-width="1.5" />
        <text fill="#3b2ea8" font-size="10.5" font-weight="800" letter-spacing="1" text-anchor="middle">
          <textPath [attr.href]="'#' + uid + 't'" startOffset="50%">{{ top() }}</textPath>
        </text>
        <text fill="#3b2ea8" font-size="9.5" font-weight="800" letter-spacing="1.5" text-anchor="middle">
          <textPath [attr.href]="'#' + uid + 'b'" startOffset="50%">{{ bottom() }}</textPath>
        </text>
        @for (l of center(); track $index) {
          <text x="60" [attr.y]="center().length === 1 ? 58 : 52 + $index * 11" fill="#3b2ea8" font-size="9" font-weight="800" text-anchor="middle">{{ l }}</text>
        }
        <text x="60" y="80" fill="#3b2ea8" font-size="10" text-anchor="middle">★</text>
      </svg>
    }
  `,
  styles: [':host { display: inline-flex; flex: 0 0 auto; } img { object-fit: contain; border-radius: 50%; }']
})
export class StampSeal {
  readonly name = input('');
  readonly image = input<string | null | undefined>('');
  readonly caption = input('');
  readonly size = input(96);
  readonly uid = `seal${nextId++}`;

  /** Top arc: organisation / department caption, else the full name. */
  readonly top = computed(() => (this.caption() || this.name()).toUpperCase().slice(0, 24));
  readonly bottom = computed(() => (this.caption() ? 'INTERNAL' : 'APPROVED'));
  /** Centre: the stamp name without the word "stamp", up to two lines. */
  readonly center = computed(() => {
    const words = this.name().replace(/\bstamp\b/i, '').trim().toUpperCase().split(/\s+/).filter(Boolean);
    if (words.length <= 1) return words.length ? words : ['SEAL'];
    const half = Math.ceil(words.length / 2);
    return [words.slice(0, half).join(' '), words.slice(half).join(' ')].map((w) => w.slice(0, 14));
  });
}
