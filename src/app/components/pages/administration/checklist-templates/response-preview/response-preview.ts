import { Component, Input, OnChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Camera, LucideAngularModule, MessageSquare } from 'lucide-angular';
import { ChecklistItem } from '../checklist-templates.model';

/** How a checklist item renders for the technician during execution (adapts to the response type). */
@Component({
  standalone: true,
  selector: 'app-response-preview',
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './response-preview.html',
  styleUrls: ['../../shared/admin-workspace.css', './response-preview.css']
})
export class ResponsePreview implements OnChanges {
  @Input({ required: true }) item!: ChecklistItem;

  readonly icons = { Camera, MessageSquare };
  answer = '';
  numeric: number | null = null;
  text = '';
  comment = '';
  showComment = false;
  photos = 0;

  ngOnChanges(): void {
    this.answer = '';
    this.numeric = null;
    this.text = '';
    this.comment = '';
    this.showComment = false;
    this.photos = 0;
  }

  choose(value: string): void {
    this.answer = this.answer === value ? '' : value;
  }

  addPhoto(): void {
    this.photos += 1;
  }

  toggleComment(): void {
    this.showComment = !this.showComment;
  }

  /** Whether the numeric reading falls inside "a-b" (or equals "a") from Expected Value */
  get numericInRange(): boolean | null {
    if (this.numeric === null || !this.item.expected.trim()) return null;
    const m = /(-?\d+(?:\.\d+)?)\s*(?:-\s*(-?\d+(?:\.\d+)?))?/.exec(this.item.expected);
    if (!m) return null;
    const lo = Number(m[1]);
    const hi = m[2] !== undefined ? Number(m[2]) : lo;
    return this.numeric >= lo && this.numeric <= hi;
  }
}
