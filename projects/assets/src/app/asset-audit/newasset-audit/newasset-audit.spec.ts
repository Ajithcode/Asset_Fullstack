import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NewassetAudit } from './newasset-audit';

describe('NewassetAudit', () => {
  let component: NewassetAudit;
  let fixture: ComponentFixture<NewassetAudit>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NewassetAudit]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NewassetAudit);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
