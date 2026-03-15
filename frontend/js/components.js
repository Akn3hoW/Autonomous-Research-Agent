// ═══════════════════════════════════════════════════
// Reusable UI Components
// ═══════════════════════════════════════════════════

// ── DOM Helpers ──────────────────────────────────

export function h(tag, attrs = {}, ...children) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === undefined || v === false) continue;
    if (k === 'className') el.className = v;
    else if (k === 'dataset') Object.assign(el.dataset, v);
    else if (k.startsWith('on')) el.addEventListener(k.slice(2).toLowerCase(), v);
    else if (k === 'htmlFor') el.htmlFor = v;
    else if (k === 'innerHTML') el.innerHTML = v;
    else if (v === true) el.setAttribute(k, '');
    else el.setAttribute(k, v);
  }
  for (const child of children) {
    if (child == null || child === false) continue;
    if (typeof child === 'string' || typeof child === 'number') {
      el.appendChild(document.createTextNode(String(child)));
    } else if (child instanceof Node) {
      el.appendChild(child);
    }
  }
  return el;
}

export function clear(el) {
  el.innerHTML = '';
  return el;
}

// ── Status Badge ─────────────────────────────────

const STATUS_STYLES = {
  // Job statuses
  Queued: 'badge-gray',
  Running: 'badge-blue',
  Completed: 'badge-green',
  Failed: 'badge-red',
  Cancelled: 'badge-gray',

  // Paper statuses
  Draft: 'badge-gray',
  Imported: 'badge-black',
  Processing: 'badge-blue',
  Ready: 'badge-green',
  Archived: 'badge-gray',

  // Summary statuses
  Pending: 'badge-gray',
  Generated: 'badge-blue',
  Approved: 'badge-green',
  Rejected: 'badge-red',

  // Document statuses
  Downloaded: 'badge-blue',
  Extracted: 'badge-green',
};

export function badge(status) {
  const cls = STATUS_STYLES[status] || 'badge-gray';
  return h('span', { className: `badge ${cls}` }, status);
}

// ── Pagination ───────────────────────────────────

export function pagination({ pageNumber, pageSize, totalCount }, onChange) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const showing = Math.min(pageSize, totalCount - (pageNumber - 1) * pageSize);
  const start = totalCount > 0 ? (pageNumber - 1) * pageSize + 1 : 0;
  const end = start + showing - 1;

  const info = h('div', { className: 'pagination-info' },
    totalCount > 0
      ? `${start}\u2013${end} of ${totalCount}`
      : 'No results'
  );

  const controls = h('div', { className: 'pagination-controls' });

  // Prev
  const prevBtn = h('button', {
    className: 'pagination-btn',
    disabled: pageNumber <= 1 ? '' : undefined,
    onClick: () => onChange(pageNumber - 1),
  }, '\u2190');
  if (pageNumber <= 1) prevBtn.disabled = true;
  controls.appendChild(prevBtn);

  // Page numbers — show max 7
  const pages = getPageNumbers(pageNumber, totalPages, 7);
  for (const p of pages) {
    if (p === '...') {
      controls.appendChild(h('span', { className: 'pagination-btn', style: 'cursor:default;' }, '\u2026'));
    } else {
      const btn = h('button', {
        className: `pagination-btn ${p === pageNumber ? 'active' : ''}`,
        onClick: () => onChange(p),
      }, String(p));
      controls.appendChild(btn);
    }
  }

  // Next
  const nextBtn = h('button', {
    className: 'pagination-btn',
    onClick: () => onChange(pageNumber + 1),
  });
  nextBtn.textContent = '\u2192';
  if (pageNumber >= totalPages) nextBtn.disabled = true;
  controls.appendChild(nextBtn);

  return h('div', { className: 'pagination' }, info, controls);
}

function getPageNumbers(current, total, max) {
  if (total <= max) return Array.from({ length: total }, (_, i) => i + 1);
  const pages = [];
  const half = Math.floor((max - 3) / 2);
  let start = Math.max(2, current - half);
  let end = Math.min(total - 1, current + half);

  if (current <= half + 2) end = max - 2;
  if (current >= total - half - 1) start = total - max + 3;

  pages.push(1);
  if (start > 2) pages.push('...');
  for (let i = start; i <= end; i++) pages.push(i);
  if (end < total - 1) pages.push('...');
  pages.push(total);
  return pages;
}

// ── Loading Spinner ──────────────────────────────

export function loading(text = 'Loading') {
  return h('div', { className: 'loading' },
    h('div', { className: 'spinner' }),
    h('span', { className: 'loading-text' }, text)
  );
}

// ── Empty State ──────────────────────────────────

export function emptyState(title, text) {
  return h('div', { className: 'empty-state' },
    h('div', { className: 'empty-state-title' }, title),
    text ? h('div', { className: 'empty-state-text' }, text) : null
  );
}

// ── Toast Notifications ─────────────────────────

export function toast(message, type = 'info') {
  const container = document.getElementById('toasts');
  const cls = type === 'error' ? 'toast-error' : type === 'success' ? 'toast-success' : type === 'warning' ? 'toast-warning' : '';
  const el = h('div', { className: `toast ${cls}` }, message);
  container.appendChild(el);
  setTimeout(() => {
    el.style.opacity = '0';
    el.style.transform = 'translateY(8px)';
    el.style.transition = 'all 200ms ease';
    setTimeout(() => el.remove(), 200);
  }, 4000);
}

// ── Time Formatting ──────────────────────────────

export function timeAgo(dateStr) {
  const date = new Date(dateStr);
  const now = Date.now();
  const diff = now - date.getTime();
  const seconds = Math.floor(diff / 1000);
  const minutes = Math.floor(seconds / 60);
  const hours = Math.floor(minutes / 60);
  const days = Math.floor(hours / 24);

  if (seconds < 60) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  if (hours < 24) return `${hours}h ago`;
  if (days < 30) return `${days}d ago`;
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

export function formatDate(dateStr) {
  if (!dateStr) return '\u2014';
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

export function formatDateTime(dateStr) {
  if (!dateStr) return '\u2014';
  return new Date(dateStr).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// ── Debounce ─────────────────────────────────────

export function debounce(fn, ms) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
}

// ── Truncate ─────────────────────────────────────

export function truncate(str, maxLen = 80) {
  if (!str) return '';
  return str.length > maxLen ? str.slice(0, maxLen) + '\u2026' : str;
}

// ── Authors formatting ───────────────────────────

export function formatAuthors(authors, max = 3) {
  if (!authors || authors.length === 0) return '\u2014';
  if (authors.length <= max) return authors.join(', ');
  return `${authors.slice(0, max).join(', ')} +${authors.length - max}`;
}

// ── JSON pretty-print ────────────────────────────

export function jsonBlock(data) {
  const str = typeof data === 'string' ? data : JSON.stringify(data, null, 2);
  return h('pre', { className: 'json-block' }, str || '\u2014');
}
