import { TestBed } from '@angular/core/testing';

import { AssetDropdownService } from './asset-dropdown';

describe('AssetDropdownService', () => {
  let service: AssetDropdownService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AssetDropdownService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
