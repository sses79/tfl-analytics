# Angular interview notes: TfL Analytics Dashboard

Use these notes to describe what the frontend currently implements. Keep proposed
improvements in future tense so the interview answer remains accurate.

## Project summary

The frontend is an Angular 21.2 and TypeScript 5.9 standalone application. It is
a real-time TfL operations dashboard with four lazy-loaded routes:

- `/dashboard` displays network metrics and can request an immediate ingestion
  pull.
- `/status` displays live line-status cards.
- `/arrivals` displays predictions for a selected monitored station.
- `/alerts` displays recent operational alerts.

The application is hosted in Azure Static Web Apps. It reads persisted data from
an ASP.NET Core API using `HttpClient` and receives live updates from the
backend's `/hubs/dashboard` SignalR hub.

The main frontend dependencies are Angular Router, Angular `HttpClient`, Angular
signals, RxJS 7.8, Microsoft SignalR 10, SCSS, Vitest 4 and jsdom 28. The project
uses strict TypeScript and strict Angular template checking.

## Strong interview answer

> “I built an Angular 21 dashboard in TypeScript for a real-time Transport for
> London analytics platform. It uses standalone components and lazy-loaded
> routes for dashboard, line-status, arrivals and alert views.
>
> I separated REST access into a typed `ApiService` and SignalR transport into a
> root-provided `SignalRService`. Initial and manually refreshed state comes from
> the API. SignalR then pushes arrival, line-status and alert messages, which the
> service stores in Angular signals. Components use `effect` to merge those
> messages into their local signal state, so the UI updates without polling each
> feature endpoint.
>
> The app also reports API health and live-connection state in its shell. It uses
> automatic SignalR reconnection, environment-specific backend URLs, strict
> compiler settings, and Vitest with Angular's testing utilities. It is built as
> a production static application and deployed to Azure Static Web Apps.”

## Architecture to describe

```text
ASP.NET Core REST API ── HttpClient/Observable ──> ApiService
                                                    │
                                                    v
                                            component signals
                                                    │
                                                    v
Azure/.NET SignalR hub ── SignalRService signals ──> effect() ──> template
```

- `models.ts` defines the REST summaries and SignalR payload interfaces.
- `ApiService` owns the typed GET and POST requests.
- `SignalRService` owns connection setup, event registration, reconnection and
  shutdown.
- Each view owns its loading, error and displayed-data signals.
- `App` owns navigation, API health polling and the global live-connection
  indicators.
- `DataFlowExplainerComponent` is a reusable collapsible component used by all
  four views.

## Likely questions and accurate answers

### Why did you use SignalR?

> “The dashboard shows events that continue to arrive after the initial page
> load. REST is a good fit for loading the current persisted state, while SignalR
> lets the server push newly processed arrivals, status changes and alerts to an
> open browser. This avoids repeatedly polling every feature endpoint.”

The client registers these hub messages:

- `arrivalsUpdated`
- `lineStatusChanged`
- `alertRaised`

The root component starts the singleton connection once. The service guards
against duplicate starts, enables `withAutomaticReconnect()`, changes its
`connected` signal after a successful start or reconnect, clears it on close,
and stops the connection in `ngOnDestroy`.

### How did you combine SignalR with Angular state?

> “I isolated the SignalR client in a service and represented the latest payload
> of each message type as an Angular signal. Feature components read those
> signals inside `effect`. The status page replaces the matching line, the alert
> page prepends and deduplicates by alert ID, and the arrivals page prepends an
> update only when it belongs to the selected station.”

This project does **not** expose SignalR events as RxJS `Subject` or
`BehaviorSubject` streams. RxJS is present through `HttpClient`, whose methods
return typed `Observable` values. The components currently call `subscribe`
directly for those finite HTTP requests.

### Why use signals here?

> “The feature state is small and local: data, loading flags, error messages and
> the most recent pushed event. Signals provide direct synchronous reads in the
> templates and simple immutable updates. `effect` is useful at the boundary
> where a SignalR message needs to update a feature's displayed collection.”

Examples include `summary`, `lines`, `arrivals`, `alerts`, `loading`, `error`,
`selectedStation`, `apiOnline` and `connected`.

### How is the Angular application structured?

> “It is a standalone application rather than an NgModule-based one. Global
> providers are configured with `provideHttpClient()` and `provideRouter()`.
> Each route lazy-loads a standalone view with `loadComponent`, while typed API
> and SignalR services are provided at root. Shared UI is implemented as a
> standalone component imported only where it is used.”

The empty path and wildcard both redirect to `/dashboard`.

### How does each view react to live messages?

- Dashboard: an arrival or line-status signal causes the summary endpoint to be
  queried again.
- Line status: the matching line is replaced in memory, or appended if it is
  new.
- Arrivals: a matching-station update is prepended and the list is capped at 30
  entries.
- Alerts: the pushed alert is mapped to the view model, prepended and
  deduplicated by `alertId`.

> “The backend remains the source of persisted truth. The dashboard demonstrates
> two useful update strategies: re-querying an aggregate after an event, and
> applying a typed event directly to a local list.”

### How did you handle loading and failures?

> “Each view has explicit loading and error signals and renders skeleton, empty
> or error states with Angular's built-in `@if` and `@for` control flow. API
> subscriptions use `next` and `error` handlers. The shell calls `/health/live`
> on startup and every 60 seconds, and separately displays whether SignalR is
> connected.”

The health interval is cleared with `DestroyRef.onDestroy`. SignalR start
failures are currently logged as warnings. There is no custom retry policy or
manual reconnect button in the frontend.

### How did you handle cleanup?

