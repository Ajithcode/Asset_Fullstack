import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { AssetTypes } from './asset-types';

describe('AssetTypes', () => {
  let service: AssetTypes;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AssetTypes);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
