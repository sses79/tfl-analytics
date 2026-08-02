import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { ArrivalSummary, ArrivalsUpdated, StationSummary } from '../../models';
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
export class ArrivalsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly signalR = inject(SignalRService);

  protected readonly stations = signal<StationSummary[]>([]);
  protected readonly arrivals = signal<ArrivalSummary[]>([]);
  protected readonly selectedStation = signal<string>('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly flowSteps: readonly DataFlowStep[] = [
    { service: 'TfL Arrivals API', detail: 'Predictions for five monitored stations', tone: 'source' },
    { service: 'PollArrivals', detail: 'Every five minutes; publishes one prediction batch', tone: 'compute' },
    { service: 'Cosmos change feed', detail: 'One raw batch per polling cycle', tone: 'messaging' },
    { service: 'ProcessQueuedEvent', detail: 'Persists each prediction, then emits one batch update', tone: 'compute' },
    { service: 'Cosmos DB', detail: 'live-events container', tone: 'storage' },
    { service: 'API + SignalR', detail: 'Station query and one fresh batch push', tone: 'api' },
    { service: 'Arrivals page', detail: 'Selected station prediction board', tone: 'ui' }
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
      const update = this.signalR.lastArrivalsUpdate();
      if (!update || update.stationId !== this.selectedStation()) return;
      this.arrivals.update(list => [this.toSummary(update), ...list].slice(0, 30));
    });
    effect(() => {
      const batch = this.signalR.lastArrivalsBatchUpdate();
      const station = this.selectedStation();
      if (!batch || !station) return;
      const fresh = batch.arrivals
        .filter(update => update.stationId === station)
        .map(update => this.toSummary(update));
      if (fresh.length > 0) {
        this.arrivals.set(fresh.slice(0, 30));
      }
    });
  }

  ngOnInit(): void {
    this.api.getStations().subscribe({
      next: stations => {
        this.stations.set(stations);
        if (stations.length > 0) {
          this.selectedStation.set(stations[0].stationId);
          this.loadArrivals();
        }
      },
      error: () => this.error.set('Unable to load station list.')
    });
  }

  protected onStationChange(stationId: string): void {
    this.selectedStation.set(stationId);
    this.loadArrivals();
  }

  protected stationLabel(station: StationSummary): string {
    return station.name
      ?? ArrivalsComponent.StationNames[station.stationId]
      ?? station.stationId;
  }

  protected loadArrivals(): void {
    const station = this.selectedStation();
    if (!station) return;

    this.loading.set(true);
    this.error.set(null);
    this.api.getArrivals(station).subscribe({
      next: list => {
        this.arrivals.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load arrivals for this station.');
        this.loading.set(false);
      }
    });
  }

  protected secondsLabel(seconds: number): string {
    if (seconds < 60) return `${seconds}s`;
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return s > 0 ? `${m}m ${s}s` : `${m}m`;
  }

  protected formatTime(iso: string | null): string {
    if (!iso) return '—';
    return new Intl.DateTimeFormat('en-GB', {
      hour: '2-digit', minute: '2-digit', second: '2-digit'
    }).format(new Date(iso));
  }

  private toSummary(update: ArrivalsUpdated): ArrivalSummary {
    return {
      lineId: update.lineId,
      lineName: update.lineName,
      destinationName: update.destinationName,
      platformName: update.platformName,
      direction: update.direction,
      expectedArrivalUtc: update.expectedArrivalUtc,
      secondsToStation: update.secondsToStation,
      observedAtUtc: update.observedAtUtc
    };
  }
}
