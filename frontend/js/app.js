// ═══════════════════════════════════════════════════
// ARA — Main Application Entry
// Hash-based SPA router + initialization
// ═══════════════════════════════════════════════════

import { checkHealth, getConfig, saveConfig } from './api.js';
import { clear, toast } from './components.js';

// ── Route definitions ────────────────────────────

const routes = [
  { path: '/', module: () => import('./pages/dashboard.js'), title: 'Dashboard' },
  { path: '/papers', module: () => import('./pages/papers.js'), title: 'Papers' },
  { path: '/papers/:id', module: () => import('./pages/paper-detail.js'), title: 'Paper Detail' },
  { path: '/jobs', module: () => import('./pages/jobs.js'), title: 'Jobs' },
  { path: '/jobs/:id', module: () => import('./pages/jobs.js'), title: 'Job Detail' },
  { path: '/search', module: () => import('./pages/search.js'), title: 'Search' },
  { path: '/import', module: () => import('./pages/import.js'), title: 'Import' },
];

// ── Router ───────────────────────────────────────

let currentAbortController = null;

function matchRoute(hash) {
  const path = hash.replace(/^#/, '') || '/';

  for (const route of routes) {
    const pattern = route.path.replace(/:(\w+)/g, '([^/]+)');
    const regex = new RegExp(`^${pattern}$`);
    const match = path.match(regex);

    if (match) {
      const paramNames = [...route.path.matchAll(/:(\w+)/g)].map(m => m[1]);
      const params = {};
      paramNames.forEach((name, i) => { params[name] = match[i + 1]; });
      return { route, params };
    }
  }
  return null;
}

function navigate(path) {
  window.location.hash = path;
}

async function handleRoute() {
  // Abort previous page's async work
  if (currentAbortController) currentAbortController.abort();
  currentAbortController = new AbortController();

  const hash = window.location.hash || '#/';
  const matched = matchRoute(hash);

  if (!matched) {
    navigate('/');
    return;
  }

  const { route, params } = matched;
  const content = document.getElementById('content');

  // Update active nav link
  document.querySelectorAll('.nav-link[data-route]').forEach(link => {
    const routeName = link.dataset.route;
    const currentPath = hash.replace(/^#/, '') || '/';
    const isActive =
      (routeName === 'dashboard' && currentPath === '/') ||
      (routeName !== 'dashboard' && currentPath.startsWith(`/${routeName}`));
    link.classList.toggle('active', isActive);
  });

  // Update breadcrumb
  const breadcrumb = document.getElementById('breadcrumb');
  if (params.id) {
    const basePath = route.path.split('/:')[0];
    const baseName = basePath.replace('/', '').toUpperCase() || 'DASHBOARD';
    breadcrumb.innerHTML = `<a href="#${basePath}" style="color:var(--c-text-secondary)">${baseName}</a> <span>\u2002/\u2002</span> ${params.id.slice(0, 8)}\u2026`;
  } else {
    breadcrumb.textContent = route.title.toUpperCase();
  }

  // Update page title
  document.title = `${route.title} \u2014 ARA`;

  // Close mobile sidebar
  document.getElementById('sidebar').classList.remove('open');

  // Load and render page module
  try {
    const mod = await route.module();
    await mod.render(content, {
      navigate,
      params,
      signal: currentAbortController.signal,
    });
  } catch (err) {
    if (err.name === 'AbortError') return;
    console.error('Route error:', err);
    clear(content);
    content.innerHTML = `
      <div class="empty-state">
        <div class="empty-state-title">Something went wrong</div>
        <div class="empty-state-text">${err.message}</div>
      </div>
    `;
  }
}

// ── Health Check ─────────────────────────────────

async function updateConnectionStatus() {
  const dot = document.querySelector('.status-dot');
  const text = document.querySelector('.status-text');

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 5000);
    const healthy = await checkHealth(controller.signal);
    clearTimeout(timeout);

    if (healthy) {
      dot.className = 'status-dot connected';
      text.textContent = 'CONNECTED';
    } else {
      dot.className = 'status-dot error';
      text.textContent = 'ERROR';
    }
  } catch {
    dot.className = 'status-dot error';
    text.textContent = 'OFFLINE';
  }
}

// ── Settings Modal ───────────────────────────────

function initSettings() {
  const modal = document.getElementById('settings-modal');
  const urlInput = document.getElementById('api-url-input');
  const tokenInput = document.getElementById('api-token-input');
  const saveBtn = document.getElementById('settings-save');
  const settingsBtn = document.getElementById('settings-btn');

  function open() {
    const config = getConfig();
    urlInput.value = config.baseUrl;
    tokenInput.value = config.token;
    modal.hidden = false;
    requestAnimationFrame(() => urlInput.focus());
  }

  function close() {
    modal.hidden = true;
  }

  settingsBtn.addEventListener('click', open);

  modal.querySelectorAll('.modal-close').forEach(btn => {
    btn.addEventListener('click', close);
  });

  modal.querySelector('.modal-backdrop').addEventListener('click', close);

  saveBtn.addEventListener('click', () => {
    saveConfig(urlInput.value.replace(/\/+$/, ''), tokenInput.value);
    close();
    toast('Settings saved', 'success');
    updateConnectionStatus();
    handleRoute(); // Reload current page
  });

  // Escape key
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && !modal.hidden) close();
  });
}

// ── Mobile Menu ──────────────────────────────────

function initMobileMenu() {
  const toggle = document.getElementById('menu-toggle');
  const sidebar = document.getElementById('sidebar');

  toggle.addEventListener('click', (e) => {
    e.stopPropagation();
    sidebar.classList.toggle('open');
  });

  // Close on click outside
  document.getElementById('main').addEventListener('click', () => {
    sidebar.classList.remove('open');
  });
}

// ── Initialize ───────────────────────────────────

function init() {
  initSettings();
  initMobileMenu();

  // Listen for route changes
  window.addEventListener('hashchange', handleRoute);

  // Initial route
  handleRoute();

  // Health check
  updateConnectionStatus();
  setInterval(updateConnectionStatus, 30_000);
}

// Boot
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init);
} else {
  init();
}
