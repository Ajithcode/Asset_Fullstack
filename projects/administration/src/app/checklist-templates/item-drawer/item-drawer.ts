import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Info, LucideAngularModule, X } from 'lucide-angular';
import { ChecklistItem, ChecklistSection, FAILURE_ACTIONS, RESPONSE_TONES, RESPONSE_TYPES, ResponseType, itemErrors } from '../checklist-templates.model';
import { ResponsePreview } from '../response-preview/response-preview';

export interface ItemSave {
  item: ChecklistItem;
  sectionId: string;
}

/** Right-hand "Checklist Item Settings" drawer; edits a copy and hands it back on Save Item. */
@Component({
  standalone: true,
  selector: 'app-item-drawer',
  imports: [FormsModule, LucideAngularModule, ResponsePreview],
  templateUrl: './item-drawer.html',
  styleUrls: ['../../shared/admin-workspace.css', './item-drawer.css']
})
export class ItemDrawer implements OnChanges {
  @Input({ required: true }) item!: ChecklistItem;
  @Input({ required: true }) sectionId!: string;
  @Input() sections: ChecklistSection[] = [];
  @Input() isNew = false;
  @Output() save = new EventEmitter<ItemSave>();
  @Output() cancel = new EventEmitter<void>();

  readonly icons = { Info, X };
  readonly responseTypes = RESPONSE_TYPES;
  readonly failureActions = FAILURE_ACTIONS;

  edit!: ChecklistItem;
  section = '';
  showErrors = false;

  ngOnChanges(): void {
    this.edit = structuredClone(this.item);
    this.section = this.sectionId;
    this.showErrors = false;
  }

  sectionLabel(s: ChecklistSection, i: number): string {
    return `${i + 1}. ${s.title}`;
  }

  tone(type: ResponseType): string {
    return RESPONSE_TONES[type];
  }

  get errors(): Record<string, string> {
    return itemErrors(this.edit);
  }

  err(field: string): string | null {
    return this.showErrors ? (this.errors[field] ?? null) : null;
  }

  get expectedPlaceholder(): string {
    switch (this.edit.responseType) {
      case 'Numeric':
        return 'e.g. 40-60 or 75%';
      case 'Yes/No':
        return 'e.g. Yes';
      case 'Pass/Fail':
        return 'e.g. OK / No corrosion';
      default:
        return 'e.g. Clear photo of the nameplate';
    }
  }

  onSave(): void {
    this.showErrors = true;
    if (Object.keys(this.errors).length || !this.section) return;
    this.edit.text = this.edit.text.trim();
    this.save.emit({ item: structuredClone(this.edit), sectionId: this.section });
  }
}
