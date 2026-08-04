import { Component, OnDestroy, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, takeUntil, tap } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import {
  DepartureBoard,
  PassengerJourney,
  PassengerJourneyPlan,
  PassengerStationMatch,
  PassengerTrain,
  RouteRecommendation,
  StationSummary
} from '../../models';
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
  private readonly destinationSearch = new Subject<string>();
  private readonly destroyed = new Subject<void>();

  protected readonly stations = signal<StationSummary[]>([]);
  protected readonly board = signal<DepartureBoard | null>(null);
  protected readonly selectedStation = signal('');
  protected readonly selectedDestination = signal('');
  protected readonly selectedDestinationName = signal('');
  protected readonly loading = signal(false);
  protected readonly journeyPlan = signal<PassengerJourneyPlan | null>(null);
  protected readonly journeyLoading = signal(false);
  protected readonly journeyError = signal<string | null>(null);
  protected readonly journeyPreference = signal('leastinterchange');
  protected readonly stepFree = signal(false);
  protected readonly showAlternatives = signal(false);
  protected readonly expandedJourneyId = signal<string | null>(null);
  protected readonly destinationQuery = signal('');
  protected readonly destinationMatches = signal<PassengerStationMatch[]>([]);
  protected readonly destinationOpen = signal(false);
  protected readonly destinationSearchState = signal<'idle' | 'loading' | 'ready' | 'empty' | 'error'>('idle');
  protected readonly activeDestinationIndex = signal(-1);
  protected readonly error = signal<string | null>(null);
  protected readonly now = signal(Date.now());
  protected readonly directSuggestions = computed<PassengerStationMatch[]>(() =>
    (this.board()?.destinations ?? []).slice(0, 8).map(destination => ({
      stationId: destination.stationId,
      displayName: destination.stationName,
      modes: ['tube'],
      lines: destination.lineIds,
      isDirect: true
    }))
  );
  protected readonly visibleDestinationMatches = computed(() =>
    this.destinationQuery().trim().length < 2 ? this.directSuggestions() : this.destinationMatches()
  );
  protected readonly directMatches = computed(() => this.visibleDestinationMatches().filter(match => match.isDirect));
  protected readonly interchangeMatches = computed(() => this.visibleDestinationMatches().filter(match => !match.isDirect));
  protected readonly hasDirectRecommendation = computed(() => (this.board()?.recommendations.length ?? 0) > 0);
  protected readonly flowSteps: readonly DataFlowStep[] = [
    { service: 'TfL Arrivals API', detail: 'Live predictions and passenger train state', tone: 'source' },
    { service: 'PollArrivals', detail: 'Five-minute baseline with batched observations', tone: 'compute' },
    { service: 'Cosmos + archive', detail: 'Latest snapshot plus durable raw evidence', tone: 'storage' },
    { service: 'Route topology cache', detail: 'TfL branches cached for 24 hours', tone: 'compute' },
    { service: 'Passenger APIs', detail: 'Normalize stations, routes and alternatives', tone: 'api' },
    { service: 'SignalR', detail: 'One batch refresh notification', tone: 'messaging' },
    { service: 'Passenger board', detail: 'Choose destination, route and train', tone: 'ui' }
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
    this.destinationSearch.pipe(
      debounceTime(275),
      distinctUntilChanged(),
      tap(() => this.destinationSearchState.set('loading')),
      switchMap(query => this.api.searchStations(query, this.selectedStation()).pipe(
        catchError(() => {
          this.destinationSearchState.set('error');
          return of({ matches: [] });
        })
      )),
      takeUntil(this.destroyed)
    ).subscribe(result => {
      this.destinationMatches.set(result.matches);
      this.activeDestinationIndex.set(result.matches.length > 0 ? 0 : -1);
      if (this.destinationSearchState() !== 'error') {
        this.destinationSearchState.set(result.matches.length > 0 ? 'ready' : 'empty');
      }
    });

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
    this.destroyed.next();
    this.destroyed.complete();
  }

  protected onStationChange(stationId: string): void {
    this.selectedStation.set(stationId);
    this.clearDestination();
    this.loadBoard();
  }

  protected onDestinationInput(value: string): void {
    this.destinationQuery.set(value);
    this.selectedDestination.set('');
    this.selectedDestinationName.set('');
    this.destinationOpen.set(true);
    this.activeDestinationIndex.set(0);
    const query = value.trim();
    if (query.length < 2) {
      this.destinationMatches.set([]);
      this.destinationSearchState.set('idle');
      return;
    }
    this.destinationSearch.next(query);
  }

  protected openDestinations(): void {
    this.destinationOpen.set(true);
    this.activeDestinationIndex.set(this.visibleDestinationMatches().length > 0 ? 0 : -1);
  }

  protected onDestinationKeydown(event: KeyboardEvent): void {
    const matches = this.visibleDestinationMatches();
    if (event.key === 'Escape') {
      this.destinationOpen.set(false);
      return;
    }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!this.destinationOpen()) this.destinationOpen.set(true);
      if (matches.length === 0) {
        this.activeDestinationIndex.set(-1);
        return;
      }
      const direction = event.key === 'ArrowDown' ? 1 : -1;
      this.activeDestinationIndex.set(Math.max(0, Math.min(matches.length - 1, this.activeDestinationIndex() + direction)));
      return;
    }
    const activeMatch = matches[this.activeDestinationIndex()];
    if (event.key === 'Enter' && this.destinationOpen() && activeMatch) {
      event.preventDefault();
      this.selectDestination(activeMatch);
    }
  }

  protected selectDestination(match: PassengerStationMatch): void {
    this.selectedDestination.set(match.stationId);
    this.selectedDestinationName.set(match.displayName);
    this.destinationQuery.set(match.displayName);
    this.destinationOpen.set(false);
    this.destinationMatches.set([]);
    this.showAlternatives.set(!match.isDirect);
    this.loadBoard();
    if (!match.isDirect) this.loadJourneys(); else this.journeyPlan.set(null);
  }

  protected clearDestination(): void {
    this.selectedDestination.set('');
    this.selectedDestinationName.set('');
    this.destinationQuery.set('');
    this.destinationMatches.set([]);
    this.destinationSearchState.set('idle');
    this.destinationOpen.set(false);
    this.showAlternatives.set(false);
    this.journeyPlan.set(null);
    this.expandedJourneyId.set(null);
  }

  protected retryDestinationSearch(): void {
    const query = this.destinationQuery().trim();
    if (query.length >= 2) this.destinationSearch.next(query);
  }

  protected toggleAlternatives(): void {
    const next = !this.showAlternatives();
    this.showAlternatives.set(next);
    if (next && !this.journeyPlan()) this.loadJourneys();
  }

  protected toggleJourney(journeyId: string): void {
    this.expandedJourneyId.set(this.expandedJourneyId() === journeyId ? null : journeyId);
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
    return station.name ?? ArrivalsComponent.StationNames[station.stationId] ?? station.stationId;
  }

  protected matchMeta(match: PassengerStationMatch): string {
    return (match.lines.length > 0 ? match.lines : match.modes).join(' · ');
  }

  protected optionId(match: PassengerStationMatch): string {
    return `destination-${match.stationId.replace(/[^a-zA-Z0-9]/g, '')}`;
  }

  protected formatTime(value: string | null): string {
    return value ? new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit' }).format(new Date(value)) : 'Time unavailable';
  }

  protected mainLines(journey: PassengerJourney): string {
    return journey.legs.filter(leg => leg.kind === 'transport').map(leg => leg.lineName ?? leg.mode).join(' → ');
  }

  protected loadBoard(showLoader = true): void {
    const station = this.selectedStation();
    if (!station) return;
    if (showLoader) this.loading.set(true);
    this.error.set(null);
    this.api.getDepartureBoard(station, this.selectedDestination() || undefined).subscribe({
      next: board => { this.board.set(board); this.loading.set(false); },
      error: () => { this.error.set('Unable to load the live departure board.'); this.loading.set(false); }
    });
  }

  protected countdown(train: PassengerTrain): string {
    if (!train.expectedArrivalUtc) return this.secondsLabel(train.secondsToStation);
    const seconds = Math.max(0, Math.round((new Date(train.expectedArrivalUtc).getTime() - this.now()) / 1000));
    if (seconds === 0) return 'Due';
    if (seconds < 60) return `${seconds}s`;
    return `${Math.ceil(seconds / 60)} min`;
  }

  protected freshnessLabel(): string {
    const observed = this.board()?.observedAtUtc;
    if (!observed) return 'No live observation';
    const seconds = Math.max(0, Math.round((this.now() - new Date(observed).getTime()) / 1000));
    return seconds < 60 ? `Updated ${seconds}s ago` : `Updated ${Math.floor(seconds / 60)}m ago`;
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

  protected loadJourneys(): void {
    const destination = this.selectedDestination();
    if (!destination) return;
    this.journeyLoading.set(true);
    this.journeyError.set(null);
    this.api.getJourneys(this.selectedStation(), destination, this.journeyPreference(), this.stepFree() ? ['stepFreeToPlatform'] : [])
      .subscribe({
        next: plan => { this.journeyPlan.set(plan); this.journeyLoading.set(false); },
        error: () => { this.journeyError.set('TfL Journey Planner is temporarily unavailable.'); this.journeyLoading.set(false); }
      });
  }

  private secondsLabel(seconds: number): string {
    return seconds < 60 ? `${Math.max(0, seconds)}s` : `${Math.ceil(seconds / 60)} min`;
  }
}
