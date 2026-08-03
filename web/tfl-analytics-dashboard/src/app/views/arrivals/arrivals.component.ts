import { Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { DepartureBoard, JourneyPlan, PassengerTrain, RouteRecommendation, StationSummary, StopPointSearchMatch } from '../../models';
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
  protected readonly journeyPlan = signal<JourneyPlan | null>(null);
  protected readonly journeyLoading = signal(false);
  protected readonly journeyPreference = signal('leastinterchange');
  protected readonly stepFree = signal(false);
  protected readonly destinationQuery = signal('');
  protected readonly destinationMatches = signal<StopPointSearchMatch[]>([]);
  protected readonly searchedDestination = signal<StopPointSearchMatch | null>(null);
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
    this.searchedDestination.set(null);
    this.selectedDestination.set(stationId);
    this.loadBoard();
    this.loadJourneys();
  }

  protected searchDestinations(): void {
    const query = this.destinationQuery().trim();
    if (query.length < 2) return;
    this.api.searchStations(query).subscribe({
      next: result => this.destinationMatches.set(result.matches.slice(0, 8)),
      error: () => this.destinationMatches.set([])
    });
  }

  protected selectSearchedDestination(match: StopPointSearchMatch): void {
    this.searchedDestination.set(match);
    this.destinationMatches.set([]);
    this.selectedDestination.set(match.id);
    this.loadBoard();
    this.loadJourneys();
  }

  protected onJourneyPreferenceChange(preference: string): void {
    this.journeyPreference.set(preference);
    this.loadJourneys();
  }

  protected onStepFreeChange(enabled: boolean): void {
    this.stepFree.set(enabled);
    this.loadJourneys();
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
    return train.predictionStateLabel || train.currentLocation || train.towards || 'Location not reported';
  }

  protected trainsAtStation(route: RouteRecommendation, stationId: string): PassengerTrain[] {
    return (this.board()?.platforms ?? [])
      .filter(platform => platform.lineId === route.lineId && platform.direction === route.direction)
      .flatMap(platform => platform.trains)
      .filter(train => train.estimatedStationId === stationId)
      .slice(0, 3);
  }

  protected journeyChanges(legs: JourneyPlan['journeys'][number]['legs']): number {
    return Math.max(0, legs.filter(leg => leg.mode?.name !== 'walking').length - 1);
  }

  protected loadJourneys(): void {
    const destination = this.selectedDestination();
    if (!destination) {
      this.journeyPlan.set(null);
      return;
    }
    this.journeyLoading.set(true);
    this.api.getJourneys(
      this.selectedStation(),
      destination,
      this.journeyPreference(),
      this.stepFree() ? ['stepFreeToPlatform'] : []
    ).subscribe({
      next: plan => {
        this.journeyPlan.set(plan);
        this.journeyLoading.set(false);
      },
      error: () => {
        this.journeyPlan.set(null);
        this.journeyLoading.set(false);
      }
    });
  }

  private secondsLabel(seconds: number): string {
    if (seconds < 60) return `${Math.max(0, seconds)}s`;
    return `${Math.ceil(seconds / 60)} min`;
  }
}
