#!/usr/bin/env python3
"""
TfL raw event analytics — reads compressed event envelopes from ADLS Gen2
and produces an arrival wait-time and line-status report.

Requires: azure-storage-blob azure-identity pandas tabulate

Usage examples:
    # Last 24 hours, all event types, console output
    python analyse_raw.py

    # Specific window
    python analyse_raw.py --from 2026-06-14T08:00:00Z --to 2026-06-14T20:00:00Z

    # Arrivals only, markdown output
    python analyse_raw.py --event-type arrival --output markdown

    # Export CSVs
    python analyse_raw.py --output csv --out-dir ./reports
"""

from __future__ import annotations

import argparse
import gzip
import io
import json
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timedelta, timezone
from pathlib import Path

try:
    from azure.identity import DefaultAzureCredential
    from azure.storage.blob import ContainerClient
    import pandas as pd
    from tabulate import tabulate
except ImportError as exc:
    sys.exit(
        f"Missing dependency: {exc}\n"
        "Run: pip install azure-storage-blob azure-identity pandas tabulate"
    )

DEFAULT_ACCOUNT = "sttflnhkpyupi"
DEFAULT_CONTAINER = "raw"
MAX_WORKERS = 16


# ── CLI ───────────────────────────────────────────────────────────────────────

def parse_args() -> argparse.Namespace:
    now = datetime.now(timezone.utc)
    parser = argparse.ArgumentParser(
        description="Analyse TfL raw events archived in ADLS Gen2."
    )
    parser.add_argument(
        "--from", dest="from_dt",
        default=(now - timedelta(hours=24)).strftime("%Y-%m-%dT%H:%M:%SZ"),
        metavar="ISO8601",
        help="Start of range, inclusive (default: 24h ago)",
    )
    parser.add_argument(
        "--to", dest="to_dt",
        default=now.strftime("%Y-%m-%dT%H:%M:%SZ"),
        metavar="ISO8601",
        help="End of range, exclusive (default: now)",
    )
    parser.add_argument(
        "--account", default=DEFAULT_ACCOUNT,
        help=f"Storage account name (default: {DEFAULT_ACCOUNT})",
    )
    parser.add_argument(
        "--container", default=DEFAULT_CONTAINER,
        help=f"Container name (default: {DEFAULT_CONTAINER})",
    )
    parser.add_argument(
        "--event-type",
        choices=["arrival", "line-status", "all"],
        default="all",
        help="Event type to analyse (default: all)",
    )
    parser.add_argument(
        "--output",
        choices=["console", "markdown", "csv", "html"],
        default="console",
        help="Output format (default: console)",
    )
    parser.add_argument(
        "--out-dir", default=".",
        metavar="DIR",
        help="Directory for csv/markdown files (default: current dir)",
    )
    return parser.parse_args()


def parse_dt(value: str) -> datetime:
    for fmt in ("%Y-%m-%dT%H:%M:%SZ", "%Y-%m-%dT%H:%M:%S", "%Y-%m-%d"):
        try:
            return datetime.strptime(value, fmt).replace(tzinfo=timezone.utc)
        except ValueError:
            pass
    raise ValueError(f"Cannot parse datetime: {value!r}")


# ── Blob enumeration ──────────────────────────────────────────────────────────

def hour_prefixes(start: datetime, end: datetime, event_type_slug: str) -> list[str]:
    """Return one blob prefix per hour in [start, end)."""
    prefixes = []
    current = start.replace(minute=0, second=0, microsecond=0)
    while current < end:
        prefixes.append(
            f"eventType={event_type_slug}/"
            f"year={current.year:04d}/"
            f"month={current.month:02d}/"
            f"day={current.day:02d}/"
            f"hour={current.hour:02d}/"
        )
        current += timedelta(hours=1)
    return prefixes


def list_blobs(container: ContainerClient, prefixes: list[str]) -> list[str]:
    names = []
    for prefix in prefixes:
        for blob in container.list_blobs(name_starts_with=prefix):
            names.append(blob.name)
    return names


