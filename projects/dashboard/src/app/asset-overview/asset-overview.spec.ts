import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AssetOverview } from './asset-overview';

describe('AssetOverview', () => {
  let component: AssetOverview;
  let fixture: ComponentFixture<AssetOverview>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssetOverview]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AssetOverview);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
