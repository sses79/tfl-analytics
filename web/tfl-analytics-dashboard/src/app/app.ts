import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { SignalRService } from './services/signalr.service';
import { environment } from '../environments/environment';

interface ApiHealth {
  status: string;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly http = inject(HttpClient);
  private readonly signalR = inject(SignalRService);
  private readonly healthTimer = window.setInterval(() => this.checkHealth(), 60_000);

  protected readonly apiOnline = signal(false);
  protected readonly signalRConnected = this.signalR.connected;

  constructor() {
    inject(DestroyRef).onDestroy(() => window.clearInterval(this.healthTimer));
    this.signalR.start();
    this.checkHealth();
  }

  private checkHealth(): void {
    this.http.get<ApiHealth>(`${environment.apiBaseUrl}/health/live`).subscribe({
      next: r => this.apiOnline.set(r.status === 'healthy'),
      error: () => this.apiOnline.set(false)
    });
  }
}
