import { Component, ElementRef, HostListener, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, NavigationEnd, RouterModule } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../service/auth/auth.service';
import { environment } from '../../../../environments/environment';
import { WorkflowNotificationService } from '../../pages/administration/configuration/workflows/services/workflow-notification.service';
import { WorkflowNotification } from '../../pages/administration/configuration/workflows/models/workflow.model';

interface BreadcrumbItem {
  label: string;
  path: string;
}

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './topbar.html',
  styleUrls: ['./topbar.scss']
})
export class Topbar {
  @Output() toggleSidebar = new EventEmitter<void>();
  breadcrumbs: BreadcrumbItem[] = [];
  isDropdownOpen = false;
  flyoutPosition: { top: number; left: number } | null = null;

  isNotificationsOpen = false;
  notificationsFlyoutPosition: { top: number; left: number } | null = null;

  constructor(
    private router: Router,
    private elementRef: ElementRef,
    private auth: AuthService,
    private notificationService: WorkflowNotificationService
  ) {
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => this.updateBreadcrumbs());
  }

  get notifications(): WorkflowNotification[] {
    return this.notificationService.getAll();
  }

  get unreadNotificationCount(): number {
    return this.notificationService.unreadCount();
  }

  toggleNotifications(triggerEl: EventTarget | null): void {
    this.isNotificationsOpen = !this.isNotificationsOpen;
    if (this.isNotificationsOpen && triggerEl instanceof HTMLElement) {
      const rect = triggerEl.getBoundingClientRect();
      this.notificationsFlyoutPosition = {
        top: rect.bottom + window.scrollY + 4,
        left: rect.right - 340 + window.scrollX
      };
    } else {
      this.notificationsFlyoutPosition = null;
    }
  }

  openNotification(notification: WorkflowNotification): void {
    this.notificationService.markRead(notification.id);
    this.isNotificationsOpen = false;
    this.notificationsFlyoutPosition = null;
    this.router.navigateByUrl(notification.link);
  }

  markAllNotificationsRead(): void {
    this.notificationService.markAllRead();
  }

  get displayEmail(): string {
    return this.auth.getUserEmail() || environment.supportEmail;
  }

  get roleName(): string {
    return this.auth.getUser()?.roleName || 'Administrator';
  }

  /** Two letters for the avatar, from the signed-in user's name. */
  get initials(): string {
    const name = this.auth.getUser()?.userName || this.displayEmail.split('@')[0];
    const parts = name.split(/[\s._-]+/).filter(Boolean);
    const letters = parts.length > 1 ? parts[0][0] + parts[1][0] : name.slice(0, 2);
    return letters.toUpperCase() || 'AD';
  }

  toggleMenu(): void {
    this.toggleSidebar.emit();
  }

  toggleAdminDropdown(triggerEl: EventTarget | null): void {
    this.isDropdownOpen = !this.isDropdownOpen;
    if (this.isDropdownOpen && triggerEl instanceof HTMLElement) {
      const rect = triggerEl.getBoundingClientRect();
      this.flyoutPosition = {
        top: rect.bottom + window.scrollY + 4,
        left: rect.right - 200 + window.scrollX
      };
    } else {
      this.flyoutPosition = null;
    }
  }

  logout(): void {
    this.isDropdownOpen = false;
    this.flyoutPosition = null;
    this.auth.logout();
  }

  private updateBreadcrumbs(): void {
    const url = this.router.url;
    this.breadcrumbs = [];

    if (url.includes('/administration/user-management/user')) {
      this.breadcrumbs = [
        { label: 'Administration', path: '/administration' },
        { label: 'User Management', path: '/administration/user-management' },
        { label: 'User', path: '/administration/user-management/user' }
      ];
    } else if (url.includes('/administration/user-management/role')) {
      this.breadcrumbs = [
        { label: 'Administration', path: '/administration' },
        { label: 'User Management', path: '/administration/user-management' },
        { label: 'Role', path: '/administration/user-management/role' }
      ];
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!event.target) {
      return;
    }
    const clickedInside = this.elementRef.nativeElement.contains(event.target);

    if (this.isDropdownOpen) {
      const clickedInsideFlyout = !!(event.target as HTMLElement).closest('.admin-flyout');
      if (!clickedInside && !clickedInsideFlyout) {
        this.isDropdownOpen = false;
        this.flyoutPosition = null;
      }
    }

    if (this.isNotificationsOpen) {
      const clickedInsideNotifications = !!(event.target as HTMLElement).closest('.notifications-flyout');
      if (!clickedInside && !clickedInsideNotifications) {
        this.isNotificationsOpen = false;
        this.notificationsFlyoutPosition = null;
      }
    }
  }

  @HostListener('window:resize')
  @HostListener('window:scroll')
  onViewportChange(): void {
    if (this.isDropdownOpen) {
      this.isDropdownOpen = false;
      this.flyoutPosition = null;
    }
    if (this.isNotificationsOpen) {
      this.isNotificationsOpen = false;
      this.notificationsFlyoutPosition = null;
    }
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.isDropdownOpen) {
      this.isDropdownOpen = false;
      this.flyoutPosition = null;
    }
    if (this.isNotificationsOpen) {
      this.isNotificationsOpen = false;
      this.notificationsFlyoutPosition = null;
    }
  }
}
