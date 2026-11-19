import { TestBed } from '@angular/core/testing';

import { AssetBulkImport } from './asset-bulk-import';

describe('AssetBulkImport', () => {
  let service: AssetBulkImport;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AssetBulkImport);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
