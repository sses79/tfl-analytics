import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { LineStatusChanged, LineStatusSummary } from '../../models';
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
    { service: 'PollLineStatus', detail: 'Polls every ten minutes', tone: 'compute' },
    { service: 'Cosmos raw-events', detail: 'Stores one batch for all monitored lines', tone: 'messaging' },
    { service: 'ArchiveRawEvents', detail: 'Change feed uses leases to checkpoint progress', tone: 'compute' },
    { service: 'Blob + queue', detail: 'Archives and queues one polling-cycle batch', tone: 'messaging' },
    { service: 'ProcessQueuedEvent', detail: 'Persists each line, then emits one batch update', tone: 'compute' },
    { service: 'Cosmos line-status', detail: 'Stores the current status for each line', tone: 'storage' },
    { service: 'API + SignalR', detail: 'Returns current data and pushes one status batch', tone: 'api' },
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
        const updated = this.toSummary(update);
        if (idx >= 0) {
          const copy = [...lines];
          copy[idx] = updated;
          return copy;
        }
        return [...lines, updated];
      });
      this.lastUpdated.set(new Date());
    });
    effect(() => {
      const batch = this.signalR.lastLineStatusesBatchChange();
      if (!batch) return;
      this.lines.set(batch.lineStatuses.map(status => this.toSummary(status)));
      this.lastUpdated.set(new Date(batch.observedAtUtc));
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

  private toSummary(status: LineStatusChanged): LineStatusSummary {
    return {
      lineId: status.lineId,
      lineName: status.lineName,
      statusSeverity: status.statusSeverity,
      statusSeverityDescription: status.statusSeverityDescription,
      reason: status.reason,
      observedAtUtc: status.observedAtUtc
    };
  }
}