# ── Blob fetching ─────────────────────────────────────────────────────────────

def _read_one(container: ContainerClient, name: str) -> dict | None:
    try:
        # The SDK auto-decompresses Content-Encoding:gzip via the HTTP transport,
        # so readall() returns plain JSON even though the blob is gzip-compressed.
        # Fall back to manual gzip for blobs downloaded without auto-decompress.
        data = container.get_blob_client(name).download_blob().readall()
        if data[:2] == b"\x1f\x8b":
            with gzip.open(io.BytesIO(data)) as f:
                return json.loads(f.read())
        return json.loads(data)
    except Exception as exc:
        print(f"  warning: {name}: {exc}", file=sys.stderr)
        return None


def fetch_events(container: ContainerClient, blob_names: list[str]) -> list[dict]:
    events: list[dict] = []
    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as pool:
        futures = {pool.submit(_read_one, container, n): n for n in blob_names}
        for future in as_completed(futures):
            result = future.result()
            if result is not None:
                events.append(result)
    return events


# ── Arrival analysis ──────────────────────────────────────────────────────────

def _arrival_rows(events: list[dict]) -> list[dict]:
    rows = []
    for ev in events:
        if ev.get("eventType") != "ArrivalPredictionObserved":
            continue
        p = ev.get("payload", {})
        observed = ev.get("observedAtUtc", "")
        try:
            hour = datetime.fromisoformat(observed.replace("Z", "+00:00")).hour
        except ValueError:
            hour = -1
        rows.append({
            "stationId": ev.get("stationId") or p.get("stationId", "unknown"),
            "stationName": p.get("stationName", ""),
            "lineId": p.get("lineId", ""),
            "lineName": p.get("lineName", ""),
            "secondsToStation": int(p.get("secondsToStation", 0)),
            "hour": hour,
        })
    return rows


def analyse_arrivals(events: list[dict], fmt: str, out_dir: Path) -> None:
    rows = _arrival_rows(events)
    if not rows:
        _print_section_header("Arrival Wait Times")
        print("No arrival data in range.\n")
        return

    df = pd.DataFrame(rows)

    station_summary = (
        df.groupby(["stationId", "stationName"])["secondsToStation"]
        .agg(avg="mean", min="min", max="max", count="count")
        .reset_index()
        .sort_values("avg")
    )
    station_summary["avg"] = station_summary["avg"].round(0).astype(int)
    station_summary["min"] = station_summary["min"].astype(int)
    station_summary["max"] = station_summary["max"].astype(int)
    station_summary.columns = ["Station ID", "Station", "Avg (s)", "Min (s)", "Max (s)", "Observations"]

    hourly = (
        df.groupby(["stationName", "hour"])["secondsToStation"]
        .agg(avg="mean", count="count")
        .reset_index()
        .sort_values(["stationName", "hour"])
    )
    hourly["avg"] = hourly["avg"].round(0).astype(int)
    hourly.columns = ["Station", "Hour (UTC)", "Avg Wait (s)", "Observations"]

    peak_hour = (
        df.groupby("hour")["secondsToStation"].mean().idxmax()
    )
    best_station = station_summary.iloc[0]["Station"]

    _render_section(
        "Arrival Wait Times",
        subtitle_tables={
            f"By Station  (total observations: {len(df):,}, peak hour: {peak_hour:02d}:xx UTC)": station_summary,
            f"By Station and Hour  (shortest average wait: {best_station})": hourly,
        },
        fmt=fmt,
        out_dir=out_dir,
        file_stem="arrivals",
    )


# ── Line-status analysis ──────────────────────────────────────────────────────

def _status_rows(events: list[dict]) -> list[dict]:
    rows = []
    for ev in events:
        if ev.get("eventType") != "LineStatusObserved":
            continue
        p = ev.get("payload", {})
        rows.append({
            "lineId": ev.get("lineId") or p.get("lineId", "unknown"),
            "lineName": p.get("lineName", ""),
            "statusSeverity": int(p.get("statusSeverity", -1)),
            "status": p.get("statusSeverityDescription", "Unknown"),
            "reason": p.get("reason") or "",
            "observedAtUtc": ev.get("observedAtUtc", ""),
        })
    return rows


