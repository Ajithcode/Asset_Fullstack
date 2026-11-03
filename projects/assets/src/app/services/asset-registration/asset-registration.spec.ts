import { TestBed } from '@angular/core/testing';

import { AssetRegistrationService } from './asset-registration';

describe('AssetRegistrationService', () => {
  let service: AssetRegistrationService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AssetRegistrationService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
