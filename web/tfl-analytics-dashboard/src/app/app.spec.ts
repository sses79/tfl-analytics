import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../environments/environment';
import { App } from './app';
import { SignalRService } from './services/signalr.service';

describe('App', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;
  let signalR: {
    connected: ReturnType<typeof signal<boolean>>;
    start: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    signalR = {
      connected: signal(false),
      start: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SignalRService, useValue: signalR }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    fixture.destroy();
  });

  it('renders the application shell and reports API health', () => {
    fixture.detectChanges();

    http.expectOne(`${environment.apiBaseUrl}/health/live`).flush({ status: 'healthy' });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand strong')?.textContent).toContain('TfL Analytics');
    expect(compiled.querySelectorAll('.mainnav__link')).toHaveLength(4);
    expect(compiled.querySelector('.api-state')?.textContent).toContain('API online');
    expect(signalR.start).toHaveBeenCalledOnce();
  });

  it('shows disconnected states when API health fails', () => {
    fixture.detectChanges();

    http
      .expectOne(`${environment.apiBaseUrl}/health/live`)
      .flush('Unavailable', { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const states = Array.from(compiled.querySelectorAll('.api-state')).map(element =>
      element.textContent?.trim()
    );

    expect(states).toContain('API checking');
    expect(states).toContain('Live off');
  });
});
