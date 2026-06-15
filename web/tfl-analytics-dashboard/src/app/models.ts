export interface StationSummary {
  stationId: string;
  name: string | null;
}

export interface ArrivalSummary {
  lineId: string;
  lineName: string | null;
  destinationName: string | null;
  platformName: string | null;
  direction: string | null;
  expectedArrivalUtc: string | null;
  secondsToStation: number;
  observedAtUtc: string;
}

export interface LineStatusSummary {
  lineId: string;
  lineName: string;
  statusSeverity: number;
  statusSeverityDescription: string;
  reason: string | null;
  observedAtUtc: string;
}

export interface AlertSummary {
  alertId: string;
  ruleType: string;
  stationId: string | null;
  lineId: string | null;
  title: string;
  description: string;
  previousValue: string;
  currentValue: string;
  detectedAtUtc: string;
  observedAtUtc: string;
}

export interface DashboardSummary {
  linesMonitored: number;
  linesDisrupted: number;
  stationsMonitored: number;
  recentAlertCount: number;
  lastEventUtc: string | null;
}

// SignalR push payloads
export interface ArrivalsUpdated {
  stationId: string;
  stationName: string | null;
  lineId: string;
  lineName: string | null;
  destinationName: string | null;
  platformName: string | null;
  direction: string | null;
  expectedArrivalUtc: string | null;
  secondsToStation: number;
  observedAtUtc: string;
}

export interface LineStatusChanged {
  lineId: string;
  lineName: string;
  statusSeverity: number;
  statusSeverityDescription: string;
  reason: string | null;
  observedAtUtc: string;
}

export interface AlertRaised {
  alertId: string;
  ruleType: string;
  stationId: string | null;
  lineId: string | null;
  title: string;
  description: string;
  previousValue: string;
  currentValue: string;
  detectedAtUtc: string;
}
