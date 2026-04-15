// ═══════════════════════════════════════════════════
// ARA API Client — with caching and error handling
// ═══════════════════════════════════════════════════

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

const CACHE = new Map();
const CACHE_TTL = 30_000; // 30 seconds

function getConfig() {
  return {
    baseUrl: localStorage.getItem('ara_api_url') ?? 'http://localhost:5000',
    token: localStorage.getItem('ara_api_token') || '',
  };
}

function saveConfig(baseUrl, token) {
  localStorage.setItem('ara_api_url', baseUrl);
  localStorage.setItem('ara_api_token', token);
  CACHE.clear();
}

function headers() {
  const h = { 'Content-Type': 'application/json', Accept: 'application/json' };
  const { token } = getConfig();
  if (token) h['Authorization'] = token.startsWith('Bearer ') ? token : `Bearer ${token}`;
  return h;
}

function cacheKey(method, path, body) {
  return `${method}:${path}:${body ? JSON.stringify(body) : ''}`;
}

function getCached(key) {
  const entry = CACHE.get(key);
  if (!entry) return null;
  if (Date.now() - entry.ts > CACHE_TTL) {
    CACHE.delete(key);
    return null;
  }
  return entry.data;
}

function setCache(key, data) {
  CACHE.set(key, { data, ts: Date.now() });
}

function invalidatePrefix(prefix) {
  for (const key of CACHE.keys()) {
    if (key.startsWith(prefix)) CACHE.delete(key);
  }
}

async function request(method, path, body = null, { cache = false, signal } = {}) {
  const { baseUrl } = getConfig();
  const url = `${baseUrl}${path}`;
  const key = cacheKey(method, path, body);

  if (cache && method === 'GET') {
    const cached = getCached(key);
    if (cached) return cached;
  }

  const opts = { method, headers: headers(), signal };
  if (body && method !== 'GET') opts.body = JSON.stringify(body);

  const res = await fetch(url, opts);

  if (!res.ok) {
    let detail = '';
    try {
      const err = await res.json();
      detail = err.detail || err.title || err.message || JSON.stringify(err);
    } catch {
      detail = res.statusText;
    }
    throw new ApiError(res.status, detail);
  }

  // 204 No Content
  if (res.status === 204) return null;

  const data = await res.json();
  if (cache && method === 'GET') setCache(key, data);
  return data;
}

export class ApiError extends Error {
  constructor(status, detail) {
    super(`${status}: ${detail}`);
    this.status = status;
    this.detail = detail;
  }
}

// ── Health ────────────────────────────────────────

export async function checkHealth(signal) {
  const { baseUrl } = getConfig();
  const res = await fetch(`${baseUrl}/health`, { headers: headers(), signal });
  return res.ok;
}

// ── Papers ───────────────────────────────────────

export function getPapers(params = {}, signal) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== '') qs.set(k, v);
  }
  return request('GET', `/api/v1/papers?${qs}`, null, { cache: true, signal });
}

export function getPaper(id, signal) {
  return request('GET', `/api/v1/papers/${id}`, null, { cache: true, signal });
}

export function createPaper(data) {
  invalidatePrefix('/api/v1/papers');
  return request('POST', '/api/v1/papers', data);
}

export function updatePaper(id, data) {
  invalidatePrefix('/api/v1/papers');
  return request('PATCH', `/api/v1/papers/${id}`, data);
}

export function importPapers(data) {
  invalidatePrefix('/api/v1/papers');
  return request('POST', '/api/v1/papers/import', data);
}

export async function exportPapers(format) {
  const { baseUrl, token } = getConfig();
  const h = { Accept: '*/*' };
  if (token) h['Authorization'] = token.startsWith('Bearer ') ? token : `Bearer ${token}`;
  const res = await fetch(`${baseUrl}/api/v1/papers/export?format=${encodeURIComponent(format)}`, { headers: h });
  if (!res.ok) {
    let detail = '';
    try {
      const err = await res.json();
      detail = err.detail || err.title || err.message || JSON.stringify(err);
    } catch {
      detail = res.statusText;
    }
    throw new ApiError(res.status, detail);
  }
  const blob = await res.blob();
  const contentDisposition = res.headers.get('Content-Disposition');
  const filename = contentDisposition?.match(/filename="(.+)"/)?.[1] || `papers.${format}`;
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

// ── Jobs ─────────────────────────────────────────

export function getJobs(params = {}, signal) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== '') qs.set(k, v);
  }
  return request('GET', `/api/v1/jobs?${qs}`, null, { cache: true, signal });
}

