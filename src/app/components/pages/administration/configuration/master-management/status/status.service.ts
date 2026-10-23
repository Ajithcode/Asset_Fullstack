import { Injectable } from '@angular/core';
import { MasterManagementStatusItem } from './status.model';

@Injectable({ providedIn: 'root' })
export class MasterManagementStatusService {
  readonly statusNameMaster: string[] = ['Open', 'In Progress', 'On Hold', 'Resolved', 'Closed', 'Cancelled'];

  private readonly records: MasterManagementStatusItem[] = [
    {
      statusId: 'STA-1001',
      assetId: 'AST-1001',
      assetName: 'HVAC Unit 1',
      statusName: 'Open',
      colorCode: '#1d4ed8',
      allowedTransitions: ['In Progress', 'Cancelled'],
      isActive: true
    },
    {
      statusId: 'STA-1002',
      assetId: 'AST-1002',
      assetName: 'Fire Panel A',
      statusName: 'In Progress',
      colorCode: '#b8860b',
      allowedTransitions: ['On Hold', 'Resolved', 'Cancelled'],
      isActive: true
    },
    {
      statusId: 'STA-1003',
      assetId: 'AST-1003',
      assetName: 'Chiller Pump 2',
      statusName: 'On Hold',
      colorCode: '#4f2bd8',
      allowedTransitions: ['In Progress', 'Cancelled'],
      isActive: true
    },
    {
      statusId: 'STA-1004',
      assetId: 'AST-1001',
      assetName: 'HVAC Unit 1',
      statusName: 'Resolved',
      colorCode: '#1e7e34',
      allowedTransitions: ['Closed', 'In Progress'],
      isActive: true
    },
    {
      statusId: 'STA-1005',
      assetId: 'AST-1002',
      assetName: 'Fire Panel A',
      statusName: 'Closed',
      colorCode: '#64748b',
      allowedTransitions: [],
      isActive: true
    },
    {
      statusId: 'STA-1006',
      assetId: 'AST-1003',
      assetName: 'Chiller Pump 2',
      statusName: 'Cancelled',
      colorCode: '#c0221f',
      allowedTransitions: [],
      isActive: true
    }
  ];

  private nextSequence = 1007;

  getRecords(): MasterManagementStatusItem[] {
    return this.records;
  }

  addRecord(record: MasterManagementStatusItem): MasterManagementStatusItem {
    const statusId = record.statusId?.trim() || `STA-${this.nextSequence++}`;
    const created: MasterManagementStatusItem = { ...record, statusId };
    this.records.push(created);
    return created;
  }

  updateRecord(statusId: string, changes: MasterManagementStatusItem): void {
    const index = this.records.findIndex((r) => r.statusId === statusId);
    if (index !== -1) {
      this.records[index] = { ...this.records[index], ...changes };
    }
  }

  deleteRecords(statusIds: string[]): void {
    for (const id of statusIds) {
      const index = this.records.findIndex((r) => r.statusId === id);
      if (index !== -1) {
        this.records.splice(index, 1);
      }
    }
  }

  search(term: string): MasterManagementStatusItem[] {
    const value = term.trim().toLowerCase();
    if (!value) return this.records;
    return this.records.filter((r) =>
      Object.values(r).some((v) => String(v).toLowerCase().includes(value))
    );
  }
}
