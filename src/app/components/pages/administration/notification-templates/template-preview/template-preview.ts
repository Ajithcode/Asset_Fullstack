import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Bell, LucideAngularModule, MessageSquare, Smartphone } from 'lucide-angular';
import { SAMPLE_VALUES } from '../notification-templates.data';
import { Channel, NotificationTemplate } from '../notification-templates.model';
import { fill, previewBlocks, PreviewBlock, Seg } from '../template-render';

/** Live Preview: renders the template with sample values for the chosen channel. */
@Component({
  standalone: true,
  selector: 'app-template-preview',
  imports: [NgTemplateOutlet, LucideAngularModule],
  templateUrl: './template-preview.html',
  styleUrl: './template-preview.css'
})
export class TemplatePreview {
  @Input({ required: true }) template!: NotificationTemplate;
  @Input() channel: Channel = 'email';
  @Output() action = new EventEmitter<string>();

  readonly icons = { Bell, MessageSquare, Smartphone };

  blocks(): PreviewBlock[] {
    return previewBlocks(this.template.email.body);
  }

  /** Variable value, or the raw token when it has no sample. */
  value(seg: Seg): string {
    return seg.variable ? SAMPLE_VALUES[seg.variable] ?? seg.text : seg.text;
  }

  filled(text: string): string {
    return fill(text, SAMPLE_VALUES);
  }

  get smsText(): string {
    return this.filled(this.template.sms.text);
  }
}
