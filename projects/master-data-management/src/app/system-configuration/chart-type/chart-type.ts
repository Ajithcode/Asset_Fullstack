import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-system-configuration-chart-type',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ChartTypePage {
  readonly config: MasterTableConfig = {
    key: 'system-configuration/chart-type',
    title: 'Chart Type',
    singular: 'Chart Type',
    codePrefix: 'CHT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'libraryKey', label: 'Library Key' },
      { key: 'bestFor', label: 'Best For' }
    ],
    rows: [
      { code: 'CHT-0001', name: 'Line', libraryKey: 'line', bestFor: 'Trends over time', status: 'Active' },
      { code: 'CHT-0002', name: 'Bar', libraryKey: 'bar', bestFor: 'Comparing categories', status: 'Active' },
      { code: 'CHT-0003', name: 'Donut', libraryKey: 'doughnut', bestFor: 'Share of a whole', status: 'Active' },
      { code: 'CHT-0004', name: 'Heatmap', libraryKey: 'heatmap', bestFor: 'Density by time and place', status: 'Active' }
    ]
  };
}
