import { Routes } from '@angular/router';
import { OverviewPage } from './overview/overview';
import { NewInspectionPage } from './new-inspection/new-inspection';
import { PlansSchedulesPage } from './plans-schedules/plans-schedules';
import { InspectionTypesPage } from './inspection-types/inspection-types';
import { InspectionTasksPage } from './inspection-tasks/inspection-tasks';
import { ChecklistsPage } from './checklists/checklists';
import { ExecutionPage } from './execution/execution';
import { FindingsPage } from './findings/findings';
import { CalibrationPage } from './calibration/calibration';
import { SignaturesStampsPage } from './signatures-stamps/signatures-stamps';
import { NotificationTemplatesPage } from './notification-templates/notification-templates';
import { ReportTemplatesPage } from './report-templates/report-templates';
import { NumberingSequencesPage } from './numbering-sequences/numbering-sequences';
import { WorkingCalendarPage } from './working-calendar/working-calendar';

// Mounted by the shell at /inspection; paths match the Inspection menu in the shell's sidebar
export const routes: Routes = [
  { path: '', redirectTo: 'overview', pathMatch: 'full' },
  { path: 'overview', component: OverviewPage },
  // New inspection wizard; ?id=<mongo id> reopens a draft
  { path: 'new', component: NewInspectionPage },
  { path: 'plans-schedules', component: PlansSchedulesPage },
  { path: 'inspection-types', component: InspectionTypesPage },
  { path: 'tasks', component: InspectionTasksPage },
  { path: 'checklists', component: ChecklistsPage },
  // Executes one inspection; ?code=INS-2026-0148 picks which
  { path: 'execution', component: ExecutionPage },
  { path: 'findings', component: FindingsPage },
  { path: 'signatures-stamps', component: SignaturesStampsPage },
  { path: 'notification-templates', component: NotificationTemplatesPage },
  { path: 'report-templates', component: ReportTemplatesPage },
  { path: 'numbering-sequences', component: NumberingSequencesPage },
  { path: 'working-calendar', component: WorkingCalendarPage },
  { path: 'calibration', component: CalibrationPage }
];
