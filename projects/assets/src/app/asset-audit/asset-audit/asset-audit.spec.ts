import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AssetAudit } from './asset-audit';

describe('AssetAudit', () => {
  let component: AssetAudit;
  let fixture: ComponentFixture<AssetAudit>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssetAudit]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AssetAudit);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