export function getJob(id, signal) {
  return request('GET', `/api/v1/jobs/${id}`, null, { cache: true, signal });
}

export function createImportJob(data) {
  invalidatePrefix('/api/v1/jobs');
  return request('POST', '/api/v1/jobs/import-papers', data);
}

export function createSummarizeJob(data) {
  invalidatePrefix('/api/v1/jobs');
  return request('POST', '/api/v1/jobs/summarize-paper', data);
}

export function retryJob(id, data = {}) {
  invalidatePrefix('/api/v1/jobs');
  return request('POST', `/api/v1/jobs/${id}/retry`, data);
}

// ── Summaries ────────────────────────────────────

export function getPaperSummaries(paperId, signal) {
  return request('GET', `/api/v1/papers/${paperId}/summaries`, null, { cache: true, signal });
}

export function getSummary(summaryId, signal) {
  return request('GET', `/api/v1/summaries/${summaryId}`, null, { cache: true, signal });
}

export function createSummary(paperId, data) {
  invalidatePrefix('/api/v1/summaries');
  invalidatePrefix(`/api/v1/papers/${paperId}/summaries`);
  return request('POST', `/api/v1/papers/${paperId}/summaries`, data);
}

export function updateSummary(summaryId, data) {
  invalidatePrefix('/api/v1/summaries');
  return request('PATCH', `/api/v1/summaries/${summaryId}`, data);
}

export function approveSummary(summaryId, data = {}) {
  invalidatePrefix('/api/v1/summaries');
  return request('POST', `/api/v1/summaries/${summaryId}/approve`, data);
}

export function rejectSummary(summaryId, data = {}) {
  invalidatePrefix('/api/v1/summaries');
  return request('POST', `/api/v1/summaries/${summaryId}/reject`, data);
}

// ── Search ───────────────────────────────────────

export function searchKeyword(params = {}, signal) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== '') qs.set(k, v);
  }
  return request('GET', `/api/v1/search?${qs}`, null, { cache: true, signal });
}

export function searchSemantic(data, signal) {
  return request('POST', '/api/v1/search/semantic', data, { signal });
}

export function searchHybrid(data, signal) {
  return request('POST', '/api/v1/search/hybrid', data, { signal });
}

// ── Analysis ─────────────────────────────────────

export function comparePapers(data) {
  return request('POST', '/api/v1/analysis/compare-papers', data);
}

export function compareFields(data) {
  return request('POST', '/api/v1/analysis/compare-fields', data);
}

export function generateInsights(data) {
  return request('POST', '/api/v1/analysis/generate-insights', data);
}

export function getAnalysisJob(jobId, signal) {
  return request('GET', `/api/v1/analysis/${jobId}`, null, { signal });
}

// ── Documents ────────────────────────────────────

export function getPaperDocuments(paperId, signal) {
  return request('GET', `/api/v1/papers/${paperId}/documents`, null, { cache: true, signal });
}

export function createPaperDocument(paperId, data) {
  invalidatePrefix(`/api/v1/papers/${paperId}/documents`);
  return request('POST', `/api/v1/papers/${paperId}/documents`, data);
}

export function queueDocumentProcessing(paperId, documentId, data = {}) {
  invalidatePrefix(`/api/v1/papers/${paperId}/documents`);
  return request('POST', `/api/v1/papers/${paperId}/documents/${documentId}/queue-processing`, data);
}

export function getCitationGraph(paperId, params = {}, signal) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== '') qs.set(k, v);
  }
  return request('GET', `/api/v1/papers/${paperId}/graph?${qs}`, null, { cache: true, signal });
}

