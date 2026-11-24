import { Component } from '@angular/core';

@Component({
  standalone: true,
  selector: 'app-section-unavailable',
  template: `
    <div class="section-unavailable">
      <h2>This section is temporarily unavailable</h2>
      <p>We couldn't load this part of the app. Try refreshing, or come back later.</p>
    </div>
  `,
  styles: [`
    .section-unavailable {
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
    .section-unavailable h2 {
      margin: 0;
      color: #211d2b;
      font-size: 1.25rem;
    }
    .section-unavailable p {
      margin: 0;
      font-size: 0.875rem;
    }
  `]
})
export class SectionUnavailable {}
