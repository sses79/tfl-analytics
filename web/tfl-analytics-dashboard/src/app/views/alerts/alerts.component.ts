import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { AlertSummary } from '../../models';
import {
  DataFlowExplainerComponent,
  DataFlowStep
} from '../../components/data-flow-explainer/data-flow-explainer.component';

@Component({
  selector: 'app-alerts',
  imports: [DataFlowExplainerComponent],
  templateUrl: './alerts.component.html',
  styleUrl: './alerts.component.scss'
})
export class AlertsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly signalR = inject(SignalRService);

  protected readonly alerts = signal<AlertSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly flowSteps: readonly DataFlowStep[] = [
    { service: 'Processed TfL event', detail: 'Arrival slip or line-status transition', tone: 'source' },
    { service: 'Alert detector', detail: 'Applies disruption and slippage rules', tone: 'compute' },
    { service: 'Durable Functions', detail: 'Coordinates retryable alert activities', tone: 'compute' },
    { service: 'Azure SQL', detail: 'Exactly-once alert record', tone: 'storage' },
    { service: 'Table Storage', detail: 'AlertRaised audit entity', tone: 'storage' },
    { service: 'API + SignalR', detail: 'History query and live alert push', tone: 'api' },
    { service: 'Alerts page', detail: 'Operational workflow outcome', tone: 'ui' }
  ];

  constructor() {
    effect(() => {
      const newAlert = this.signalR.lastAlert();
      if (!newAlert) return;
      const summary: AlertSummary = {
        alertId: newAlert.alertId,
        ruleType: newAlert.ruleType,
        stationId: newAlert.stationId,
        lineId: newAlert.lineId,
        title: newAlert.title,
        description: newAlert.description,
        previousValue: newAlert.previousValue,
        currentValue: newAlert.currentValue,
        detectedAtUtc: newAlert.detectedAtUtc,
        observedAtUtc: newAlert.detectedAtUtc
      };
      this.alerts.update(list => [summary, ...list.filter(a => a.alertId !== newAlert.alertId)]);
    });
  }

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.api.getAlerts().subscribe({
      next: list => {
        this.alerts.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load alert history.');
        this.loading.set(false);
      }
    });
  }

  protected formatTime(iso: string): string {
    return new Intl.DateTimeFormat('en-GB', {
      dateStyle: 'short', timeStyle: 'medium'
    }).format(new Date(iso));
  }

  protected ruleLabel(ruleType: string): string {
    switch (ruleType) {
      case 'ArrivalPredictionSlippage': return 'Delay';
      case 'LineStatusDisruption': return 'Disruption';
      default: return ruleType;
    }
  }

  private static readonly StationNames: Record<string, string> = {
    '940GZZLUVIC': 'Victoria',
    '940GZZLUOXC': 'Oxford Circus',
    '940GZZLUGPK': 'Green Park',
    '940GZZLUKSX': "King's Cross St. Pancras",
    '940GZZLULNB': 'London Bridge'
  };

  protected stationLabel(stationId: string | null): string {
    if (!stationId) return '';
    return AlertsComponent.StationNames[stationId] ?? stationId;
  }

  protected formatValue(value: string, ruleType: string): string {
    if (ruleType === 'ArrivalPredictionSlippage') {
      const n = Number(value);
      if (!isNaN(n)) {
        const abs = Math.abs(n);
        const sign = n < 0 ? '−' : '+';
        if (abs < 60) return `${sign}${abs}s`;
        const m = Math.floor(abs / 60);
        const s = abs % 60;
        return s > 0 ? `${sign}${m}m ${s}s` : `${sign}${m}m`;
      }
    }
    const iso = Date.parse(value);
    if (!isNaN(iso)) {
      return new Intl.DateTimeFormat('en-GB', {
        hour: '2-digit', minute: '2-digit', second: '2-digit'
      }).format(iso);
    }
    return value;
  }
}
