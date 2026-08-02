import { Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { DepartureBoard, PassengerTrain, StationSummary } from '../../models';
import {
  DataFlowExplainerComponent,
  DataFlowStep
} from '../../components/data-flow-explainer/data-flow-explainer.component';

@Component({
  selector: 'app-arrivals',
  imports: [FormsModule, DataFlowExplainerComponent],
  templateUrl: './arrivals.component.html',
  styleUrl: './arrivals.component.scss'
})
export class ArrivalsComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly signalR = inject(SignalRService);
  private readonly clock = window.setInterval(() => this.now.set(Date.now()), 1000);

  protected readonly stations = signal<StationSummary[]>([]);
  protected readonly board = signal<DepartureBoard | null>(null);
  protected readonly selectedStation = signal('');
  protected readonly selectedDestination = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly now = signal(Date.now());
  protected readonly flowSteps: readonly DataFlowStep[] = [
    { service: 'TfL Arrivals API', detail: 'Live predictions and passenger train state', tone: 'source' },
    { service: 'PollArrivals', detail: 'Five-minute baseline with batched observations', tone: 'compute' },
    { service: 'Cosmos + archive', detail: 'Latest snapshot plus durable raw evidence', tone: 'storage' },
    { service: 'Route topology cache', detail: 'TfL branches cached for 24 hours', tone: 'compute' },
    { service: 'Departure-board API', detail: 'Matches destination, direction and platform', tone: 'api' },
    { service: 'SignalR', detail: 'One batch refresh notification', tone: 'messaging' },
    { service: 'Passenger board', detail: 'Choose destination and train', tone: 'ui' }
  ];

  private static readonly StationNames: Record<string, string> = {
    '940GZZLUVIC': 'Victoria',
    '940GZZLUOXC': 'Oxford Circus',
    '940GZZLUGPK': 'Green Park',
    '940GZZLUKSX': "King's Cross St. Pancras",
    '940GZZLULNB': 'London Bridge'
  };

  constructor() {
    effect(() => {
      const batch = this.signalR.lastArrivalsBatchUpdate();
      const station = this.selectedStation();
      if (batch?.arrivals.some(arrival => arrival.stationId === station)) {
        this.loadBoard(false);
      }
    });
  }

  ngOnInit(): void {
    this.api.getStations().subscribe({
      next: stations => {
        this.stations.set(stations);
        if (stations.length > 0) {
          this.selectedStation.set(stations[0].stationId);
          this.loadBoard();
        }
      },
      error: () => this.error.set('Unable to load station list.')
    });
  }

  ngOnDestroy(): void {
    window.clearInterval(this.clock);
  }

  protected onStationChange(stationId: string): void {
    this.selectedStation.set(stationId);
    this.selectedDestination.set('');
    this.loadBoard();
  }

  protected onDestinationChange(stationId: string): void {
    this.selectedDestination.set(stationId);
    this.loadBoard();
  }

  protected stationLabel(station: StationSummary): string {
    return station.name
      ?? ArrivalsComponent.StationNames[station.stationId]
      ?? station.stationId;
  }

  protected loadBoard(showLoader = true): void {
    const station = this.selectedStation();
    if (!station) return;
    if (showLoader) this.loading.set(true);
    this.error.set(null);
    this.api.getDepartureBoard(station, this.selectedDestination() || undefined).subscribe({
      next: board => {
        this.board.set(board);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load the live departure board.');
        this.loading.set(false);
      }
    });
  }

  protected countdown(train: PassengerTrain): string {
    if (!train.expectedArrivalUtc) return this.secondsLabel(train.secondsToStation);
    const seconds = Math.max(
      0,
      Math.round((new Date(train.expectedArrivalUtc).getTime() - this.now()) / 1000)
    );
    if (seconds === 0) return 'Due';
    if (seconds < 60) return `${seconds}s`;
    return `${Math.ceil(seconds / 60)} min`;
  }

  protected freshnessLabel(): string {
    const observed = this.board()?.observedAtUtc;
    if (!observed) return 'No live observation';
    const seconds = Math.max(0, Math.round((this.now() - new Date(observed).getTime()) / 1000));
    if (seconds < 60) return `Updated ${seconds}s ago`;
    return `Updated ${Math.floor(seconds / 60)}m ago`;
  }

  protected trainState(train: PassengerTrain): string {
    return train.currentLocation || train.towards || 'Location not reported';
  }

  private secondsLabel(seconds: number): string {
    if (seconds < 60) return `${Math.max(0, seconds)}s`;
    return `${Math.ceil(seconds / 60)} min`;
  }
}