export function ingestCitations(paperId) {
  invalidatePrefix(`/api/v1/papers/${paperId}`);
  return request('POST', `/api/v1/papers/${paperId}/ingest-citations`, null);
}

// ── Annotations ─────────────────────────────────

export function getPaperAnnotations(paperId, userId = null, signal) {
  const qs = new URLSearchParams();
  if (userId != null) qs.set('userId', userId);
  const query = qs.toString() ? `?${qs}` : '';
  return request('GET', `/api/v1/papers/${paperId}/annotations${query}`, null, { cache: true, signal });
}

export function getAnnotation(annotationId, signal) {
  return request('GET', `/api/v1/annotations/${annotationId}`, null, { cache: true, signal });
}

export function createAnnotation(paperId, data) {
  invalidatePrefix(`/api/v1/papers/${paperId}/annotations`);
  return request('POST', `/api/v1/papers/${paperId}/annotations`, data);
}

export function updateAnnotation(annotationId, data) {
  invalidatePrefix('/api/v1/annotations');
  return request('PUT', `/api/v1/annotations/${annotationId}`, data);
}

export function deleteAnnotation(annotationId) {
  invalidatePrefix('/api/v1/annotations');
  return request('DELETE', `/api/v1/annotations/${annotationId}`);
}

// ── Trends ───────────────────────────────────────

export function getTrends(params = {}, signal) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== '') qs.set(k, v);
  }
  return request('GET', `/api/v1/trends?${qs}`, null, { cache: true, signal });
}

// ── Chat ─────────────────────────────────────────

export async function streamChat(data, { signal } = {}) {
  const { baseUrl, token } = getConfig();
  const url = `${baseUrl}/api/v1/chat`;
  const h = { 'Content-Type': 'application/json' };
  if (token) h['Authorization'] = token.startsWith('Bearer ') ? token : `Bearer ${token}`;

  const res = await fetch(url, {
    method: 'POST',
    headers: h,
    body: JSON.stringify(data),
    signal,
  });

  if (!res.ok) {
    let detail = '';
    try {
      const err = await res.json();
      detail = err.detail || err.title || err.message || JSON.stringify(err);
    } catch {
      detail = res.statusText;
    }
    throw new ApiError(res.status, detail);
  }

  const reader = res.body.getReader();
  const decoder = new TextDecoder();

  return {
    async *[Symbol.asyncIterator]() {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        yield decoder.decode(value, { stream: true });
      }
    }
  };
}

export async function postChat(data, { signal } = {}) {
  return request('POST', '/api/v1/chat/sources', data, { signal });
}

// ── Saved Searches / Watchlist ──────────────────

export function getSavedSearches(params = {}, signal) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== '') qs.set(k, v);
  }
  return request('GET', `/api/v1/saved-searches?${qs}`, null, { cache: true, signal });
}

export function getSavedSearch(id, signal) {
  return request('GET', `/api/v1/saved-searches/${id}`, null, { cache: true, signal });
}

export function createSavedSearch(data) {
  invalidatePrefix('/api/v1/saved-searches');
  return request('POST', '/api/v1/saved-searches', data);
}

export function updateSavedSearch(id, data) {
  invalidatePrefix('/api/v1/saved-searches');
  return request('PUT', `/api/v1/saved-searches/${id}`, data);
}

export function deleteSavedSearch(id) {
  invalidatePrefix('/api/v1/saved-searches');
  return request('DELETE', `/api/v1/saved-searches/${id}`);
}

export function runSavedSearch(id) {
  invalidatePrefix('/api/v1/saved-searches');
  return request('POST', `/api/v1/saved-searches/${id}/run`);
}

// ── Notifications ────────────────────────────────

export function getNotifications(params = {}, signal) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== '') qs.set(k, v);
  }
  return request('GET', `/api/v1/notifications?${qs}`, null, { cache: true, signal });
}

export function getUnreadNotificationCount(signal) {
  return request('GET', '/api/v1/notifications/unread-count', null, { cache: true, signal });
}

