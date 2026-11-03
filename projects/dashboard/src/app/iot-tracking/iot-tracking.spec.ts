import { ComponentFixture, TestBed } from '@angular/core/testing';

import { IotTracking } from './iot-tracking';

describe('IotTracking', () => {
  let component: IotTracking;
  let fixture: ComponentFixture<IotTracking>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IotTracking]
    })
    .compileComponents();

    fixture = TestBed.createComponent(IotTracking);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
