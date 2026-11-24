import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Lock, LucideAngularModule } from 'lucide-angular';

/** Shared page for modules that are not part of the licence (see licence.config.ts). */
@Component({
  standalone: true,
  selector: 'app-module-locked',
  imports: [LucideAngularModule],
  template: `
    <div class="module-locked">
      <div class="module-locked__icon">
        <lucide-icon [img]="lockIcon" [size]="32" [strokeWidth]="2"></lucide-icon>
      </div>
      <h2>Module not activated</h2>
      <p>Activate license – contact administrator.</p>
      <button type="button" class="module-locked__btn" (click)="goToDashboard()">Go to Dashboard</button>
    </div>
  `,
  styles: [`
    .module-locked {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 10px;
      padding: 96px 24px;
      text-align: center;
      color: var(--piq-text-secondary, #6b7280);
    }
    .module-locked__icon {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 72px;
      height: 72px;
      margin-bottom: 6px;
      border-radius: 50%;
      background: var(--piq-primary-light, #ede9fe);
      color: var(--piq-primary, #4f2bd8);
    }
    .module-locked h2 {
      margin: 0;
      color: var(--piq-text-brand, #3b0764);
      font-size: 1.25rem;
      font-weight: 600;
    }
    .module-locked p {
      margin: 0;
      font-size: 0.875rem;
    }
    .module-locked__btn {
      margin-top: 12px;
      padding: 9px 20px;
      border: none;
      border-radius: 8px;
      background: var(--piq-primary, #4f2bd8);
      color: #fff;
      font-size: 0.875rem;
      font-weight: 500;
      cursor: pointer;
    }
    .module-locked__btn:hover {
      background: var(--piq-primary-dark, #3d20b0);
    }
  `]
})
export class ModuleLocked {
  readonly lockIcon = Lock;
  private readonly router = inject(Router);

  goToDashboard(): void {
    this.router.navigateByUrl('/dashboard');
  }
}
