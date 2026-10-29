import { Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Bell, Bold, Code, Image, Italic, Link, List, ListOrdered, LucideAngularModule, Mail, MessageSquare, Smartphone, TextAlignCenter,
  TextAlignEnd, TextAlignStart, Underline
} from 'lucide-angular';
import { VARIABLE_GROUPS } from '../notification-templates.data';
import { Channel, CHANNEL_LABELS, CHANNELS, NotificationTemplate, RecipientType } from '../notification-templates.model';
import { KNOWN_VARIABLES, PUSH_BODY_LIMIT, PUSH_TITLE_LIMIT, Seg, SMS_LIMIT, token, validateTemplate, variableSegments } from '../template-render';
import { VariablePanel } from './variable-panel';

type FieldKey = 'subject' | 'body' | 'sms' | 'inAppTitle' | 'inAppBody' | 'pushTitle' | 'pushBody';
type TextField = HTMLInputElement | HTMLTextAreaElement;

/** Default insert target per tab when no field has been focused yet. */
const TAB_DEFAULT: Record<Channel, FieldKey> = { email: 'body', sms: 'sms', inApp: 'inAppBody', push: 'pushBody' };
const TAB_FIELDS: Record<Channel, FieldKey[]> = {
  email: ['subject', 'body'],
  sms: ['sms'],
  inApp: ['inAppTitle', 'inAppBody'],
  push: ['pushTitle', 'pushBody']
};

/**
 * Centre editor: channel tabs and the message fields. The email body is a textarea laid over a
 * mirror that shows the same text with {{variables}} highlighted.
 */
@Component({
  standalone: true,
  selector: 'app-template-editor',
  imports: [FormsModule, LucideAngularModule, VariablePanel],
  templateUrl: './template-editor.html',
  styleUrls: ['../../shared/admin-workspace.css', './template-editor.css']
})
export class TemplateEditor {
  @Input({ required: true }) template!: NotificationTemplate;
  @Input() submitted = false;
  @Output() notice = new EventEmitter<string>();

  @ViewChild('bodyEl') bodyEl?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('smsEl') smsEl?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('inAppBodyEl') inAppBodyEl?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('pushBodyEl') pushBodyEl?: ElementRef<HTMLTextAreaElement>;

  readonly icons = { Mail, MessageSquare, Smartphone, Bell, Bold, Italic, Underline, List, ListOrdered, TextAlignStart, TextAlignCenter, TextAlignEnd, Link, Image, Code };
  readonly channels = CHANNELS;
  readonly channelLabels = CHANNEL_LABELS;
  readonly recipientTypes: RecipientType[] = ['Assignee', 'Supervisor', 'Team'];
  readonly variableGroups = VARIABLE_GROUPS;
  readonly smsLimit = SMS_LIMIT;
  readonly pushTitleLimit = PUSH_TITLE_LIMIT;
  readonly pushBodyLimit = PUSH_BODY_LIMIT;
  readonly paragraphStyles = ['Paragraph', 'Heading', 'Quote'];
  paragraphStyle = 'Paragraph';

  tab: Channel = 'email';
  private target: { key: FieldKey; el: TextField } | null = null;

  channelIcon(c: Channel) {
    return c === 'email' ? this.icons.Mail : c === 'sms' ? this.icons.MessageSquare : c === 'inApp' ? this.icons.Smartphone : this.icons.Bell;
  }

  setTab(c: Channel): void {
    this.tab = c;
    this.target = null;
  }

  err(field: string): string {
    return this.submitted ? validateTemplate(this.template, false)[field] ?? '' : '';
  }

  /** Highlighted mirror of the email body; a trailing space keeps the last empty line's height. */
  bodySegments(): Seg[] {
    return variableSegments(this.template.email.body + ' ');
  }

  known(name: string | null): boolean {
    return !!name && KNOWN_VARIABLES.has(name);
  }

  // ===== Variable insertion =====

  track(key: FieldKey, el: TextField): void {
    this.target = { key, el };
  }

  private value(key: FieldKey): string {
    const t = this.template;
    switch (key) {
      case 'subject': return t.email.subject;
      case 'body': return t.email.body;
      case 'sms': return t.sms.text;
      case 'inAppTitle': return t.inApp.title;
      case 'inAppBody': return t.inApp.body;
      case 'pushTitle': return t.push.title;
      default: return t.push.body;
    }
  }

  private setValue(key: FieldKey, v: string): void {
    const t = this.template;
    switch (key) {
      case 'subject': t.email.subject = v; break;
      case 'body': t.email.body = v; break;
      case 'sms': t.sms.text = v; break;
      case 'inAppTitle': t.inApp.title = v; break;
      case 'inAppBody': t.inApp.body = v; break;
      case 'pushTitle': t.push.title = v; break;
      default: t.push.body = v;
    }
  }

  private defaultTarget(): { key: FieldKey; el: TextField } | null {
    const key = TAB_DEFAULT[this.tab];
    const ref = key === 'body' ? this.bodyEl : key === 'sms' ? this.smsEl : key === 'inAppBody' ? this.inAppBodyEl : this.pushBodyEl;
    return ref ? { key, el: ref.nativeElement } : null;
  }

  /** Replaces the current selection of the active field and puts the caret after the new text. */
  private replaceSelection(build: (selected: string) => string): void {
    const t = this.target && TAB_FIELDS[this.tab].includes(this.target.key) ? this.target : this.defaultTarget();
    if (!t) return;
    const v = this.value(t.key);
    const start = t.el.selectionStart ?? v.length;
    const end = t.el.selectionEnd ?? start;
    const text = build(v.slice(start, end));
    this.setValue(t.key, v.slice(0, start) + text + v.slice(end));
    const caret = start + text.length;
    setTimeout(() => {
      t.el.focus();
      t.el.setSelectionRange(caret, caret);
    });
  }

  insertVariable(name: string): void {
    this.replaceSelection(() => token(name));
  }

  insertFromSelect(event: Event): void {
    const select = event.target as HTMLSelectElement;
    if (select.value) this.insertVariable(select.value);
    select.value = '';
  }

  // ===== Formatting toolbar (body only) =====

  private bodyTarget(): boolean {
    if (this.target?.key !== 'body' && this.bodyEl) this.target = { key: 'body', el: this.bodyEl.nativeElement };
    return !!this.bodyEl;
  }

  wrap(marker: string): void {
    if (!this.bodyTarget()) return;
    this.replaceSelection((sel) => `${marker}${sel || 'text'}${marker}`);
  }

  list(ordered: boolean): void {
    if (!this.bodyTarget()) return;
    this.replaceSelection((sel) => {
      const lines = (sel || 'Item').split('\n');
      return lines.map((l, i) => `${ordered ? `${i + 1}.` : '•'} ${l}`).join('\n');
    });
  }

  /** Call-to-action button line, rendered as a button in the preview. */
  insertButton(): void {
    if (!this.bodyTarget()) return;
    this.replaceSelection((sel) => `\n[button:${sel || 'Open in Asset360'}]\n`);
  }

  unsupported(what: string): void {
    this.notice.emit(`DEMO MODE: ${what} is not available in the template editor yet.`);
  }

  // ===== Counters =====

  get smsLength(): number {
    return this.template.sms.text.length;
  }

  get smsSegments(): number {
    return Math.max(1, Math.ceil(this.smsLength / SMS_LIMIT));
  }
}
