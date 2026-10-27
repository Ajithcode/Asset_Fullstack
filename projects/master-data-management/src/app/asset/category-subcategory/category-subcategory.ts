import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-asset-category-subcategory',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class CategorySubcategoryPage {
  readonly config: MasterTableConfig = {
    key: 'asset/category-subcategory',
    title: 'Category / Subcategory',
    singular: 'Category',
    codePrefix: 'CAT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'parent', label: 'Parent Category' },
      { key: 'description', label: 'Description', type: 'textarea', hideInTable: true }
    ],
    rows: [
      { code: 'CAT-0001', name: 'Electrical', parent: '-', description: '', status: 'Active' },
      { code: 'CAT-0002', name: 'Generators', parent: 'Electrical', description: '', status: 'Active' },
      { code: 'CAT-0003', name: 'HVAC', parent: '-', description: '', status: 'Active' },
      { code: 'CAT-0004', name: 'Chillers', parent: 'HVAC', description: '', status: 'Active' }
    ]
  };
}
