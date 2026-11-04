import { TestBed } from '@angular/core/testing';

import { AssetHierarchy } from './asset-hierarchy';

describe('AssetHierarchy', () => {
  let service: AssetHierarchy;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AssetHierarchy);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
