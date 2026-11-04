import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { GeneratedReportService, ReportModule, ReportTemplate } from '../generated-report.service';

interface TreeNode {
  label: string;
  expanded?: boolean;
  active?: boolean;
  children?: TreeNode[];
}

type ReportFormat = 'url' | 'pdf' | 'csv';
type Recurrence = 'once' | 'daily' | 'weekly' | 'monthly';

@Component({
  standalone: true,
  selector: 'app-create-report',
  imports: [CommonModule, FormsModule],
  templateUrl: './create-report.html',
  styleUrls: ['./create-report.css']
})
export class CreateReport implements OnInit {
  constructor(private router: Router, private generatedReportService: GeneratedReportService) {}

  // ---- Breadcrumb navigation ----
  navigateToReport(): void {
    // Adjust the path below to match your app's actual route for the Report list page
    this.router.navigate(['/reports-analytics/generated-reports']);
  }

  // ---- Left navigation tree ----
  searchTerm = '';

  treeNodes: TreeNode[] = [
    {
      label: 'Track People',
      expanded: true,
      children: [
        {
          label: 'UAE',
          expanded: true,
          children: [
            {
              label: 'Oman',
              expanded: true,
              children: [
                {
                  label: 'Street One',
                  expanded: true,
                  children: [
                    {
                      label: 'Second Colony',
                      expanded: true,
                      children: [
                        {
                          label: 'Third Right',
                          expanded: true,
                          children: [{ label: 'Azy floor', active: true }]
                        }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
      ]
    }
  ];

  toggleNode(node: TreeNode): void {
    if (node.children?.length) {
      node.expanded = !node.expanded;
    }
  }

  // ---- Connection / status strip ----
  connectionStatus = { online: true };
  gatewayStats = {
    totalGateway: 24,
    fixedSensor: 118,
    mobileSensor: 46,
    alarms: 3
  };

  // ---- Templates and time ranges (GET /api/generated-reports/templates) ----
  modules: ReportModule[] = [];
  loadingTemplates = false;
  templatesError: string | null = null;
  saving = false;
  saveError: string | null = null;

  ngOnInit(): void {
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.loadingTemplates = true;
    this.templatesError = null;
    this.generatedReportService.getTemplates().subscribe({
      next: (catalog) => {
        this.modules = catalog.modules ?? [];
        const ranges = catalog.timeRanges ?? [];
        // Hour ranges stay as chips; the longer ones go in the dropdown.
        this.timeRangePresets = ranges.filter((range) => range.value.endsWith('h'));
        this.longTimeRanges = ranges.filter((range) => !range.value.endsWith('h'));
        if (catalog.weekdays?.length) this.weekDays = catalog.weekdays;
        this.loadingTemplates = false;
      },
      error: (err: Error) => {
        this.templatesError = err.message;
        this.loadingTemplates = false;
      }
    });
  }

  // ---- Time range ----
  timeRangePreset = '30d';

  timeRangePresets: { value: string; label: string }[] = [];
  longTimeRanges: { value: string; label: string }[] = [];

  get longTimeRangeValue(): string {
    return this.longTimeRanges.some((range) => range.value === this.timeRangePreset) ? this.timeRangePreset : '';
  }

  selectTimeRange(preset: string): void {
    if (preset) this.timeRangePreset = preset;
  }

  // ---- Report form state ----
  reportName = '';

  selectedModule: string | null = null;

  selectedTemplate = '';

  get templateOptions(): ReportTemplate[] {
    return this.modules.find((module) => module.name === this.selectedModule)?.templates ?? [];
  }

  get selectedTemplateInfo(): ReportTemplate | null {
    return this.templateOptions.find((template) => template.name === this.selectedTemplate) ?? null;
  }

  // ---- Format ----
  format: ReportFormat = 'url';
  downloadCsvAlso = false;

  // ---- Recurrence ----
  recurrence: Recurrence = 'once';
  dailyTime = '';
  weeklyDay = 'Monday';
  weeklyTime = '';
  monthlyDate = '';
  monthlyTime = '';

  weekDays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

  // ---- Share with ----
  shareWith = '';

  selectModule(value: string): void {
    this.selectedModule = value;
    this.selectedTemplate = '';
  }

  setFormat(value: ReportFormat): void {
    this.format = value;
  }

  setRecurrence(value: Recurrence): void {
    this.recurrence = value;
  }

  get isFormValid(): boolean {
    if (!this.reportName.trim() || !this.selectedModule || !this.selectedTemplateInfo?.available) return false;
    switch (this.recurrence) {
      case 'daily':
        return !!this.dailyTime;
      case 'weekly':
        return !!this.weeklyDay && !!this.weeklyTime;
      case 'monthly':
        return !!this.monthlyDate && !!this.monthlyTime;
      default:
        return true;
    }
  }

  private readonly recurrenceLabels: Record<Recurrence, string> = {
    once: 'Once',
    daily: 'Daily',
    weekly: 'Weekly',
    monthly: 'Monthly'
  };

  onCreateReport(): void {
    if (!this.isFormValid || this.saving) {
      return;
    }

    const scheduleTime = { once: null, daily: this.dailyTime, weekly: this.weeklyTime, monthly: this.monthlyTime }[this.recurrence];
    this.saving = true;
    this.saveError = null;
    this.generatedReportService
      .create({
        name: this.reportName.trim(),
        module: this.selectedModule!,
        template: this.selectedTemplate,
        timeRange: this.timeRangePreset,
        recurrence: this.recurrenceLabels[this.recurrence],
        scheduleTime,
        scheduleDay: this.recurrence === 'weekly' ? this.weeklyDay : null,
        scheduleDate: this.recurrence === 'monthly' ? this.monthlyDate : null,
        format: this.format.toUpperCase(),
        shareWith: this.shareWith
          .split(',')
          .map((email) => email.trim())
          .filter(Boolean)
      })
      .subscribe({
        next: () => {
          this.saving = false;
          this.router.navigate(['/reports-analytics/generated-reports']);
        },
        error: (err: Error) => {
          this.saving = false;
          this.saveError = err.message;
        }
      });
  }

  onCancel(): void {
    this.reportName = '';
    this.selectedModule = null;
    this.selectedTemplate = '';
    this.format = 'url';
    this.downloadCsvAlso = false;
    this.recurrence = 'once';
    this.dailyTime = '';
    this.weeklyDay = 'Monday';
    this.weeklyTime = '';
    this.monthlyDate = '';
    this.monthlyTime = '';
    this.shareWith = '';
    this.saveError = null;
  }
}