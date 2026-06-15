import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { ArrivalSummary, StationSummary } from '../../models';

@Component({
  selector: 'app-arrivals',
  imports: [FormsModule],
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
      this.arrivals.update(list => {
        const fresh: ArrivalSummary = {
          lineId: update.lineId,
          lineName: update.lineName,
          destinationName: update.destinationName,
          platformName: update.platformName,
          direction: update.direction,
          expectedArrivalUtc: update.expectedArrivalUtc,
          secondsToStation: update.secondsToStation,
          observedAtUtc: update.observedAtUtc
        };
        return [fresh, ...list].slice(0, 30);
      });
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
}