def analyse_status(events: list[dict], fmt: str, out_dir: Path) -> None:
    rows = _status_rows(events)
    if not rows:
        _print_section_header("Line Status")
        print("No line-status data in range.\n")
        return

    df = pd.DataFrame(rows)

    distribution = (
        df.groupby(["lineName", "status"])
        .size()
        .reset_index(name="observations")
        .pivot_table(
            index="lineName", columns="status",
            values="observations", fill_value=0,
        )
        .reset_index()
    )
    distribution.columns.name = None
    distribution = distribution.rename(columns={"lineName": "Line"})

    disruptions = df[df["statusSeverity"] < 10][
        ["lineName", "status", "reason", "observedAtUtc"]
    ].copy().sort_values(["lineName", "observedAtUtc"])
    disruptions.columns = ["Line", "Status", "Reason", "Observed (UTC)"]

    subtitle_tables: dict = {
        f"Status Distribution  (total observations: {len(df):,})": distribution,
    }
    if not disruptions.empty:
        subtitle_tables[f"Disruptions  ({len(disruptions)} observations)"] = disruptions

    _render_section(
        "Line Status",
        subtitle_tables=subtitle_tables,
        fmt=fmt,
        out_dir=out_dir,
        file_stem="line_status",
    )


# ── Rendering ─────────────────────────────────────────────────────────────────

_TABULATE_FMT = {
    "console": "rounded_outline",
    "markdown": "github",
}


def _print_section_header(title: str) -> None:
    print(f"\n{'─' * 60}")
    print(f"  {title}")
    print(f"{'─' * 60}")


def _render_section(
    title: str,
    subtitle_tables: dict[str, pd.DataFrame],
    fmt: str,
    out_dir: Path,
    file_stem: str,
) -> None:
    _print_section_header(title)
    for subtitle, df in subtitle_tables.items():
        print(f"\n{subtitle}")
        if fmt == "csv":
            slug = subtitle.split()[0].lower()
            path = out_dir / f"{file_stem}_{slug}.csv"
            df.to_csv(path, index=False)
            print(f"  → {path}")
        elif fmt == "markdown":
            path = out_dir / f"{file_stem}_{subtitle.split()[0].lower()}.md"
            md = tabulate(df, headers="keys", tablefmt="github", showindex=False)
            path.write_text(f"## {subtitle}\n\n{md}\n")
            print(md)
            print(f"\n  → {path}")
        else:
            print(tabulate(df, headers="keys", tablefmt="rounded_outline", showindex=False))


# ── HTML report ───────────────────────────────────────────────────────────────

_LINE_COLORS = [
    "#003688", "#DC241F", "#009FE0", "#000000", "#6950A1",
    "#007229", "#E86A10", "#9B0058", "#00AFAD", "#84B817",
]

_STATUS_BADGE = {
    "Good Service":   "success",
    "Minor Delays":   "warning text-dark",
    "Severe Delays":  "danger",
    "Part Suspended": "secondary",
    "Suspended":      "dark",
    "Part Closure":   "secondary",
    "Planned Closure":"secondary",
}


