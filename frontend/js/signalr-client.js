import { store, setSlice } from './store.js';
import { toast } from './components.js';

let connection = null;
let jobStatusHub = null;
let reconnectAttempts = 0;
const MAX_RECONNECT_ATTEMPTS = 5;
const RECONNECT_DELAY_MS = 3000;

export function initSignalR(baseUrl) {
  if (connection) return connection;

  const existingScript = document.querySelector('script[src*="signalr"]');
  if (!existingScript) {
    const script = document.createElement('script');
    script.src = `${baseUrl}/libs/signalr/signalr.min.js`;
    script.async = true;
    document.head.appendChild(script);
  }

  return new Promise((resolve) => {
    const checkSignalR = setInterval(() => {
      if (typeof signalR !== 'undefined') {
        clearInterval(checkSignalR);
        resolve(signalR);
      }
    }, 100);
  });
}

export async function connectToJobStatusHub(baseUrl) {
  const signalR = await initSignalR(baseUrl);

  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    return connection;
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${baseUrl}/hubs/jobs`)
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: () => {
        if (reconnectAttempts >= MAX_RECONNECT_ATTEMPTS) return null;
        reconnectAttempts++;
        return RECONNECT_DELAY_MS * reconnectAttempts;
      },
    })
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  connection.on('JobStatusChanged', (job) => {
    handleJobStatusChanged(job);
  });

  connection.on('JobCompleted', (job) => {
    handleJobCompleted(job);
  });

  connection.on('JobFailed', (job) => {
    handleJobFailed(job);
  });

  connection.onclose(() => {
    console.warn('[SignalR] Connection closed');
    updateConnectionStatus(false);
  });

  connection.onreconnected(() => {
    console.info('[SignalR] Reconnected');
    reconnectAttempts = 0;
    updateConnectionStatus(true);
  });

  connection.onreconnecting(() => {
    console.warn('[SignalR] Reconnecting...');
    updateConnectionStatus(false);
  });

  try {
    await connection.start();
    console.info('[SignalR] Connected to JobStatusHub');
    updateConnectionStatus(true);
    reconnectAttempts = 0;
    return connection;
  } catch (err) {
    console.error('[SignalR] Failed to connect', err);
    updateConnectionStatus(false);
    throw err;
  }
}

function handleJobStatusChanged(job) {
  const jobs = store.getState().jobs;
  if (jobs && jobs.items) {
    const updatedItems = jobs.items.map((j) =>
      j.id === job.id ? { ...j, ...job } : j
    );
    setSlice('jobs', { items: updatedItems });
  }

  if (job.id) {
    window.dispatchEvent(new CustomEvent('jobstatuschanged', { detail: job }));
  }
}

function handleJobCompleted(job) {
  handleJobStatusChanged(job);

  toast(`Job completed: ${formatJobType(job.type || 'Unknown')}`, 'success');

  window.dispatchEvent(new CustomEvent('jobcompleted', { detail: job }));
}

function handleJobFailed(job) {
  handleJobStatusChanged(job);

  const errorMsg = job.errorMessage || 'Job failed';
  toast(`Job failed: ${errorMsg}`, 'error');

  window.dispatchEvent(new CustomEvent('jobfailed', { detail: job }));
}

function formatJobType(type) {
  const map = {
    ImportPapers: 'Import Papers',
    SummarizePaper: 'Summarize Paper',
    GenerateEmbeddings: 'Generate Embeddings',
    Analysis: 'Analysis',
    ProcessPaperDocument: 'Process Document',
  };
  return map[type] || type;
}

function updateConnectionStatus(connected) {
  const statusEl = document.getElementById('connection-status');
  if (!statusEl) return;

  const dot = statusEl.querySelector('.status-dot');
  const text = statusEl.querySelector('.status-text');

  if (dot) {
    dot.style.backgroundColor = connected ? 'var(--c-green)' : 'var(--c-red)';
  }
  if (text) {
    text.textContent = connected ? 'LIVE' : 'OFFLINE';
  }
}

export async function disconnect() {
  if (connection) {
    try {
      await connection.stop();
    } catch (err) {
      console.warn('[SignalR] Error stopping connection', err);
    }
    connection = null;
  }
}

export function getConnection() {
  return connection;
}

export const JobStatusClient = {
  connect: connectToJobStatusHub,
  disconnect,
  getConnection,
  onJobStatusChanged: (handler) => {
    if (connection) {
      connection.on('JobStatusChanged', handler);
    }
  },
  onJobCompleted: (handler) => {
    if (connection) {
      connection.on('JobCompleted', handler);
    }
  },
  onJobFailed: (handler) => {
    if (connection) {
      connection.on('JobFailed', handler);
    }
  },
};

export default JobStatusClient;