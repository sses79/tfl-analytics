import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { DashboardSummary } from '../../models';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly signalR = inject(SignalRService);

  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

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

  protected formatTime(iso: string | null): string {
    if (!iso) return '—';
    return new Intl.DateTimeFormat('en-GB', {
      hour: '2-digit', minute: '2-digit', second: '2-digit'
    }).format(new Date(iso));
  }
}
