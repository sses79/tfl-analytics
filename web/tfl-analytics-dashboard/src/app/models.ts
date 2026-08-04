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
  predictionId: string | null;
  vehicleId: string | null;
  stationId: string | null;
  stationName: string | null;
  destinationStationId: string | null;
  towards: string | null;
  currentLocation: string | null;
}

export interface DepartureBoard {
  stationId: string;
  stationName: string | null;
  observedAtUtc: string | null;
  isStale: boolean;
  destinations: DestinationOption[];
  recommendations: RouteRecommendation[];
  platforms: PlatformDepartureBoard[];
  disruptions: PassengerDisruption[];
}

export interface DestinationOption {
  stationId: string;
  stationName: string;
  lineIds: string[];
}

export interface RouteRecommendation {
  lineId: string;
  lineName: string | null;
  direction: string;
  platformName: string | null;
  towards: string | null;
  stopsUntilDestination: number;
  stations: RouteStation[];
}

export interface RouteStation {
  stationId: string;
  stationName: string;
  sequence: number;
  isOrigin: boolean;
  isDestination: boolean;
}

export interface PlatformDepartureBoard {
  lineId: string;
  lineName: string | null;
  direction: string;
  platformName: string | null;
  trains: PassengerTrain[];
}

export interface PassengerTrain {
  predictionId: string | null;
  vehicleId: string | null;
  destinationStationId: string | null;
  destinationName: string | null;
  towards: string | null;
  currentLocation: string | null;
  expectedArrivalUtc: string | null;
  secondsToStation: number;
  observedAtUtc: string;
  servesSelectedDestination: boolean | null;
  stopsUntilDestination: number | null;
  predictionState: 'unknown' | 'betweenStations' | 'approachingStation' | 'atPlatform';
  estimatedStationId: string | null;
  predictionStateLabel: string;
}

export interface PassengerDisruption {
  lineId: string;
  lineName: string;
  status: string;
  reason: string | null;
  observedAtUtc: string;
}

export interface PassengerJourneyPlan { journeys: PassengerJourney[]; duplicateCountRemoved: number; }
export interface PassengerJourney {
  id: string;
  labels: string[];
  durationMinutes: number;
  departureUtc: string | null;
  arrivalUtc: string | null;
  changeCount: number;
  walkingMinutes: number;
  accessibilitySummary: string | null;
  legs: PassengerJourneyLeg[];
  disruptions: PassengerJourneyDisruption[];
}
export interface PassengerJourneyLeg {
  kind: 'enter' | 'exit' | 'walk' | 'transport';
  mode: string;
  lineName: string | null;
  towards: string | null;
  fromStationId: string | null;
  fromName: string | null;
  toStationId: string | null;
  toName: string | null;
  durationMinutes: number;
  instruction: string;
}
export interface PassengerJourneyDisruption {
  category: string | null;
  summary: string;
  affectedLegIndexes: number[];
}
export interface StationSearchResponse { matches: PassengerStationMatch[]; }
export interface PassengerStationMatch {
  stationId: string;
  displayName: string;
  modes: string[];
  lines: string[];
  isDirect: boolean;
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
  predictionId: string | null;
  vehicleId: string | null;
  destinationStationId: string | null;
  towards: string | null;
  currentLocation: string | null;
}

export interface ArrivalsBatchUpdated {
  arrivals: ArrivalsUpdated[];
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

export interface LineStatusesBatchChanged {
  lineStatuses: LineStatusChanged[];
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
