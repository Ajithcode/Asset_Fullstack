import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-inventory-item',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ItemPage {
  readonly config: MasterTableConfig = {
    key: 'inventory/item',
    title: 'Item',
    singular: 'Item',
    codePrefix: 'ITM',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'category', label: 'Item Category', type: 'select', options: ['Filters', 'Belts', 'Electrical Spares', 'Lubricants'] },
      { key: 'uom', label: 'UOM', type: 'select', options: ['Nos', 'Kg', 'Litre', 'Metre', 'Box'] },
      { key: 'reorderLevel', label: 'Reorder Level', type: 'number' },
      { key: 'unitPrice', label: 'Unit Price', type: 'number' }
    ],
    rows: [
      { code: 'ITM-0001', name: 'AHU Pre-filter 24x24', category: 'Filters', uom: 'Nos', reorderLevel: 10, unitPrice: 850, status: 'Active' },
      { code: 'ITM-0002', name: 'V-Belt B52', category: 'Belts', uom: 'Nos', reorderLevel: 6, unitPrice: 420, status: 'Active' },
      { code: 'ITM-0003', name: 'Engine Oil 15W40', category: 'Lubricants', uom: 'Litre', reorderLevel: 40, unitPrice: 310, status: 'Active' }
    ]
  };
}
