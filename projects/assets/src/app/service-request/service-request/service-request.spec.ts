import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ServiceRequest } from './service-request';

describe('ServiceRequest', () => {
  let component: ServiceRequest;
  let fixture: ComponentFixture<ServiceRequest>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ServiceRequest],
      providers: [provideRouter([])],
    })
    .compileComponents();

    fixture = TestBed.createComponent(ServiceRequest);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
