import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, map, of, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { CHANNEL_LABELS, CHANNELS, Channel, NotificationTemplate, TemplateStatus } from './notification-templates.model';

/** Request body for POST and PUT {administrationApiUrl}api/administration/notification-templates[/{templateId}] */
interface SaveNotificationTemplateRequest {
  templateName: string;
  templateCode: string;
  category: string;
  triggerEvent: string;
  /** Email, SMS, In-App, Push */
  deliveryChannels: string[];
  language: string;
  priority: string;
  deliveryTiming: string;
  retryPolicy: string;
  status: TemplateStatus;
  /** Message content; placeholders such as {assetName}, {zone}, {time} are stored as written. */
  subject?: string | null;
  body?: string | null;
  smsText?: string | null;
  inAppTitle?: string | null;
  inAppBody?: string | null;
  pushTitle?: string | null;
  pushBody?: string | null;
}

/** Raw shape returned by the notification templates API. */
interface NotificationTemplateDto {
  templateId: string;
  templateName: string;
  templateCode: string;
  category: string;
  triggerEvent: string;
  deliveryChannels: string[];
  language: string;
  priority: string | null;
  deliveryTiming: string | null;
  retryPolicy: string | null;
  status: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  tenantId: string | null;
  clientId: string | null;
  subject?: string | null;
  body?: string | null;
  smsText?: string | null;
  inAppTitle?: string | null;
  inAppBody?: string | null;
  pushTitle?: string | null;
  pushBody?: string | null;
}

function toRequest(t: NotificationTemplate): SaveNotificationTemplateRequest {
  return {
    templateName: t.name.trim(),
    templateCode: t.code.trim(),
    category: t.category,
    triggerEvent: t.triggerEvent,
    deliveryChannels: CHANNELS.filter((c) => t.channels[c]).map((c) => CHANNEL_LABELS[c]),
    language: t.language,
    priority: t.priority,
    deliveryTiming: t.deliveryTiming,
    retryPolicy: t.retryPolicy,
    status: t.status,
    subject: t.email.subject,
    body: t.email.body,
    smsText: t.sms.text,
    inAppTitle: t.inApp.title,
    inAppBody: t.inApp.body,
    pushTitle: t.push.title,
    pushBody: t.push.body
  };
}

/**
 * Starting point for a template loaded from the API with no local copy: description, version, icon and tone have no
 * backend field yet, so they start at their defaults (the message content comes from the API).
 */
function baseTemplate(dto: NotificationTemplateDto): NotificationTemplate {
  return {
    id: dto.templateId,
    name: dto.templateName,
    code: dto.templateCode,
    category: dto.category,
    owner: dto.createdBy ?? '',
    description: '',
    status: 'Draft',
    version: dto.status === 'Draft' ? 'v0.1' : 'v1.0',
    updated: dto.updatedAt ?? dto.createdAt ?? new Date().toISOString(),
    triggerEvent: dto.triggerEvent,
    channels: { email: false, sms: false, inApp: false, push: false },
    language: dto.language,
    priority: 'Normal',
    deliveryTiming: 'Send Immediately',
    retryPolicy: '3 retries (1hr apart)',
    email: { subject: dto.subject ?? '', fromName: 'Asset360 Notifications', replyTo: 'noreply@asset360.com', recipientType: 'Assignee', body: dto.body ?? '' },
    sms: { text: dto.smsText ?? '' },
    inApp: { title: dto.inAppTitle ?? dto.templateName, body: dto.inAppBody ?? '', link: '{{portal_url}}' },
    push: { title: dto.pushTitle ?? '', body: dto.pushBody ?? '' },
    icon: 'mail',
    tone: 'purple'
  };
}

/**
 * The API stores the template settings and the message content (subject, body, SMS / in-app / push text); description,
 * version, icon, tone, sender and link have no backend field yet and are kept from the local copy (this session only).
 */
function toAppTemplate(dto: NotificationTemplateDto, local: NotificationTemplate): NotificationTemplate {
  const channels = Object.fromEntries(
    CHANNELS.map((c) => [c, dto.deliveryChannels.includes(CHANNEL_LABELS[c])])
  ) as Record<Channel, boolean>;
  return {
    ...structuredClone(local),
    id: dto.templateId,
    name: dto.templateName,
    code: dto.templateCode,
    category: dto.category,
    owner: dto.createdBy || local.owner,
    status: (dto.status as TemplateStatus) || local.status,
    updated: dto.updatedAt ?? dto.createdAt ?? local.updated,
    triggerEvent: dto.triggerEvent,
    channels,
    language: dto.language,
    priority: dto.priority ?? local.priority,
    deliveryTiming: dto.deliveryTiming ?? local.deliveryTiming,
    retryPolicy: dto.retryPolicy ?? local.retryPolicy,
    email: { ...local.email, subject: dto.subject ?? '', body: dto.body ?? '' },
    sms: { ...local.sms, text: dto.smsText ?? '' },
    inApp: { ...local.inApp, title: dto.inAppTitle ?? '', body: dto.inAppBody ?? '' },
    push: { ...local.push, title: dto.pushTitle ?? '', body: dto.pushBody ?? '' }
  };
}

