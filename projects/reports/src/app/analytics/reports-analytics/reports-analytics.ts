import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { REPORTS_ANALYTICS_BASE_PATH, REPORTS_ANALYTICS_MENU } from 'shared-ui';

/** Reports & Analytics landing page: one card per sub module. */
@Component({
  standalone: true,
  selector: 'app-reports-analytics',
  imports: [RouterLink],
  templateUrl: './reports-analytics.html',
  styleUrls: ['./reports-analytics.css']
})
export class ReportsAnalytics {
  readonly basePath = REPORTS_ANALYTICS_BASE_PATH;
  readonly modules = REPORTS_ANALYTICS_MENU;
}
