import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-inventory-item-category',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ItemCategoryPage {
  readonly config: MasterTableConfig = {
    key: 'inventory/item-category',
    title: 'Item Category',
    singular: 'Item Category',
    codePrefix: 'ICAT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'parent', label: 'Parent Category' },
      { key: 'description', label: 'Description' }
    ],
    rows: [
      { code: 'ICAT-0001', name: 'Filters', parent: '-', description: '', status: 'Active' },
      { code: 'ICAT-0002', name: 'Belts', parent: '-', description: '', status: 'Active' },
      { code: 'ICAT-0003', name: 'Electrical Spares', parent: '-', description: '', status: 'Active' },
      { code: 'ICAT-0004', name: 'Lubricants', parent: '-', description: '', status: 'Active' }
    ]
  };
}