/** First readable message from a ValidationProblem / Conflict / network error. */
export function apiErrorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    if (body?.errors) {
      const first = Object.values(body.errors as Record<string, string[]>)[0];
      if (first?.length) return first[0];
    }
    if (typeof body?.message === 'string') return body.message;
    if (err.status === 0) return 'Could not reach the Administration API.';
  }
  return 'Something went wrong while talking to the Administration API.';
}

/**
 * Notification templates backed by the Administration API. New drafts live in memory until their first save
 * (POST); stored templates are updated with PUT and deleted with DELETE.
 */
@Injectable({ providedIn: 'root' })
export class NotificationTemplateService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly baseUrl = `${environment.administrationApiUrl}api/administration/notification-templates`;

  private readonly store = signal<NotificationTemplate[]>([]);
  readonly templates = this.store.asReadonly();
  /** Ids of templates that exist on the server. */
  private readonly persisted = new Set<string>();
  private nextId = 1;

  all(): NotificationTemplate[] {
    return this.store();
  }

  get(id: string): NotificationTemplate | undefined {
    return this.store().find((t) => t.id === id);
  }

  isPersisted(id: string): boolean {
    return this.persisted.has(id);
  }

  codeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    return this.store().some((t) => t.code.toUpperCase() === c && t.id !== exceptId);
  }

  /** Temporary id for a draft that has not been sent to the API yet. */
  newId(): string {
    return `NEW-${String(this.nextId++).padStart(4, '0')}`;
  }

  /** Loads the stored templates, keeping local content (message bodies etc.) of templates already in memory. */
  load(): Observable<NotificationTemplate[]> {
    const params = { tenantId: environment.tenantId, clientId: environment.clientId };
    return this.http.get<NotificationTemplateDto[]>(this.baseUrl, { params }).pipe(
      map((dtos) => dtos.map((dto) => toAppTemplate(dto, this.get(dto.templateId) ?? baseTemplate(dto)))),
      tap((loaded) => {
        loaded.forEach((t) => this.persisted.add(t.id));
        // Unsaved drafts stay on top of the stored list
        const drafts = this.store().filter((t) => !this.persisted.has(t.id));
        this.store.set([...drafts, ...loaded]);
      })
    );
  }

  /** Insert or replace (by id) locally; new templates go to the top. */
  save(t: NotificationTemplate): NotificationTemplate {
    const copy = structuredClone(t);
    this.store.update((list) => (list.some((x) => x.id === t.id) ? list.map((x) => (x.id === t.id ? copy : x)) : [copy, ...list]));
    return copy;
  }

  /** POSTs a local draft; on success the server record (with its own id) replaces the draft. */
  create(t: NotificationTemplate): Observable<NotificationTemplate> {
    const request = {
      ...toRequest(t),
      createdBy: this.auth.getUserEmail() || 'unknown',
      tenantId: environment.tenantId,
      clientId: environment.clientId
    };
    return this.http.post<NotificationTemplateDto>(this.baseUrl, request).pipe(
      map((dto) => toAppTemplate(dto, t)),
      tap((saved) => {
        this.persisted.add(saved.id);
        this.store.update((list) => (list.some((x) => x.id === t.id) ? list.map((x) => (x.id === t.id ? saved : x)) : [saved, ...list]));
      })
    );
  }

  /** PUTs a stored template and replaces it with the server record. */
  update(t: NotificationTemplate): Observable<NotificationTemplate> {
    return this.http.put<NotificationTemplateDto>(`${this.baseUrl}/${encodeURIComponent(t.id)}`, toRequest(t)).pipe(
      map((dto) => toAppTemplate(dto, t)),
      tap((saved) => this.store.update((list) => list.map((x) => (x.id === saved.id ? saved : x))))
    );
  }

  /** DELETEs a stored template; drafts that were never saved are only removed locally. */
  remove(id: string): Observable<void> {
    const request = this.persisted.has(id) ? this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(id)}`) : of(undefined);
    return request.pipe(
      tap(() => {
        this.persisted.delete(id);
        this.store.update((list) => list.filter((t) => t.id !== id));
      })
    );
  }
}
