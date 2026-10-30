import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type NotificationLevel = 'info' | 'warning' | 'success' | 'error';

export interface AppNotification {
  id: string;
  message: string;
  timestamp: string;
  read: boolean;
  level: NotificationLevel;
  link: string;
}

// Feeds the topbar bell (see Topbar).
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private nextId = 1;

  private notificationsSubject = new BehaviorSubject<AppNotification[]>([]);

  watch() {
    return this.notificationsSubject.asObservable();
  }

  getAll(): AppNotification[] {
    return this.notificationsSubject.value;
  }

  unreadCount(): number {
    return this.notificationsSubject.value.filter((n) => !n.read).length;
  }

  push(message: string, level: NotificationLevel, link: string): void {
    const notification: AppNotification = {
      id: `ntf-${this.nextId++}`,
      message,
      timestamp: new Date().toISOString().slice(0, 16).replace('T', ' '),
      read: false,
      level,
      link
    };
    this.notificationsSubject.next([notification, ...this.notificationsSubject.value]);
  }

  markAllRead(): void {
    this.notificationsSubject.next(this.notificationsSubject.value.map((n) => ({ ...n, read: true })));
  }

  markRead(id: string): void {
    this.notificationsSubject.next(
      this.notificationsSubject.value.map((n) => (n.id === id ? { ...n, read: true } : n))
    );
  }
}
