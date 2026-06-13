import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../environments/environment';
import { App } from './app';

describe('App', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    fixture.destroy();
  });

  it('renders live line status data', () => {
    fixture.detectChanges();

    http.expectOne(`${environment.apiBaseUrl}/health/live`).flush({ status: 'healthy' });
    http
      .expectOne(request => request.url.includes('/api/tfl/line-status/'))
      .flush([
        {
          id: 'victoria',
          name: 'Victoria',
          modeName: 'tube',
          lineStatuses: [
            {
              statusSeverity: 10,
              statusSeverityDescription: 'Good Service'
            }
          ]
        }
      ]);

    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('How is London moving?');
    expect(compiled.querySelector('.line-name h3')?.textContent).toContain('Victoria');
    expect(compiled.querySelector('.status-pill')?.textContent).toContain('Good Service');
    expect(compiled.querySelector('.api-state')?.textContent).toContain('API online');
  });

  it('shows a recoverable error when line data fails', () => {
    fixture.detectChanges();

    http.expectOne(`${environment.apiBaseUrl}/health/live`).flush({ status: 'healthy' });
    http
      .expectOne(request => request.url.includes('/api/tfl/line-status/'))
      .flush('Unavailable', { status: 503, statusText: 'Service Unavailable' });

    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('[role="alert"]')?.textContent).toContain(
      'Live TfL data is temporarily unavailable'
    );
  });
});
