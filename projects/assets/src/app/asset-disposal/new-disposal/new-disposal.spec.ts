import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NewDisposal } from './new-disposal';

describe('NewDisposal', () => {
  let component: NewDisposal;
  let fixture: ComponentFixture<NewDisposal>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NewDisposal]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NewDisposal);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
