import { Component, NgZone, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { ToastHost, installMenuViewportGuard } from 'shared-ui';

@Component({
  standalone: true,
  selector: 'app-root',
  imports: [RouterOutlet, ToastHost],
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class App {
  protected readonly title = signal('frontend');

  constructor() {
    // One guard for every module: action menus (⋮) stay inside the visible screen.
    // Outside the Angular zone: its observer/listeners/frames must not trigger app-wide change detection.
    if (isPlatformBrowser(inject(PLATFORM_ID))) inject(NgZone).runOutsideAngular(() => installMenuViewportGuard());
  }
}
