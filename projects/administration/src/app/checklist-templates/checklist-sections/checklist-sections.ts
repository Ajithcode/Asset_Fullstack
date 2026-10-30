import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChevronDown, ChevronRight, EllipsisVertical, GripVertical, LucideAngularModule } from 'lucide-angular';
import { ChecklistItem, ChecklistSection, ChecklistTemplate, RESPONSE_TONES, ResponseType } from '../checklist-templates.model';

export interface ItemRef {
  sectionId: string;
  itemId: string;
}

export interface DeleteRequest {
  kind: 'item' | 'section';
  sectionId: string;
  itemId?: string;
}

/** Collapsible sections with their items: toggles, scores, reordering (buttons or drag) and row menus. */
@Component({
  standalone: true,
  selector: 'app-checklist-sections',
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './checklist-sections.html',
  styleUrls: ['../../shared/admin-workspace.css', './checklist-sections.css']
})
export class ChecklistSections {
  @Input({ required: true }) template!: ChecklistTemplate;
  @Input() selectedItemId: string | null = null;
  @Output() selectItem = new EventEmitter<ItemRef>();
  @Output() addItemTo = new EventEmitter<string>();
  @Output() duplicateItem = new EventEmitter<ItemRef>();
  @Output() requestDelete = new EventEmitter<DeleteRequest>();
  @Output() changed = new EventEmitter<void>();

  readonly icons = { ChevronDown, ChevronRight, EllipsisVertical, GripVertical };

  private collapsed = new Set<string>();
  /** Open ⋯ menu: section id or item id */
  menuFor: string | null = null;
  renaming: string | null = null;
  renameText = '';
  private drag: { sectionId: string; index: number } | null = null;
  dropTarget: string | null = null;

  isCollapsed(s: ChecklistSection): boolean {
    return this.collapsed.has(s.id);
  }

  toggle(s: ChecklistSection): void {
    if (this.collapsed.has(s.id)) this.collapsed.delete(s.id);
    else this.collapsed.add(s.id);
  }

  collapseAll(): void {
    this.template.sections.forEach((s) => this.collapsed.add(s.id));
  }

  expandAll(): void {
    this.collapsed.clear();
  }

  expand(sectionId: string): void {
    this.collapsed.delete(sectionId);
  }

  tone(type: ResponseType): string {
    return RESPONSE_TONES[type];
  }

  typeLabel(type: ResponseType): string {
    return type === 'Yes/No' ? 'Yes / No' : type === 'Pass/Fail' ? 'Pass / Fail' : type;
  }

  open(s: ChecklistSection, item: ChecklistItem): void {
    this.menuFor = null;
    this.selectItem.emit({ sectionId: s.id, itemId: item.id });
  }

  toggleMenu(id: string, event: Event): void {
    event.stopPropagation();
    this.menuFor = this.menuFor === id ? null : id;
  }

  closeMenu(): void {
    this.menuFor = null;
  }

  touch(): void {
    this.changed.emit();
  }

  // ===== Items =====
  moveItem(s: ChecklistSection, index: number, delta: number): void {
    this.menuFor = null;
    const target = index + delta;
    if (target < 0 || target >= s.items.length) return;
    [s.items[index], s.items[target]] = [s.items[target], s.items[index]];
    this.changed.emit();
  }

  duplicate(s: ChecklistSection, item: ChecklistItem): void {
    this.menuFor = null;
    this.duplicateItem.emit({ sectionId: s.id, itemId: item.id });
  }

  deleteItem(s: ChecklistSection, item: ChecklistItem): void {
    this.menuFor = null;
    this.requestDelete.emit({ kind: 'item', sectionId: s.id, itemId: item.id });
  }

  // ===== Sections =====
  startRename(s: ChecklistSection): void {
    this.menuFor = null;
    this.renaming = s.id;
    this.renameText = s.title;
  }

  finishRename(s: ChecklistSection): void {
    const text = this.renameText.trim();
    if (text && text !== s.title) {
      s.title = text;
      this.changed.emit();
    }
    this.renaming = null;
  }

  cancelRename(): void {
    this.renaming = null;
  }

  moveSection(index: number, delta: number): void {
    this.menuFor = null;
    const list = this.template.sections;
    const target = index + delta;
    if (target < 0 || target >= list.length) return;
    [list[index], list[target]] = [list[target], list[index]];
    this.changed.emit();
  }

  addItem(s: ChecklistSection): void {
    this.menuFor = null;
    this.collapsed.delete(s.id);
    this.addItemTo.emit(s.id);
  }

  deleteSection(s: ChecklistSection): void {
    this.menuFor = null;
    this.requestDelete.emit({ kind: 'section', sectionId: s.id });
  }

  // ===== Drag and drop (items, within or across sections) =====
  onDragStart(s: ChecklistSection, index: number, event: DragEvent): void {
    this.drag = { sectionId: s.id, index };
    event.dataTransfer?.setData('text/plain', `${s.id}:${index}`);
  }

  onDragOver(key: string, event: DragEvent): void {
    if (!this.drag) return;
    event.preventDefault();
    this.dropTarget = key;
  }

  /** Drop on an item row (insert before it) or on a section header (append) */
  onDrop(target: ChecklistSection, index: number | null, event: DragEvent): void {
    event.preventDefault();
    const drag = this.drag;
    this.drag = null;
    this.dropTarget = null;
    if (!drag) return;
    const from = this.template.sections.find((s) => s.id === drag.sectionId);
    if (!from) return;
    const [moved] = from.items.splice(drag.index, 1);
    let at = index ?? target.items.length;
    if (from === target && index !== null && drag.index < index) at -= 1;
    target.items.splice(Math.max(0, at), 0, moved);
    this.collapsed.delete(target.id);
    this.changed.emit();
  }

  onDragEnd(): void {
    this.drag = null;
    this.dropTarget = null;
  }
}
