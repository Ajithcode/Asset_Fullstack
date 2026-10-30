import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AssetAssignment } from './asset-assignment';

describe('AssetAssignment', () => {
  let component: AssetAssignment;
  let fixture: ComponentFixture<AssetAssignment>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssetAssignment]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AssetAssignment);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