def build_html_report(
    events: list[dict],
    start: datetime,
    end: datetime,
    out_dir: Path,
) -> Path:
    """Render a self-contained HTML report and return its path."""
    arr_rows = _arrival_rows(events)
    stat_rows = _status_rows(events)

    # ── Arrival aggregates ────────────────────────────────────────────────
    if arr_rows:
        df_arr = pd.DataFrame(arr_rows)
        station_agg = (
            df_arr.groupby(["stationId", "stationName"])["secondsToStation"]
            .agg(avg="mean", min="min", max="max", count="count")
            .reset_index()
            .sort_values("avg")
        )
        hourly_agg = (
            df_arr.groupby(["stationName", "hour"])["secondsToStation"]
            .mean()
            .reset_index()
            .sort_values(["stationName", "hour"])
        )
    else:
        station_agg = pd.DataFrame()
        hourly_agg = pd.DataFrame()

    # ── Status aggregates ─────────────────────────────────────────────────
    if stat_rows:
        df_stat = pd.DataFrame(stat_rows)
        status_dist = (
            df_stat.groupby(["lineName", "status", "statusSeverity"])
            .size()
            .reset_index(name="count")
            .sort_values(["lineName", "statusSeverity"])
        )
        disruptions = df_stat[df_stat["statusSeverity"] < 10].copy()
    else:
        status_dist = pd.DataFrame()
        disruptions = pd.DataFrame()

    # ── Summary numbers ───────────────────────────────────────────────────
    total_events = len(events)
    arrival_count = len(arr_rows)
    station_count = int(station_agg["stationId"].nunique()) if not station_agg.empty else 0
    disruption_count = len(disruptions) if not disruptions.empty else 0

    # ── Chart.js data ─────────────────────────────────────────────────────
    if not station_agg.empty:
        chart_station_labels = json.dumps(station_agg["stationName"].tolist())
        chart_station_avgs   = json.dumps([round(float(v)) for v in station_agg["avg"].tolist()])
    else:
        chart_station_labels = "[]"
        chart_station_avgs   = "[]"

    if not hourly_agg.empty:
        all_hours = sorted(hourly_agg["hour"].unique().tolist())
        datasets = []
        for i, (station, grp) in enumerate(hourly_agg.groupby("stationName")):
            hour_map = dict(zip(grp["hour"].tolist(), grp["secondsToStation"].round(0).tolist()))
            data = [round(hour_map[h]) if h in hour_map else None for h in all_hours]
            color = _LINE_COLORS[i % len(_LINE_COLORS)]
            datasets.append({
                "label": station,
                "data": data,
                "borderColor": color,
                "backgroundColor": color + "33",
                "fill": False,
                "tension": 0.3,
                "spanGaps": True,
            })
        hourly_labels_js   = json.dumps([f"{h:02d}:00" for h in all_hours])
        hourly_datasets_js = json.dumps(datasets)
    else:
        hourly_labels_js   = "[]"
        hourly_datasets_js = "[]"

    # ── Station table rows ────────────────────────────────────────────────
    if not station_agg.empty:
        station_rows_html = ""
        for _, row in station_agg.iterrows():
            avg_s = int(row["avg"])
            color = "#28a745" if avg_s < 120 else "#ffc107" if avg_s < 240 else "#dc3545"
            station_rows_html += (
                f'<tr>'
                f'<td><strong>{row["stationName"]}</strong>'
                f'<br><small class="text-muted">{row["stationId"]}</small></td>'
                f'<td><span style="color:{color};font-weight:700">'
                f'{avg_s}s</span> <small>({avg_s // 60}m {avg_s % 60}s)</small></td>'
                f'<td>{int(row["min"])}s</td>'
                f'<td>{int(row["max"])}s</td>'
                f'<td>{int(row["count"]):,}</td>'
                f'</tr>\n'
            )
    else:
        station_rows_html = '<tr><td colspan="5" class="text-center text-muted py-3">No arrival data</td></tr>'

    # ── Line status rows ──────────────────────────────────────────────────
    if not status_dist.empty:
        status_rows_html = ""
        for line, grp in status_dist.groupby("lineName"):
            total = int(grp["count"].sum())
            badges = "".join(
                f'<span class="badge bg-{_STATUS_BADGE.get(row["status"], "info")} me-1">'
                f'{row["status"]} ({round(row["count"] / total * 100)}%)</span>'
                for _, row in grp.iterrows()
            )
            status_rows_html += f'<tr><td><strong>{line}</strong></td><td>{badges}</td><td>{total:,}</td></tr>\n'
    else:
        status_rows_html = '<tr><td colspan="3" class="text-center text-muted py-3">No status data</td></tr>'

    # ── Disruptions section ───────────────────────────────────────────────
    if not disruptions.empty:
        d_rows = ""
        for _, row in disruptions.sort_values(["lineName", "observedAtUtc"]).iterrows():
            badge = _STATUS_BADGE.get(row["status"], "info")
            reason = row["reason"]
            if len(reason) > 120:
                reason = reason[:120] + "…"
            d_rows += (
                f'<tr>'
                f'<td>{row["lineName"]}</td>'
                f'<td><span class="badge bg-{badge}">{row["status"]}</span></td>'
                f'<td><small>{reason or "—"}</small></td>'
                f'<td><small class="text-muted">{row["observedAtUtc"]}</small></td>'
                f'</tr>\n'
            )
        disruptions_html = (
            '<div class="card mb-4">'
            '<div class="card-header bg-danger text-white"><h5 class="mb-0">Disruptions</h5></div>'
            '<div class="card-body p-0">'
            '<table class="table mb-0">'
            '<thead class="table-dark"><tr><th>Line</th><th>Status</th><th>Reason</th><th>Observed (UTC)</th></tr></thead>'
            f'<tbody>{d_rows}</tbody></table></div></div>'
        )
    else:
        disruptions_html = '<div class="alert alert-success mb-4">No disruptions recorded in this period.</div>'

    generated = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    period    = f"{start.strftime('%Y-%m-%d %H:%M')} &#8594; {end.strftime('%Y-%m-%d %H:%M')} UTC"

    # ── HTML template ─────────────────────────────────────────────────────
    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>TfL Analytics Report</title>
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet">
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.4/dist/chart.umd.min.js"></script>
<style>
  body {{ background:#f0f2f5 }}
  .report-header {{ background:#003688; color:#fff; padding:2rem; border-radius:.5rem; margin-bottom:1.5rem }}
  .report-header p {{ opacity:.8; margin:0 }}
  .stat-card {{ text-align:center; padding:1.25rem 0 }}
  .stat-card .number {{ font-size:2.4rem; font-weight:700; color:#003688; line-height:1 }}
  .stat-card .label  {{ color:#6c757d; font-size:.85rem; margin-top:.25rem }}
</style>
</head>
<body>
<div class="container py-4">

  <div class="report-header">
    <h1 class="mb-2">TfL Live Analytics Report</h1>
    <p>Period: {period} &nbsp;|&nbsp; Generated: {generated}</p>
  </div>

  <div class="row g-3 mb-4">
    <div class="col-6 col-md-3"><div class="card stat-card h-100">
      <div class="number">{total_events:,}</div><div class="label">Total Events</div>
    </div></div>
    <div class="col-6 col-md-3"><div class="card stat-card h-100">
      <div class="number">{arrival_count:,}</div><div class="label">Arrival Observations</div>
    </div></div>
    <div class="col-6 col-md-3"><div class="card stat-card h-100">
      <div class="number">{station_count}</div><div class="label">Stations Monitored</div>
    </div></div>
    <div class="col-6 col-md-3"><div class="card stat-card h-100">
      <div class="number">{disruption_count}</div><div class="label">Disruption Observations</div>
    </div></div>
  </div>

  <div class="card mb-4">
    <div class="card-header"><h5 class="mb-0">Average Arrival Wait by Station</h5></div>
    <div class="card-body"><canvas id="stationChart" height="80"></canvas></div>
  </div>

  <div class="card mb-4">
    <div class="card-header"><h5 class="mb-0">Arrival Statistics</h5></div>
    <div class="card-body p-0">
      <table class="table table-striped table-hover mb-0">
        <thead class="table-dark">
          <tr><th>Station</th><th>Avg Wait</th><th>Min</th><th>Max</th><th>Observations</th></tr>
        </thead>
        <tbody>{station_rows_html}</tbody>
      </table>
    </div>
  </div>

  <div class="card mb-4">
    <div class="card-header"><h5 class="mb-0">Average Wait by Hour (UTC)</h5></div>
    <div class="card-body"><canvas id="hourlyChart" height="100"></canvas></div>
  </div>

  <div class="card mb-4">
    <div class="card-header"><h5 class="mb-0">Line Status Distribution</h5></div>
    <div class="card-body p-0">
      <table class="table mb-0">
        <thead class="table-dark">
          <tr><th>Line</th><th>Status</th><th>Observations</th></tr>
        </thead>
        <tbody>{status_rows_html}</tbody>
      </table>
    </div>
  </div>

  {disruptions_html}

</div>
<script>
new Chart(document.getElementById('stationChart'), {{
  type: 'bar',
  data: {{
    labels: {chart_station_labels},
    datasets: [{{
      label: 'Avg Wait (s)',
      data: {chart_station_avgs},
      backgroundColor: 'rgba(0,54,136,0.75)',
      borderColor: '#003688',
      borderWidth: 1
    }}]
  }},
  options: {{
    responsive: true,
    plugins: {{ legend: {{ display: false }} }},
    scales: {{ y: {{ beginAtZero: true, title: {{ display: true, text: 'Seconds' }} }} }}
  }}
}});

new Chart(document.getElementById('hourlyChart'), {{
  type: 'line',
  data: {{
    labels: {hourly_labels_js},
    datasets: {hourly_datasets_js}
  }},
  options: {{
    responsive: true,
    plugins: {{ legend: {{ position: 'bottom' }} }},
    scales: {{
      y: {{ beginAtZero: true, title: {{ display: true, text: 'Avg Wait (s)' }} }},
      x: {{ title: {{ display: true, text: 'Hour (UTC)' }} }}
    }}
  }}
}});
</script>
</body>
</html>"""

    path = out_dir / "report.html"
    path.write_text(html, encoding="utf-8")
    return path


# ── Main ──────────────────────────────────────────────────────────────────────

def main() -> None:
    args = parse_args()
    start = parse_dt(args.from_dt)
    end = parse_dt(args.to_dt)

    if end <= start:
        sys.exit("--to must be after --from")

    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    import os
    key = os.environ.get("AZURE_STORAGE_KEY")
    if key:
        conn = f"DefaultEndpointsProtocol=https;AccountName={args.account};AccountKey={key};EndpointSuffix=core.windows.net"
        container = ContainerClient.from_connection_string(conn, args.container)
    else:
        url = f"https://{args.account}.blob.core.windows.net"
        container = ContainerClient(url, args.container, credential=DefaultAzureCredential())

    event_types = (
        ["arrival", "line-status"] if args.event_type == "all"
        else [args.event_type]
    )

    print(f"Account  : {args.account}/{args.container}")
    print(f"Period   : {start.strftime('%Y-%m-%d %H:%M UTC')} → {end.strftime('%Y-%m-%d %H:%M UTC')}")
    print(f"Events   : {', '.join(event_types)}")
    print()

    blob_names: list[str] = []
    for et in event_types:
        prefixes = hour_prefixes(start, end, et)
        found = list_blobs(container, prefixes)
        print(f"  {et}: {len(found)} blobs across {len(prefixes)} hour partition(s)")
        blob_names.extend(found)

    if not blob_names:
        print("\nNo blobs found in the specified range.")
        return

    print(f"\nFetching {len(blob_names)} blobs …")
    events = fetch_events(container, blob_names)
    print(f"Loaded {len(events)} events.")

    print(f"\n# TfL Raw Event Analytics")
    print(f"Period  : {start.strftime('%Y-%m-%d %H:%M UTC')} → {end.strftime('%Y-%m-%d %H:%M UTC')}")
    print(f"Blobs   : {len(blob_names)}   Events: {len(events)}")

    if args.output == "html":
        report_path = build_html_report(events, start, end, out_dir)
        print(f"\nHTML report: {report_path}")
        print(f"Open with:  open '{report_path}'")
    else:
        if "arrival" in event_types:
            analyse_arrivals(events, args.output, out_dir)
        if "line-status" in event_types:
            analyse_status(events, args.output, out_dir)

    print()


if __name__ == "__main__":
    main()
