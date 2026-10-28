import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { ChevronLeft, ChevronRight, LucideAngularModule, X } from 'lucide-angular';
import { ChecklistItem, ChecklistSection, ChecklistTemplate, itemCount, maxScore } from '../checklist-templates.model';
import { ResponsePreview } from '../response-preview/response-preview';

interface Step {
  section: ChecklistSection;
  sectionIndex: number;
  item: ChecklistItem;
  itemIndex: number;
}

/** Read-only run-through of a template, one item at a time, as a technician would see it. */
@Component({
  standalone: true,
  selector: 'app-checklist-preview',
  imports: [LucideAngularModule, ResponsePreview],
  templateUrl: './checklist-preview.html',
  styleUrls: ['../../shared/admin-workspace.css', './checklist-preview.css']
})
export class ChecklistPreview implements OnChanges {
  @Input({ required: true }) template!: ChecklistTemplate;
  @Output() closed = new EventEmitter<void>();

  readonly icons = { ChevronLeft, ChevronRight, X };
  steps: Step[] = [];
  index = 0;

  ngOnChanges(): void {
    this.steps = [];
    this.template.sections.forEach((section, sectionIndex) =>
      section.items.forEach((item, itemIndex) => this.steps.push({ section, sectionIndex, item, itemIndex }))
    );
    this.index = 0;
  }

  get step(): Step | null {
    return this.steps[this.index] ?? null;
  }

  /** The summary page comes after the last item */
  get finished(): boolean {
    return this.index >= this.steps.length;
  }

  get progress(): number {
    return this.steps.length ? Math.round((Math.min(this.index, this.steps.length) / this.steps.length) * 100) : 100;
  }

  get totalItems(): number {
    return itemCount(this.template);
  }

  get totalScore(): number {
    return maxScore(this.template);
  }

  get requiredCount(): number {
    return this.steps.filter((s) => s.item.required).length;
  }

  get criticalCount(): number {
    return this.steps.filter((s) => s.item.critical).length;
  }

  prev(): void {
    if (this.index > 0) this.index -= 1;
  }

  next(): void {
    if (this.index < this.steps.length) this.index += 1;
  }

  restart(): void {
    this.index = 0;
  }
}
