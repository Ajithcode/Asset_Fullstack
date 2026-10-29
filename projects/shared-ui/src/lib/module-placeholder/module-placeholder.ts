import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

// Temporary page for sidebar modules that don't have their own screens yet.
// The module name comes from the route's `data.title`.
@Component({
  standalone: true,
  selector: 'app-module-placeholder',
  template: `
    <div class="module-placeholder">
      <h2>{{ title }}</h2>
      <p>This module is coming soon.</p>
    </div>
  `,
  styles: [`
    .module-placeholder {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 8px;
      padding: 80px 24px;
      text-align: center;
      color: #6b6478;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .module-placeholder h2 {
      margin: 0;
      color: #211d2b;
      font-size: 20px;
    }
    .module-placeholder p {
      margin: 0;
      font-size: 14px;
    }
  `]
})
export class ModulePlaceholder {
  title = inject(ActivatedRoute).snapshot.data['title'] ?? 'Module';
}
