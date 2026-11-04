import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChevronDown, ChevronRight, ListFilter, LucideAngularModule, Search } from 'lucide-angular';
import { KIND_ICONS, SiteNode } from '../maps.data';
import { MapsStore } from '../maps.store';

/** Organization > site > building > floor tree with search; floors are selectable. */
@Component({
  standalone: true,
  selector: 'app-site-tree',
  imports: [FormsModule, NgTemplateOutlet, LucideAngularModule],
  template: `
    <div class="tree-tools">
      <label class="search">
        <lucide-icon [img]="icons.Search" [size]="15"></lucide-icon>
        <input [placeholder]="placeholder" [attr.aria-label]="placeholder" [(ngModel)]="query" />
      </label>
      <button type="button" class="icon-btn" aria-label="Filter" [class.on]="floorsOnly" (click)="floorsOnly = !floorsOnly">
        <lucide-icon [img]="icons.ListFilter" [size]="16"></lucide-icon>
      </button>
    </div>
    <ul class="tree" role="tree">
      <ng-container *ngTemplateOutlet="branch; context: { $implicit: store.tree, depth: 0 }"></ng-container>
    </ul>

    <ng-template #branch let-node let-depth="depth">
      @if (matches(node)) {
        <li role="treeitem" [attr.aria-expanded]="node.children ? isOpen(node) : null" [attr.aria-selected]="node.id === store.selectedFloorId">
          <div
            class="node"
            [class.node--floor]="node.kind === 'floor'"
            [class.active]="node.id === store.selectedFloorId"
            [style.padding-left.px]="depth * 16 + 4"
            (click)="onNode(node)"
          >
            <span class="node__caret">
              @if (node.children?.length) {
                <lucide-icon [img]="isOpen(node) ? icons.ChevronDown : icons.ChevronRight" [size]="14"></lucide-icon>
              }
            </span>
            <lucide-icon class="node__icon" [img]="kindIcon(node)" [size]="15"></lucide-icon>
            <span class="node__label">{{ node.label }}</span>
            @if (node.count !== undefined) {
              <span class="node__count">{{ node.count }}</span>
            }
          </div>
          @if (node.children?.length && isOpen(node)) {
            <ul role="group">
              @for (c of node.children; track c.id) {
                <ng-container *ngTemplateOutlet="branch; context: { $implicit: c, depth: depth + 1 }"></ng-container>
              }
            </ul>
          }
        </li>
      }
    </ng-template>
  `,
  styles: `
    :host { display: block; }
    .tree-tools { display: flex; gap: 8px; margin-bottom: 10px; }
    .search { flex: 1; min-width: 0; height: 36px; display: flex; align-items: center; gap: 8px; padding: 0 10px;
      border: 1px solid #e6e8ef; border-radius: 10px; color: #94a3b8; background: #fff; }
    .search input { flex: 1; min-width: 0; border: 0; outline: 0; font: inherit; color: #0f172a; background: transparent; }
    .icon-btn { display: grid; place-items: center; width: 36px; height: 36px; border: 1px solid #e6e8ef; border-radius: 10px;
      background: #fff; color: #475569; cursor: pointer; }
    .icon-btn.on { border-color: #5b2aa8; color: #5b2aa8; background: #f3effc; }
    ul { list-style: none; margin: 0; padding: 0; }
    .node { display: flex; align-items: center; gap: 6px; height: 32px; padding-right: 6px; border-radius: 8px; cursor: pointer; color: #1e293b; }
    .node:hover { background: #faf9fe; }
    .node.active { background: #ede7fb; color: #5b2aa8; font-weight: 700; }
    .node__caret { display: grid; place-items: center; width: 14px; color: #64748b; }
    .node__icon { color: #5b2aa8; }
    .node--floor .node__icon { color: #7c6bb0; }
    .node__label { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .node__count { min-width: 20px; padding: 0 6px; border-radius: 6px; background: #f1f5f9; font-size: 11px; text-align: center; color: #475569; }
    .node.active .node__count { background: #fff; color: #5b2aa8; }
  `
})
export class SiteTree {
  readonly store = inject(MapsStore);
  readonly icons = { ChevronDown, ChevronRight, ListFilter, Search };

  @Input() placeholder = 'Search maps, sites or floors...';
  @Output() floorSelect = new EventEmitter<string>();

  query = '';
  /** Filter button: hide branches that hold no floor plans. */
  floorsOnly = false;
  private closed = new Set<string>(['ho-b', 'p1', 'wh']);

  kindIcon(node: SiteNode) {
    return KIND_ICONS[node.kind];
  }

  isOpen(node: SiteNode): boolean {
    return !!this.query.trim() || !this.closed.has(node.id);
  }

  onNode(node: SiteNode): void {
    if (node.kind === 'floor') {
      this.store.selectFloor(node.id);
      this.floorSelect.emit(node.id);
      return;
    }
    if (this.closed.has(node.id)) this.closed.delete(node.id);
    else this.closed.add(node.id);
  }

  matches(node: SiteNode): boolean {
    const q = this.query.trim().toLowerCase();
    const self = (!q || node.label.toLowerCase().includes(q)) && (!this.floorsOnly || node.kind !== 'floor' || (node.count ?? 0) > 0);
    return self || !!node.children?.some((c) => this.matches(c));
  }
}
