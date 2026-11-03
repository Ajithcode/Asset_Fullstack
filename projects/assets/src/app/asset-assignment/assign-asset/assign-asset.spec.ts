import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AssignAsset } from './assign-asset';

describe('AssignAsset', () => {
  let component: AssignAsset;
  let fixture: ComponentFixture<AssignAsset>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssignAsset]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AssignAsset);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
