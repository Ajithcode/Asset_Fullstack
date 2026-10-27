import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-asset-manufacturer',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ManufacturerPage {
  readonly config: MasterTableConfig = {
    key: 'asset/manufacturer',
    title: 'Manufacturer',
    singular: 'Manufacturer',
    codePrefix: 'MFR',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'country', label: 'Country' },
      { key: 'website', label: 'Website' },
      { key: 'email', label: 'Support Email', type: 'email' }
    ],
    rows: [
      { code: 'MFR-0001', name: 'Cummins', country: 'USA', website: 'cummins.com', email: 'support@cummins.com', status: 'Active' },
      { code: 'MFR-0002', name: 'Carrier', country: 'USA', website: 'carrier.com', email: '', status: 'Active' },
      { code: 'MFR-0003', name: 'Schneider Electric', country: 'France', website: 'se.com', email: '', status: 'Active' }
    ]
  };
}
