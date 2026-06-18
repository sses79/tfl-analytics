import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AlertSummary,
  ArrivalSummary,
  DashboardSummary,
  LineStatusSummary,
  StationSummary,
} from '../models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;
  private readonly ingestionBase = environment.ingestionBaseUrl;

  getStations(): Observable<StationSummary[]> {
    return this.http.get<StationSummary[]>(`${this.base}/api/stations`);
  }

  getArrivals(stationId: string, count = 20): Observable<ArrivalSummary[]> {
    return this.http.get<ArrivalSummary[]>(
      `${this.base}/api/stations/${stationId}/arrivals`,
      { params: { count } }
    );
  }

  getLineStatus(): Observable<LineStatusSummary[]> {
    return this.http.get<LineStatusSummary[]>(`${this.base}/api/lines/status`);
  }

  getAlerts(count = 50): Observable<AlertSummary[]> {
    return this.http.get<AlertSummary[]>(`${this.base}/api/alerts`, {
      params: { count },
    });
  }

  getDashboardSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.base}/api/dashboard/summary`);
  }

  triggerPull(): Observable<{ arrivalsPublished: number; lineStatusPublished: number }> {
    return this.http.post<{ arrivalsPublished: number; lineStatusPublished: number }>(
      `${this.ingestionBase}/api/pull`,
      {}
    );
  }
}
