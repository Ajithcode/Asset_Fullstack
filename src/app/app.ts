import { Component, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { installMenuViewportGuard } from 'shared-ui';

@Component({
  standalone: true,
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class App {
  protected readonly title = signal('frontend');

  constructor() {
    // One guard for every module: action menus (⋮) stay inside the visible screen.
    if (isPlatformBrowser(inject(PLATFORM_ID))) installMenuViewportGuard();
  }
}
