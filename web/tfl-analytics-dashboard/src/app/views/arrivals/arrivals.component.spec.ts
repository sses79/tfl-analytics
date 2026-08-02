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
    platforms: [{
      lineId: 'victoria', lineName: 'Victoria', direction: 'inbound',
      platformName: 'Northbound - Platform 3',
      trains: [{
        predictionId: 'prediction-1', vehicleId: 'vehicle-1',
        destinationStationId: '940GZZLUWWL', destinationName: 'Walthamstow Central',
        towards: 'Walthamstow Central', currentLocation: 'Approaching Victoria',
        expectedArrivalUtc: '2026-08-02T12:02:00Z', secondsToStation: 120,
        observedAtUtc: '2026-08-02T12:00:00Z', servesSelectedDestination: true,
        stopsUntilDestination: 5
      }]
    }]
  };

  beforeEach(async () => {
    const api = {
      getStations: vi.fn(() => of([{ stationId: board.stationId, name: board.stationName }])),
      getDepartureBoard: vi.fn(() => of(board))
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
    const select = fixture.nativeElement.querySelector('#destination') as HTMLSelectElement;
    select.value = '940GZZLUKSX';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.recommendation-card')?.textContent).toContain('5 stops');
    expect(element.querySelector('.train-row__decision--yes')?.textContent).toContain('Board this train');
  });
});
