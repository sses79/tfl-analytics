import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { DashboardSummary } from '../../models';
import {
  DataFlowExplainerComponent,
  DataFlowStep
} from '../../components/data-flow-explainer/data-flow-explainer.component';

@Component({
  selector: 'app-dashboard',
  imports: [DataFlowExplainerComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly signalR = inject(SignalRService);

  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly pulling = signal(false);
  protected readonly pullResult = signal<string | null>(null);
  protected readonly flowSteps: readonly DataFlowStep[] = [
    { service: 'TfL Unified API', detail: 'Arrival and line status observations', tone: 'source' },
    { service: 'Ingestion Functions', detail: 'Timer-triggered polling', tone: 'compute' },
    { service: 'Event Hubs', detail: 'Transport-neutral event stream', tone: 'messaging' },
    { service: 'Processing Functions', detail: 'Archive, normalize and detect', tone: 'compute' },
    { service: 'Cosmos DB + SQL', detail: 'Live events and alert history', tone: 'storage' },
    { service: 'Container App API', detail: 'Aggregated dashboard summary', tone: 'api' },
    { service: 'Angular + SignalR', detail: 'Live overview in Static Web Apps', tone: 'ui' }
  ];

  constructor() {
    effect(() => {
      const _ = this.signalR.lastLineStatusChange();
      const __ = this.signalR.lastArrivalsUpdate();
      this.load();
    });
  }

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.api.getDashboardSummary().subscribe({
      next: s => {
        this.summary.set(s);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load summary data.');
        this.loading.set(false);
      }
    });
  }

  protected pull(): void {
    this.pulling.set(true);
    this.pullResult.set(null);
    this.api.triggerPull().subscribe({
      next: r => {
        this.pullResult.set(
          `Pulled ${r.arrivalsPublished} arrivals, ${r.lineStatusPublished} line updates.`
        );
        this.pulling.set(false);
        this.load();
      },
      error: () => {
        this.pullResult.set('Pull failed — ingestion service unavailable.');
        this.pulling.set(false);
      }
    });
  }

  protected formatTime(iso: string | null): string {
    if (!iso) return '—';
    return new Intl.DateTimeFormat('en-GB', {
      hour: '2-digit', minute: '2-digit', second: '2-digit'
    }).format(new Date(iso));
  }
}
