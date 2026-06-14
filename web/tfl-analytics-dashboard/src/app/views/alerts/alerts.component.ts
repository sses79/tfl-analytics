import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { AlertSummary } from '../../models';

@Component({
  selector: 'app-alerts',
  templateUrl: './alerts.component.html',
  styleUrl: './alerts.component.scss'
})
export class AlertsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly signalR = inject(SignalRService);

  protected readonly alerts = signal<AlertSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

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
}
