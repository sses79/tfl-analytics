import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { LineStatusSummary } from '../../models';
import {
  DataFlowExplainerComponent,
  DataFlowStep
} from '../../components/data-flow-explainer/data-flow-explainer.component';

@Component({
  selector: 'app-line-status',
  imports: [DataFlowExplainerComponent],
  templateUrl: './line-status.component.html',
  styleUrl: './line-status.component.scss'
})
export class LineStatusComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly signalR = inject(SignalRService);

  protected readonly lines = signal<LineStatusSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly lastUpdated = signal<Date | null>(null);
  protected readonly flowSteps: readonly DataFlowStep[] = [
    { service: 'TfL Line API', detail: 'Current Underground service status', tone: 'source' },
    { service: 'PollLineStatus', detail: 'Runs approximately every two minutes', tone: 'compute' },
    { service: 'Event Hubs', detail: 'LineStatusObserved event', tone: 'messaging' },
    { service: 'ProcessQueuedEvent', detail: 'Normalizes and compares status', tone: 'compute' },
    { service: 'Cosmos DB', detail: 'line-status container', tone: 'storage' },
    { service: 'API + SignalR', detail: 'Query response and live change push', tone: 'api' },
    { service: 'Line status page', detail: 'Service cards update in the browser', tone: 'ui' }
  ];

  private static readonly LineColours: Record<string, string> = {
    bakerloo: '#894e24',
    central: '#e32017',
    circle: '#ffd300',
    district: '#00782a',
    'hammersmith-city': '#f3a9bb',
    jubilee: '#a0a5a9',
    metropolitan: '#9b0056',
    northern: '#000000',
    piccadilly: '#003688',
    victoria: '#0098d4',
    'waterloo-city': '#95cdba',
    elizabeth: '#6950a1',
    overground: '#e86a10',
    'dlr': '#00afad'
  };

  constructor() {
    effect(() => {
      const update = this.signalR.lastLineStatusChange();
      if (!update) return;
      this.lines.update(lines => {
        const idx = lines.findIndex(l => l.lineId === update.lineId);
        const updated: LineStatusSummary = {
          lineId: update.lineId,
          lineName: update.lineName,
          statusSeverity: update.statusSeverity,
          statusSeverityDescription: update.statusSeverityDescription,
          reason: update.reason,
          observedAtUtc: update.observedAtUtc
        };
        if (idx >= 0) {
          const copy = [...lines];
          copy[idx] = updated;
          return copy;
        }
        return [...lines, updated];
      });
      this.lastUpdated.set(new Date());
    });
  }

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.api.getLineStatus().subscribe({
      next: lines => {
        this.lines.set(lines);
        this.loading.set(false);
        this.lastUpdated.set(new Date());
      },
      error: () => {
        this.error.set('Unable to load line status data.');
        this.loading.set(false);
      }
    });
  }

  protected lineColour(lineId: string): string {
    return LineStatusComponent.LineColours[lineId] ?? '#5b6573';
  }

  protected statusTone(severity: number): string {
    if (severity === 10) return 'good';
    return severity >= 7 ? 'warning' : 'disruption';
  }

  protected updatedLabel(): string {
    const d = this.lastUpdated();
    return d
      ? new Intl.DateTimeFormat('en-GB', {
          hour: '2-digit', minute: '2-digit', second: '2-digit'
        }).format(d)
      : 'Awaiting data';
  }
}
