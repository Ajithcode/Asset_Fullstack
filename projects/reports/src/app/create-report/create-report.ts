import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { buildSampleRows, GeneratedReportService, TEMPLATE_HEADERS } from '../generated-report.service';

interface TreeNode {
  label: string;
  expanded?: boolean;
  active?: boolean;
  children?: TreeNode[];
}

type ReportModule =
  | 'asset-tracking'
  | 'asset-management'
  | 'maintenance'
  | 'inspection'
  | 'wip';
 
const TEMPLATE_OPTIONS_BY_MODULE: Record<ReportModule, string[]> = {
  'asset-tracking': [
    'Asset Location & Movement Report',
    'Asset Location History / Trail Report',
    'Asset Missing / Not-Seen Report',
    'Asset Movement & Exception Report',
    'Asset Utilization & Dwell-Time Report'
  ],
  'asset-management': [
    'Asset Register Report',
    'Asset Lifecycle Report',
    'Asset Allocation & Custodian Report',
    'Asset Valuation & Depreciation Report',
    'Warranty / AMC / Contract Expiry Report'
  ],
  maintenance: [
    'Preventive Maintenance Compliance Report',
    'Maintenance Work Order Report',
    'Asset Maintenance History Report',
    'Breakdown & Reliability Report',
    'Maintenance Cost Report'
  ],
  inspection: [
    'Detailed Inspection Report / Certificate',
    'Inspection Compliance Summary Report',
    'Failed Inspection & Findings Report',
    'Corrective Action & Reinspection Report',
    'Inspection & Approval Audit Trail Report'
  ],
  wip: [
    'WIP Status Summary Report',
    'WIP Ageing Report',
    'WIP Movement & Stage History Report',
    'WIP Delay / Bottleneck Report',
    'WIP Completion & Turnaround-Time Report'
  ]
};

type ReportFormat = 'url' | 'pdf' | 'csv';
type Recurrence = 'once' | 'daily' | 'weekly' | 'monthly';
type TimeRangePreset = '1h' | '2h' | '4h' | '8h' | '24h' | 'custom';

@Component({
  standalone: true,
  selector: 'app-create-report',
  imports: [CommonModule, FormsModule],
  templateUrl: './create-report.html',
  styleUrls: ['./create-report.css']
})
export class CreateReport {
  constructor(private router: Router, private generatedReportService: GeneratedReportService) {}

  // ---- Breadcrumb navigation ----
  navigateToReport(): void {
    // Adjust the path below to match your app's actual route for the Report list page
    this.router.navigate(['/report']);
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

  // ---- Time range ----
  timeRangePreset: TimeRangePreset = '2h';

  timeRangePresets: { value: TimeRangePreset; label: string }[] = [
    { value: '1h', label: '1 Hours' },
    { value: '2h', label: '2 Hours' },
    { value: '4h', label: '4 Hours' },
    { value: '8h', label: '8 Hours' },
    { value: '24h', label: '24 Hours' }
  ];

  selectTimeRange(preset: TimeRangePreset): void {
    this.timeRangePreset = preset;
  }

  // ---- Report form state ----
  reportName = '';

  selectedModule: ReportModule | null = null;

  moduleOptions: { value: ReportModule; label: string }[] = [
    { value: 'asset-tracking', label: 'Asset tracking' },
    { value: 'asset-management', label: 'Asset Management' },
    { value: 'maintenance', label: 'Maintanance management' },
    { value: 'inspection', label: 'Inspection Management' },
    { value: 'wip', label: 'Work in Progress' },
  ];

  selectedTemplate = '';

  get templateOptions(): string[] {
    return this.selectedModule ? TEMPLATE_OPTIONS_BY_MODULE[this.selectedModule] : [];
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

  selectModule(value: ReportModule): void {
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
    return !!this.reportName.trim() && !!this.selectedModule;
  }

  private readonly recurrenceLabels: Record<Recurrence, string> = {
    once: 'Once',
    daily: 'Daily',
    weekly: 'Weekly',
    monthly: 'Monthly'
  };

  onCreateReport(): void {
    if (!this.isFormValid) {
      return;
    }

    const headers = this.selectedTemplate ? TEMPLATE_HEADERS[this.selectedTemplate] ?? [] : [];
    const rows = buildSampleRows(headers, 5);
    const today = new Date().toISOString().slice(0, 10);
    const timeRangeLabel = this.timeRangePresets.find((p) => p.value === this.timeRangePreset)?.label ?? '';

    this.generatedReportService.addReport({
      title: this.reportName.trim(),
      timeRange: timeRangeLabel,
      expireOn: '',
      recurrence: this.recurrenceLabels[this.recurrence],
      createdOn: today,
      type: this.format.toUpperCase(),
      shareWith: this.shareWith
        .split(',')
        .map((email) => email.trim())
        .filter(Boolean)
        .join(', '),
      generatedOn: today,
      status: 'Completed',
      headers,
      rows
    });

    this.router.navigate(['/report']);
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
  }
}