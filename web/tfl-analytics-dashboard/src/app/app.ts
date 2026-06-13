import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { environment } from '../environments/environment';

interface ApiHealth {
  status: string;
}

interface LineStatus {
  statusSeverity: number;
  statusSeverityDescription: string;
  reason?: string;
}

interface Line {
  id: string;
  name: string;
  modeName: string;
  lineStatuses: LineStatus[];
}

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly http = inject(HttpClient);
  private readonly refreshTimer = window.setInterval(() => this.refresh(), 60_000);
  private readonly lineIds = 'victoria,circle,central,jubilee,piccadilly';

  protected readonly lines = signal<Line[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly apiOnline = signal(false);
  protected readonly lastUpdated = signal<Date | null>(null);
  protected readonly disruptions = computed(() =>
    this.lines().filter(line => this.primaryStatus(line).statusSeverity < 10).length
  );
  protected readonly updatedLabel = computed(() => {
    const updated = this.lastUpdated();
    return updated
      ? new Intl.DateTimeFormat('en-GB', {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit'
        }).format(updated)
      : 'Awaiting data';
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => window.clearInterval(this.refreshTimer));
    this.refresh();
  }

  protected refresh(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.http.get<ApiHealth>(`${environment.apiBaseUrl}/health/live`).subscribe({
      next: response => this.apiOnline.set(response.status === 'healthy'),
      error: () => this.apiOnline.set(false)
    });

    this.http
      .get<Line[]>(`${environment.apiBaseUrl}/api/tfl/line-status/${this.lineIds}`)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: lines => {
          this.lines.set(lines);
          this.lastUpdated.set(new Date());
        },
        error: () => {
          this.error.set(
            'Live TfL data is temporarily unavailable. The dashboard will retry automatically.'
          );
        }
      });
  }

  protected primaryStatus(line: Line): LineStatus {
    return line.lineStatuses[0] ?? {
      statusSeverity: 0,
      statusSeverityDescription: 'Status unavailable'
    };
  }

  protected statusTone(severity: number): string {
    if (severity === 10) {
      return 'good';
    }

    return severity >= 7 ? 'warning' : 'disruption';
  }

  protected lineColour(lineId: string): string {
    const colours: Record<string, string> = {
      central: '#e32017',
      circle: '#ffd300',
      jubilee: '#a0a5a9',
      piccadilly: '#003688',
      victoria: '#0098d4'
    };

    return colours[lineId] ?? '#5b6573';
  }
}