export function markNotificationAsRead(id) {
  invalidatePrefix('/api/v1/notifications');
  return request('PUT', `/api/v1/notifications/${id}/read`);
}

export function markAllNotificationsAsRead() {
  invalidatePrefix('/api/v1/notifications');
  return request('PUT', '/api/v1/notifications/read-all');
}

// ── Hypotheses ────────────────────────────────────

export function getHypotheses(signal) {
  return request('GET', '/api/v1/hypotheses', null, { cache: true, signal });
}

export function getHypothesis(id, signal) {
  return request('GET', `/api/v1/hypotheses/${id}`, null, { cache: true, signal });
}

export function createHypothesis(data) {
  invalidatePrefix('/api/v1/hypotheses');
  return request('POST', '/api/v1/hypotheses', data);
}

export function updateHypothesis(id, data) {
  invalidatePrefix('/api/v1/hypotheses');
  return request('PUT', `/api/v1/hypotheses/${id}`, data);
}

export function updateHypothesisStatus(id, data) {
  invalidatePrefix('/api/v1/hypotheses');
  return request('PUT', `/api/v1/hypotheses/${id}/status`, data);
}

export function deleteHypothesis(id) {
  invalidatePrefix('/api/v1/hypotheses');
  return request('DELETE', `/api/v1/hypotheses/${id}`);
}

export function addHypothesisPaper(hypothesisId, data) {
  invalidatePrefix('/api/v1/hypotheses');
  return request('POST', `/api/v1/hypotheses/${hypothesisId}/papers`, data);
}

export function removeHypothesisPaper(hypothesisId, paperId) {
  invalidatePrefix('/api/v1/hypotheses');
  return request('DELETE', `/api/v1/hypotheses/${hypothesisId}/papers/${paperId}`);
}

// ── Reading List ─────────────────────────────────

export function getReadingList(params = {}, signal) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== '') qs.set(k, v);
  }
  return request('GET', `/api/v1/me/reading-list?${qs}`, null, { cache: true, signal });
}

export function getReadingSession(id, signal) {
  return request('GET', `/api/v1/me/reading-list/${id}`, null, { cache: true, signal });
}

export function createReadingSession(data) {
  invalidatePrefix('/api/v1/me/reading-list');
  return request('POST', '/api/v1/me/reading-list', data);
}

export function updateReadingSession(id, data) {
  invalidatePrefix('/api/v1/me/reading-list');
  return request('PUT', `/api/v1/me/reading-list/${id}`, data);
}

export function deleteReadingSession(id) {
  invalidatePrefix('/api/v1/me/reading-list');
  return request('DELETE', `/api/v1/me/reading-list/${id}`);
}

// ── Recommendations ───────────────────────────────

export function getRecommendations(params = {}, signal) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== '') qs.set(k, v);
  }
  return request('GET', `/api/v1/me/recommendations?${qs}`, null, { cache: true, signal });
}

// ── Webhooks ─────────────────────────────────────

export function getWebhooks(signal) {
  return request('GET', '/api/v1/me/webhooks', null, { cache: false, signal });
}

export function createWebhook(data) {
  invalidatePrefix('/api/v1/me/webhooks');
  return request('POST', '/api/v1/me/webhooks', data);
}

export function deleteWebhook(id) {
  invalidatePrefix('/api/v1/me/webhooks');
  return request('DELETE', `/api/v1/me/webhooks/${id}`);
}

// ── Collections ─────────────────────────────────

export async function exportCollection(collectionId, filename) {
  const { baseUrl, token } = getConfig();
  const h = { Accept: '*/*' };
  if (token) h['Authorization'] = token.startsWith('Bearer ') ? token : `Bearer ${token}`;
  const res = await fetch(`${baseUrl}/api/v1/collections/${collectionId}/export`, { headers: h });
  if (!res.ok) {
    let detail = '';
    try {
      const err = await res.json();
      detail = err.detail || err.title || err.message || JSON.stringify(err);
    } catch {
      detail = res.statusText;
    }
    throw new ApiError(res.status, detail);
  }
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

// ── Config re-export ─────────────────────────────

export { getConfig, saveConfig, escapeHtml };
