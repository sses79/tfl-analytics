import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { DepartureBoard } from '../../models';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { ArrivalsComponent } from './arrivals.component';

describe('ArrivalsComponent', () => {
  let fixture: ComponentFixture<ArrivalsComponent>;
  const board: DepartureBoard = {
    stationId: '940GZZLUVIC', stationName: 'Victoria',
    observedAtUtc: '2026-08-02T12:00:00Z', isStale: true,
    destinations: [{ stationId: '940GZZLUKSX', stationName: "King's Cross", lineIds: ['victoria'] }],
    recommendations: [{
      lineId: 'victoria', lineName: 'Victoria', direction: 'inbound',
      platformName: 'Northbound - Platform 3', towards: 'Walthamstow Central',
      stopsUntilDestination: 5,
      stations: [
        { stationId: '940GZZLUVIC', stationName: 'Victoria', sequence: 0, isOrigin: true, isDestination: false },
        { stationId: '940GZZLUKSX', stationName: "King's Cross", sequence: 5, isOrigin: false, isDestination: true }
      ]
    }],
    disruptions: [{ lineId: 'victoria', lineName: 'Victoria', status: 'Minor Delays', reason: 'Signal failure', observedAtUtc: '2026-08-02T12:00:00Z' }],
    platforms: [{
      lineId: 'victoria', lineName: 'Victoria', direction: 'inbound',
      platformName: 'Northbound - Platform 3',
      trains: [{
        predictionId: 'prediction-1', vehicleId: 'vehicle-1',
        destinationStationId: '940GZZLUWWL', destinationName: 'Walthamstow Central',
        towards: 'Walthamstow Central', currentLocation: 'Approaching Victoria',
        expectedArrivalUtc: '2026-08-02T12:02:00Z', secondsToStation: 120,
        observedAtUtc: '2026-08-02T12:00:00Z', servesSelectedDestination: true,
        stopsUntilDestination: 5, predictionState: 'approachingStation',
        estimatedStationId: '940GZZLUVIC', predictionStateLabel: 'Approaching Victoria'
      }]
    }]
  };

  beforeEach(async () => {
    const api = {
      getStations: vi.fn(() => of([{ stationId: board.stationId, name: board.stationName }])),
      getDepartureBoard: vi.fn(() => of(board)),
      searchStations: vi.fn(() => of({ matches: [] })),
      getJourneys: vi.fn(() => of({ duplicateCountRemoved: 1, journeys: [{
        id: 'journey-1', labels: ['Recommended', 'Fastest'], durationMinutes: 18,
        departureUtc: '2026-08-02T12:05:00Z', arrivalUtc: '2026-08-02T12:23:00Z',
        changeCount: 0, walkingMinutes: 2, accessibilitySummary: null, disruptions: [],
        legs: [{ kind: 'transport', mode: 'tube', lineName: 'Victoria', towards: 'Walthamstow Central',
          fromStationId: board.stationId, fromName: 'Victoria', toStationId: '940GZZLUKSX',
          toName: "King's Cross", durationMinutes: 18, instruction: 'Victoria line to King’s Cross' }]
      }] }))
    };
    const realtime = { lastArrivalsBatchUpdate: signal(null) };

    await TestBed.configureTestingModule({
      imports: [ArrivalsComponent],
      providers: [
        { provide: ApiService, useValue: api },
        { provide: SignalRService, useValue: realtime }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ArrivalsComponent);
    fixture.detectChanges();
  });

  afterEach(() => fixture.destroy());

  it('shows stale state and passenger platform information', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.freshness--stale')).not.toBeNull();
    expect(element.querySelector('.platform-board')?.textContent).toContain('Northbound - Platform 3');
    expect(element.querySelector('.train-row__destination')?.textContent).toContain('Walthamstow Central');
  });

  it('shows route advice and train suitability after destination selection', () => {
    const input = fixture.nativeElement.querySelector('#destination-combobox') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    const directOption = fixture.nativeElement.querySelector('.destination-matches > button') as HTMLButtonElement;
    directOption.click();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.recommendation-card')?.textContent).toContain('5 stops');
    expect(element.querySelector('.train-row__decision--yes')?.textContent).toContain('Board this train');
    expect(element.querySelector('.journey-card')).toBeNull();
    (element.querySelector('.alternatives-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(element.querySelector('.journey-card')?.textContent).toContain('18 min');
    expect(element.querySelector('.journey-card')?.textContent).toContain('Direct');
    expect(element.querySelector('.train-position')).not.toBeNull();
  });

  it('supports keyboard destination selection through one combobox', () => {
    const input = fixture.nativeElement.querySelector('#destination-combobox') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();

    expect(input.getAttribute('role')).toBe('combobox');
    expect(input.getAttribute('aria-expanded')).toBe('true');
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    expect(input.value).toBe("King's Cross");
    expect(fixture.nativeElement.querySelector('#destination-listbox')).toBeNull();
  });

  it('does not select an undefined destination when no keyboard matches exist', () => {
    const component = fixture.componentInstance as unknown as {
      clearDestination(): void;
      openDestinations(): void;
      onDestinationKeydown(event: KeyboardEvent): void;
    };
    component.clearDestination();
    component.openDestinations();

    expect(() => {
      component.onDestinationKeydown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
      component.onDestinationKeydown(new KeyboardEvent('keydown', { key: 'Enter' }));
    }).not.toThrow();
  });
});
