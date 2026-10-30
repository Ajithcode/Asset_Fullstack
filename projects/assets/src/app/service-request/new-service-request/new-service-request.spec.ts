import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { NewServiceRequest } from './new-service-request';

describe('NewServiceRequest', () => {
  let component: NewServiceRequest;
  let fixture: ComponentFixture<NewServiceRequest>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NewServiceRequest],
      providers: [provideRouter([])],
    })
    .compileComponents();

    fixture = TestBed.createComponent(NewServiceRequest);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
