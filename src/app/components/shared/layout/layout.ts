import { AfterViewInit, Component, DestroyRef, ElementRef, HostBinding, NgZone, ViewChild, inject } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs';
import { Topbar } from '../topbar/topbar';
import { Sidebar } from '../sidebar/sidebar';
import { ScrollReveal } from './scroll-reveal';

@Component({
  standalone: true,
  selector: 'app-layout',
  imports: [CommonModule, RouterOutlet, Topbar, Sidebar],
  templateUrl: './layout.html',
  styleUrls: ['./layout.css']
})
export class Layout implements AfterViewInit {
  collapsed = false;

  @ViewChild('content', { static: true }) private content!: ElementRef<HTMLElement>;
  private readonly zone = inject(NgZone);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private reveal?: ScrollReveal;

  @HostBinding('class.sidebar-collapsed') get isSidebarCollapsed() { return this.collapsed; }

  ngAfterViewInit(): void {
    if (typeof window === 'undefined') return;
    // observers run outside Angular so they never trigger change detection
    this.zone.runOutsideAngular(() => {
      this.reveal = new ScrollReveal(this.content.nativeElement);
      this.reveal.start();
    });
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.reveal?.pageOpened());
    this.destroyRef.onDestroy(() => this.reveal?.stop());
  }

  onCollapseChange(value: boolean): void {
    this.collapsed = value;
  }

  onToggleSidebar(): void {
    this.collapsed = !this.collapsed;
  }
}
