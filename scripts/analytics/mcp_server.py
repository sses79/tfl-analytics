#!/usr/bin/env python3
"""
TfL Analytics MCP server.

Exposes analyse_tfl_raw as a Claude tool so Claude Desktop can trigger
an analytics run and generate an HTML report from raw ADLS Gen2 events.

Setup:
    pip install -r requirements.txt
    # Add to ~/Library/Application Support/Claude/claude_desktop_config.json:
    # {
    #   "mcpServers": {
    #     "tfl-analytics": {
    #       "command": "python3",
    #       "args": ["/Users/tim/Ware/tfl-analytics/scripts/analytics/mcp_server.py"]
    #     }
    #   }
    # }
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

from mcp.server.fastmcp import FastMCP

SCRIPT    = Path(__file__).parent / "analyse_raw.py"
REPORTS   = Path.home() / "tfl-reports"

mcp = FastMCP("TfL Analytics")


@mcp.tool()
def analyse_tfl_raw(
    from_dt: str = "",
    to_dt: str = "",
    event_type: str = "all",
    account: str = "sttflnhkpyupi",
) -> str:
    """
    Analyse TfL live event data archived in Azure Data Lake Gen2 and generate
    an HTML report with charts.

    Reads compressed event envelopes from the raw blob container, computes
    arrival wait-time statistics per station and hour, and line-status
    distributions. Saves a self-contained Bootstrap + Chart.js HTML page.

    Args:
        from_dt:    Start of the analysis window, ISO 8601
                    (e.g. "2026-06-14T08:00:00Z"). Defaults to 24 h ago.
        to_dt:      End of the analysis window, ISO 8601. Defaults to now.
        event_type: Which events to include — "arrival", "line-status", or
                    "all" (default).
        account:    Azure Storage account name (default: sttflnhkpyupi).

    Returns:
        A plain-text summary of what was found and the path to the HTML report.
    """
    REPORTS.mkdir(parents=True, exist_ok=True)

    cmd = [
        sys.executable, str(SCRIPT),
        "--output", "html",
        "--out-dir", str(REPORTS),
        "--account", account,
        "--event-type", event_type,
    ]
    if from_dt:
        cmd += ["--from", from_dt]
    if to_dt:
        cmd += ["--to", to_dt]

    result = subprocess.run(cmd, capture_output=True, text=True, timeout=180)

    if result.returncode != 0:
        error = result.stderr.strip() or result.stdout.strip()
        return f"Analysis failed:\n{error}"

    report = REPORTS / "report.html"
    return f"{result.stdout.strip()}\n\nReport saved to: {report}"


if __name__ == "__main__":
    mcp.run()