> “HTTP calls complete after one response, so the current feature subscriptions
> do not remain open. The root health timer is explicitly cleared through
> `DestroyRef`, and the singleton SignalR service stops its hub connection in
> `ngOnDestroy`. For any future long-lived RxJS streams I would use the async pipe
> or `takeUntilDestroyed`.”

Do not claim that the current components use the async pipe or
`takeUntilDestroyed`; they do not.

### How did you build forms?

The current application does not contain a form workflow or use Reactive Forms.
The arrivals page has a native `<select>` whose `change` event updates a signal.
Although `@angular/forms` is installed and `FormsModule` is imported by the
arrivals component, the template does not currently use `ngModel`.

An accurate answer is:

> “This dashboard did not need a substantial form workflow. Its only selection
> control is a station dropdown backed by a signal and an explicit change
> handler. For a validation-heavy feature I would use typed Reactive Forms, but
> I would not claim this project as an example of that yet.”

### How did you test the frontend?

> “The Angular test target uses Vitest with jsdom and Angular TestBed. The app
> shell spec replaces SignalR with a signal-based test double and uses
> `HttpTestingController` to verify health success and failure states. A second
> spec verifies the shared data-flow explainer's inputs and its expand/collapse
> interaction.”

Current automated coverage consists of:

- `app.spec.ts`: brand and navigation rendering, API health state, SignalR
  startup and disconnected state.
- `data-flow-explainer.component.spec.ts`: rendered steps, event type and toggle
  behavior.

There are not yet dedicated specs for `ApiService`, `SignalRService`, or the four
feature views. Good next tests would cover hub callbacks, reconnection state,
HTTP error paths and the feature-specific list merge rules.

### Why Vitest rather than Karma and Jasmine?

> “The Angular 21 unit-test builder in this project runs Vitest. It provides fast
> feedback, familiar spies and assertions, and works with jsdom and Angular
> TestBed. More important than the runner choice, the service boundaries allow
> `HttpClient` and SignalR to be replaced by focused test doubles.”

### How do routing and bundle size work?

> “Each top-level feature uses `loadComponent`, so Angular can produce a separate
> lazy chunk instead of putting every view into the initial bundle. The
> production build enables script and style optimization, hashed output and
> bundle budgets: 500 kB warning and 1 MB error for the initial bundle.”

### How is configuration handled?

> “The services read base URLs from Angular environment files. Development uses
> the local API on port 8080 and ingestion Function on port 7071. The production
> environment points to the deployed Azure services. Angular's development build
> replaces the production environment file.”

The dashboard's manual “Pull latest data” action posts to `/api/pull` on the
ingestion service and reports the published arrival and line-status counts.

### How did you handle authentication?

Authentication and authorization are not implemented in this frontend. The
SignalR connection does not use `accessTokenFactory`, and there are no route
guards or HTTP authentication interceptors.

> “Authentication is outside the implemented dashboard phase. If added, I would
> integrate the chosen identity provider, protect routes as a usability measure,
> attach access tokens to API requests, configure SignalR's
> `accessTokenFactory`, and enforce authorization on the backend. Client-side
> guards alone would not be a security boundary.”

### How did you prevent stale, duplicate or out-of-order updates?

> “The alert view deduplicates pushed alerts by their stable `alertId`. The
> line-status view replaces records by `lineId`. The arrivals view currently
> prepends matching events and caps the visible list, but it does not compare
> event versions or timestamps. If ordering guarantees became important, I would
> use the event ID and observation time to deduplicate and reject stale updates,
> while keeping the backend as the source of truth.”

Do not claim full client-side ordering or deduplication across all message types.

### Did you use a state-management library?

> “No. Root services plus component-local signals are sufficient for the current
> scope. I would consider a store only if state became shared across many
> features, transitions became difficult to reason about, or we needed stronger
> event tracing and dev tooling.”

## Useful code-level details

- Dependency injection uses the `inject()` function rather than constructor
  parameters.
- Templates use Angular's modern `@if` and `@for` syntax and track stable IDs
  where available.
- REST and SignalR payloads have separate TypeScript interfaces.
- Lists are updated immutably with array copies, `filter`, and `slice`.
- Dates are formatted with `Intl.DateTimeFormat('en-GB', ...)`.
- TfL line colours and monitored-station fallback names are local lookup maps.
- The SCSS component schematic and per-component style files keep view styling
  scoped.
- The app uses accessible labels, status roles, `aria-busy`, semantic tables and
  button disabled states in its templates.

## Honest improvement discussion

If asked what you would improve next:

1. Add focused unit tests for both services and every feature's live-update
   merge behavior.
2. Model SignalR reconnecting state separately from simply connected or off, and
   recover from an initial start failure.
3. Add event-ID or timestamp-based deduplication and ordering for arrivals.
4. Standardize HTTP error handling and clear stale errors before every reload.
5. Add authentication and authorization when the backend phase supports it.
6. Remove the unused forms import unless two-way binding or a real form is
   introduced.

## 45-second version

> “I built a standalone Angular 21 dashboard with TypeScript 5.9 for a real-time
> TfL analytics platform. Four feature routes are lazy-loaded, and a typed API
> service loads dashboard summaries, line status, arrivals and alerts from a .NET
> backend.
>
> For live behavior, I wrapped Microsoft SignalR in a root service. It uses
> automatic reconnection and exposes connection state plus the latest typed
> messages as Angular signals. Feature components react with `effect` and either
> merge the event locally or refresh an aggregate from the API. The shell also
> monitors API health every minute.
>
> The project uses strict TypeScript and templates, modern Angular control flow,
> SCSS, and Vitest with jsdom and TestBed. It builds to Azure Static Web Apps. The
> next testing priority is focused coverage of the services and each feature's
> real-time merge rules.”
