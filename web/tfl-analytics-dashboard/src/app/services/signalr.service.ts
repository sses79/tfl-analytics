import {
  Injectable,
  OnDestroy,
  inject,
  signal,
} from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../environments/environment';
import { AlertRaised, ArrivalsUpdated, LineStatusChanged } from '../models';

@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {
  readonly connected = signal(false);
  readonly lastArrivalsUpdate = signal<ArrivalsUpdated | null>(null);
  readonly lastLineStatusChange = signal<LineStatusChanged | null>(null);
  readonly lastAlert = signal<AlertRaised | null>(null);

  private connection: signalR.HubConnection | null = null;

  start(): void {
    if (this.connection) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/dashboard`)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('arrivalsUpdated', (msg: ArrivalsUpdated) =>
      this.lastArrivalsUpdate.set(msg)
    );
    this.connection.on('lineStatusChanged', (msg: LineStatusChanged) =>
      this.lastLineStatusChange.set(msg)
    );
    this.connection.on('alertRaised', (msg: AlertRaised) =>
      this.lastAlert.set(msg)
    );

    this.connection.onreconnected(() => this.connected.set(true));
    this.connection.onclose(() => this.connected.set(false));

    this.connection
      .start()
      .then(() => this.connected.set(true))
      .catch(err => console.warn('SignalR connection failed:', err));
  }

  ngOnDestroy(): void {
    this.connection?.stop();
  }
}
