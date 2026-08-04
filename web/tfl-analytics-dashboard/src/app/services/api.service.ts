import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AlertSummary,
  ArrivalSummary,
  DashboardSummary,
  DepartureBoard,
  LineStatusSummary,
  PassengerJourneyPlan,
  StationSummary,
  StationSearchResponse,
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

  getDepartureBoard(
    stationId: string,
    destinationStationId?: string
  ): Observable<DepartureBoard> {
    return this.http.get<DepartureBoard>(
      `${this.base}/api/stations/${stationId}/departure-board`,
      destinationStationId ? { params: { destinationStationId } } : {}
    );
  }

  getJourneys(
    stationId: string,
    destinationStationId: string,
    preference: string,
    accessibility: string[]
  ): Observable<PassengerJourneyPlan> {
    return this.http.get<PassengerJourneyPlan>(
      `${this.base}/api/stations/${stationId}/journeys/${destinationStationId}`,
      { params: { preference, accessibility } }
    );
  }

  searchStations(query: string, originStationId: string): Observable<StationSearchResponse> {
    return this.http.get<StationSearchResponse>(`${this.base}/api/stations/search`, {
      params: { query, originStationId }
    });
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
