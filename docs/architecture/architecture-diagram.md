# Azure Event And Data Flow

> [Documentation index](../README.md)

This is the current reduced-cost Azure flow. The previous Event Hubs diagram is
retained as [historical context](../history/architecture-diagram-legacy.html).

```mermaid
flowchart LR
    TFL[TfL Unified API]
    KV[Key Vault]

    subgraph Ingestion
        TIMER[Ingestion Functions<br/>arrivals every 5 min<br/>line status every 10 min]
        RAW[Cosmos DB<br/>raw-events]
    end

    subgraph Processing
        CHANGE[Cosmos change-feed trigger<br/>leases checkpoint]
        BLOB[ADLS Gen2 raw archive]
        QUEUE[Storage processing queue]
        WORKER[Processing Functions]
        ALERTS[Durable alert workflow<br/>disabled by configuration]
    end

    subgraph Data
        CURRENT[Cosmos DB<br/>live-events and line-status]
        TABLES[Table Storage<br/>alerts and audit]
    end

    subgraph Delivery
        API[Container App API<br/>public GHCR image]
        SIGNALR[Azure SignalR Free F1]
        WEB[Angular<br/>Static Web Apps Free]
    end

    KV -. TfL key .-> TIMER
    TFL --> TIMER
    TIMER --> RAW
    RAW --> CHANGE
    CHANGE --> BLOB
    CHANGE --> QUEUE
    QUEUE --> WORKER
    WORKER --> CURRENT
    WORKER --> ALERTS
    ALERTS --> TABLES
    WORKER --> SIGNALR
    ALERTS --> SIGNALR
    CURRENT --> API
    TABLES --> API
    API --> WEB
    SIGNALR --> WEB
```
