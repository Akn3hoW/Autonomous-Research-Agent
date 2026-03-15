// ═══════════════════════════════════════════════════
// ARA API Client — with caching and error handling
// ═══════════════════════════════════════════════════

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
    if (key.includes(prefix)) CACHE.delete(key);
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
  const res = await fetch(`${baseUrl}/health`, { signal });
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

// ── Config re-export ─────────────────────────────

export { getConfig, saveConfig };
