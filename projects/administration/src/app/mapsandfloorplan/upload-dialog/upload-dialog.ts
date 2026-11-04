import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CloudUpload, LucideAngularModule } from 'lucide-angular';
import { FloorPlan } from '../maps.data';
import { MapsStore } from '../maps.store';

const FORMATS: FloorPlan['format'][] = ['PNG', 'JPG', 'PDF', 'SVG', 'DWG'];

/**
 * Upload a floor plan (or a new version of the current one) for the selected floor.
 * DEMO MODE: only the file name and type are kept.
 */
@Component({
  standalone: true,
  selector: 'app-upload-dialog',
  imports: [FormsModule, LucideAngularModule],
  template: `
    <div class="overlay" (click)="cancel.emit()">
      <form class="modal" role="dialog" aria-modal="true" [attr.aria-label]="title" (click)="$event.stopPropagation()" (ngSubmit)="submit()">
        <h2>{{ title }}</h2>
        <div class="rform">
          <div class="field">
            <label for="up-loc">Location</label>
            <select id="up-loc" name="loc" [ngModel]="store.selectedFloorId" (ngModelChange)="store.selectFloor($event)" [disabled]="mode === 'version'">
              @for (f of store.floors; track f.id) { <option [value]="f.id">{{ f.label }}</option> }
            </select>
          </div>
          <label class="dropzone" for="up-file">
            <lucide-icon [img]="icons.CloudUpload" [size]="28"></lucide-icon>
            <b>{{ fileName || 'Click to choose a file' }}</b>
            <span>PNG, JPG, PDF, SVG or DWG, up to 50 MB</span>
            <input id="up-file" type="file" hidden accept=".png,.jpg,.jpeg,.pdf,.svg,.dwg" (change)="pick($event)" />
          </label>
          @if (mode === 'plan') {
            <div class="field" [class.invalid]="tried && !name.trim()">
              <label for="up-name">Floor Plan Name <span class="req">*</span></label>
              <input id="up-name" name="name" [(ngModel)]="name" />
              @if (tried && !name.trim()) { <small>Name is required</small> }
            </div>
            <div class="field">
              <label for="up-format">Format</label>
              <select id="up-format" name="format" [(ngModel)]="format">
                @for (f of formats; track f) { <option [value]="f">{{ f }}</option> }
              </select>
            </div>
          } @else {
            <div class="field">
              <label for="up-desc">Changes / Description</label>
              <textarea id="up-desc" name="desc" rows="3" [(ngModel)]="description" placeholder="What changed in this version?"></textarea>
            </div>
          }
        </div>
        <div class="rfoot">
          <button type="button" class="btn" (click)="cancel.emit()">Cancel</button>
          <button type="submit" class="btn btn--primary">Upload</button>
        </div>
      </form>
    </div>
  `,
  styleUrls: ['../../shared/admin-workspace.css', '../maps.css']
})
export class UploadDialog {
  readonly store = inject(MapsStore);
  readonly icons = { CloudUpload };
  readonly formats = FORMATS;

  /** 'plan' adds a floor plan; 'version' uploads a new version of the current one. */
  @Input() mode: 'plan' | 'version' = 'plan';
  @Output() done = new EventEmitter<string>();
  @Output() cancel = new EventEmitter<void>();

  name = '';
  format: FloorPlan['format'] = 'PNG';
  description = '';
  fileName = '';
  tried = false;

  get title(): string {
    return this.mode === 'plan' ? 'Upload Floor Plan' : 'Upload New Version';
  }

  pick(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.fileName = file.name;
    const ext = file.name.split('.').pop()?.toUpperCase() ?? '';
    const format = (ext === 'JPEG' ? 'JPG' : ext) as FloorPlan['format'];
    if (FORMATS.includes(format)) this.format = format;
    if (!this.name) this.name = file.name.replace(/\.[^.]+$/, '').replace(/[_-]+/g, ' ');
  }

  submit(): void {
    this.tried = true;
    if (this.mode === 'plan') {
      if (!this.name.trim()) return;
      this.done.emit(this.store.addPlan(this.name.trim(), this.format).name);
    } else {
      this.done.emit(this.store.addVersion(this.description.trim()).version);
    }
  }
}
