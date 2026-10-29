import { Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Eye, LucideAngularModule, Settings, X } from 'lucide-angular';
import { DELIVERY_TIMINGS, LANGUAGES, PRIORITIES, RETRY_POLICIES, TEMPLATE_CATEGORIES, TRIGGER_EVENTS } from '../notification-templates.data';
import { Channel, CHANNEL_LABELS, CHANNELS, NotificationTemplate } from '../notification-templates.model';
import { NotificationTemplateService } from '../notification-templates.service';
import { validateTemplate } from '../template-render';
import { TemplatePreview } from '../template-preview/template-preview';

/** Right drawer: template settings plus the Live Preview. */
@Component({
  standalone: true,
  selector: 'app-template-settings',
  imports: [FormsModule, LucideAngularModule, TemplatePreview],
  templateUrl: './template-settings.html',
  styleUrls: ['../../shared/admin-workspace.css', './template-settings.css']
})
export class TemplateSettings {
  private readonly service = inject(NotificationTemplateService);

  @Input({ required: true }) template!: NotificationTemplate;
  @Input() submitted = false;
  @Output() cancel = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();
  @Output() previewAction = new EventEmitter<string>();

  readonly icons = { Settings, X, Eye };
  readonly categories = TEMPLATE_CATEGORIES;
  readonly triggerEvents = TRIGGER_EVENTS;
  readonly languages = LANGUAGES;
  readonly priorities = PRIORITIES;
  readonly timings = DELIVERY_TIMINGS;
  readonly retries = RETRY_POLICIES;
  readonly channels = CHANNELS;
  readonly channelLabels = CHANNEL_LABELS;
  previewChannel: Channel = 'email';

  err(field: string): string {
    if (!this.submitted) return '';
    const codeTaken = this.service.codeTaken(this.template.code, this.template.id);
    return validateTemplate(this.template, codeTaken)[field] ?? '';
  }

  /** The switch shows Active vs not; switching off an Active template makes it Inactive. */
  get active(): boolean {
    return this.template.status === 'Active';
  }

  set active(on: boolean) {
    this.template.status = on ? 'Active' : 'Inactive';
  }

  upperCode(): void {
    this.template.code = this.template.code.toUpperCase();
  }
}
