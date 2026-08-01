import * as signalR from '@microsoft/signalr';

const apiBaseUrl = process.env.API_BASE_URL;
const ingestionPullUrl = process.env.INGESTION_PULL_URL;
const timeoutMilliseconds = Number(process.env.SIGNALR_TIMEOUT_MS ?? '180000');
const pullIntervalMilliseconds = Number(process.env.SIGNALR_PULL_INTERVAL_MS ?? '30000');

if (!apiBaseUrl || !ingestionPullUrl) {
  console.error('API_BASE_URL and INGESTION_PULL_URL are required.');
  process.exit(1);
}

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${apiBaseUrl.replace(/\/$/, '')}/hubs/dashboard`)
  .configureLogging(signalR.LogLevel.Warning)
  .build();

let timeout;
let pullInterval;
let latestPullResult;

try {
  const received = new Promise((resolve, reject) => {
    timeout = setTimeout(
      () => reject(new Error(`No lineStatusChanged message received within ${timeoutMilliseconds}ms.`)),
      timeoutMilliseconds
    );

    connection.on('lineStatusChanged', message => resolve(message));
  });

  await connection.start();

  const pull = async () => {
    const pullResponse = await fetch(ingestionPullUrl, { method: 'POST' });
    if (!pullResponse.ok) {
      throw new Error(`Manual ingestion pull failed with HTTP ${pullResponse.status}.`);
    }

    latestPullResult = await pullResponse.json();
  };

  await pull();
  pullInterval = setInterval(() => {
    pull().catch(error => console.warn(error.message));
  }, pullIntervalMilliseconds);

  const message = await received;

  console.log(JSON.stringify({
    connected: true,
    lineStatusPublished: latestPullResult.lineStatusPublished,
    receivedTarget: 'lineStatusChanged',
    lineId: message.lineId,
    observedAtUtc: message.observedAtUtc
  }));
} finally {
  clearTimeout(timeout);
  clearInterval(pullInterval);
  await connection.stop();
}
