import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { AssetCategoryService } from './asset-category';

describe('AssetCategoryService', () => {
  let service: AssetCategoryService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AssetCategoryService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
