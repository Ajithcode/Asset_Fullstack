import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import {
  LucideAngularModule,
  BookOpen,
  FileArchive,
  FileImage,
  FileSpreadsheet,
  FileText,
  FileType,
  Maximize,
  Minus,
  Plus,
  RotateCw,
} from 'lucide-angular';
import type { AssetComponent } from '../asset-components';

export type FileFormat = 'pdf' | 'image' | 'sheet' | 'word' | 'zip' | 'other';

export function formatOf(fileName: string): FileFormat {
  const ext = fileName.split('.').pop()?.toLowerCase() ?? '';
  if (ext === 'pdf') return 'pdf';
  if (['png', 'jpg', 'jpeg', 'webp', 'gif'].includes(ext)) return 'image';
  if (['xls', 'xlsx', 'csv'].includes(ext)) return 'sheet';
  if (['doc', 'docx'].includes(ext)) return 'word';
  if (['zip', 'rar', '7z'].includes(ext)) return 'zip';
  return 'other';
}

export const FORMAT_ICONS: Record<FileFormat, typeof FileText> = {
  pdf: FileText,
  image: FileImage,
  sheet: FileSpreadsheet,
  word: FileType,
  zip: FileArchive,
  other: FileText,
};

export const FORMAT_LABELS: Record<FileFormat, string> = {
  pdf: 'PDF',
  image: 'Image',
  sheet: 'Excel',
  word: 'Word',
  zip: 'ZIP',
  other: 'File',
};

export function formatSize(bytes: number): string {
  return bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

/** Saves a Blob through a temporary link. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

/**
 * Previews one file. Uploaded PDFs use the browser's own viewer; uploaded images get zoom and rotate;
 * sample entries (no `url`) get a generated title page plus placeholder pages so the viewer still works.
 */
@Component({
  selector: 'app-file-preview',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './file-preview.html',
  styleUrl: './file-preview.css',
})
export class FilePreview implements OnChanges {
  private readonly sanitizer = inject(DomSanitizer);

  readonly icons = { BookOpen, Maximize, Minus, Plus, RotateCw };
  readonly formatIcons = FORMAT_ICONS;

  @Input({ required: true }) fileName = '';
  /** blob: URL of an uploaded file; absent for sample entries. */
  @Input() url?: string;
  @Input() pages = 1;
  /** Title shown on the sample title page. */
  @Input() title = '';
  @Input() version = '';
  /** ISO date or date-time, for the sample's edition line. */
  @Input() date = '';
  @Input() manufacturer = '';
  @Input({ required: true }) component!: AssetComponent;

  @Output() download = new EventEmitter<void>();

  page = 1;
  zoom = 100;
  rotation = 0;
  private safeUrlCache = new Map<string, SafeResourceUrl>();

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['fileName'] || changes['url']) {
      this.page = 1;
      this.zoom = 100;
      this.rotation = 0;
    }
  }

  get format(): FileFormat {
    return formatOf(this.fileName);
  }

  /** Uploaded PDFs bring the browser's own page/zoom controls, so our toolbar is hidden for them. */
  get usesOwnViewer(): boolean {
    return !!this.url && this.format === 'pdf';
  }

  get safeUrl(): SafeResourceUrl | null {
    if (!this.url) return null;
    let safe = this.safeUrlCache.get(this.url);
    if (!safe) {
      // Only blob: URLs created from the user's own uploaded file reach here.
      safe = this.sanitizer.bypassSecurityTrustResourceUrl(this.url);
      this.safeUrlCache.set(this.url, safe);
    }
    return safe;
  }

  get transform(): string {
    return `scale(${this.zoom / 100}) rotate(${this.rotation}deg)`;
  }

  setPage(value: number): void {
    const n = Math.round(Number(value));
    this.page = Number.isFinite(n) ? Math.min(Math.max(1, n), this.pages) : 1;
  }

  zoomBy(step: number): void {
    this.zoom = Math.min(200, Math.max(50, this.zoom + step));
  }

  fitWidth(): void {
    this.zoom = 100;
    this.rotation = 0;
  }

  rotate(): void {
    this.rotation = (this.rotation + 90) % 360;
  }
}
