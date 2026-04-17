import { getSavedSearches, createSavedSearch, updateSavedSearch, deleteSavedSearch, runSavedSearch } from '../api.js';
import {
  h, clear, loading, badge, pagination, timeAgo, formatDateTime,
  toast, emptyState
} from '../components.js';

let currentParams = {
  pageNumber: 1,
  pageSize: 25,
  isActive: '',
};

let pollingTimer = null;

export async function render(container, { navigate, params }) {
  currentParams = { ...currentParams, pageNumber: 1 };
  clear(container);

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Watchlist'),
      h('p', { className: 'page-subtitle' }, 'Manage saved searches and get notified about new papers'),
    )
  );

  const actions = h('div', { className: 'page-actions mb-6' },
    h('button', {
      className: 'btn btn-primary',
      onClick: () => showCreateModal(),
    }, '+ NEW SAVED SEARCH')
  );
  container.appendChild(actions);

  const filterBar = h('div', { className: 'filter-bar' });
  const statusSelect = h('select', { className: 'input input-sm select' });
  statusSelect.innerHTML = `
    <option value="">All</option>
    <option value="true">Active</option>
    <option value="false">Inactive</option>
  `;
  statusSelect.value = currentParams.isActive;
  statusSelect.addEventListener('change', (e) => {
    currentParams.isActive = e.target.value;
    currentParams.pageNumber = 1;
    loadTable();
  });
  filterBar.appendChild(h('label', { className: 'field-label' }, 'Status:'));
  filterBar.appendChild(statusSelect);

  const refreshBtn = h('button', {
    className: 'btn btn-secondary btn-sm',
    onClick: loadTable,
  }, 'REFRESH');
  filterBar.appendChild(refreshBtn);
  container.appendChild(filterBar);

  const tableContainer = h('div', { id: 'watchlist-table-container' });
  container.appendChild(tableContainer);

  loadTable();
  startPolling();

  async function loadTable() {
    const tc = document.getElementById('watchlist-table-container');
    if (!tc) { stopPolling(); return; }
    clear(tc);
    tc.appendChild(loading());

    try {
      const params = { ...currentParams };
      if (params.isActive === '') delete params.isActive;
      else params.isActive = params.isActive === 'true';

      const data = await getSavedSearches(params);
      clear(tc);

      if (data.items.length === 0) {
        tc.appendChild(emptyState('No saved searches', 'Create a saved search to get notified about new papers'));
        return;
      }

      const table = h('table', { className: 'table' });
      table.appendChild(
        h('thead', {},
          h('tr', {},
            h('th', {}, 'Query'),
            h('th', {}, 'Field'),
            h('th', {}, 'Schedule'),
            h('th', {}, 'Last Run'),
            h('th', {}, 'Results'),
            h('th', {}, 'Status'),
            h('th', {}, ''),
          )
        )
      );

      const tbody = h('tbody');
      for (const item of data.items) {
        const row = h('tr', {},
          h('td', {},
            h('div', { className: 'cell-title' }, item.query),
            item.field ? h('div', { className: 'cell-meta' }, `Field: ${item.field}`) : null,
          ),
          h('td', {}, item.field || '\u2014'),
          h('td', {}, formatSchedule(item.schedule)),
          h('td', { className: 'text-secondary' }, item.lastRunAt ? timeAgo(item.lastRunAt) : 'Never'),
          h('td', {}, item.resultCount != null ? String(item.resultCount) : '\u2014'),
          h('td', {}, badge(item.isActive ? 'Active' : 'Inactive')),
          h('td', {},
            h('div', { className: 'flex gap-2' },
              h('button', {
                className: 'btn btn-secondary btn-sm',
                title: 'Run now',
                onClick: async (e) => {
                  e.stopPropagation();
                  try {
                    await runSavedSearch(item.id);
                    toast('Search started', 'success');
                    loadTable();
                  } catch (err) {
                    toast(err.message, 'error');
                  }
                },
              }, '\u25B6'),
              h('button', {
                className: 'btn btn-secondary btn-sm',
                title: 'Edit',
                onClick: (e) => {
                  e.stopPropagation();
                  showEditModal(item);
                },
              }, '\u270E'),
              h('button', {
                className: 'btn btn-ghost btn-sm',
                title: 'Delete',
                onClick: async (e) => {
                  e.stopPropagation();
                  if (confirm('Delete this saved search?')) {
                    try {
                      await deleteSavedSearch(item.id);
                      toast('Saved search deleted', 'success');
                      loadTable();
                    } catch (err) {
                      toast(err.message, 'error');
                    }
                  }
                },
              }, '\u2715'),
            )
          ),
        );
        tbody.appendChild(row);
      }

      table.appendChild(tbody);
      tc.appendChild(h('div', { className: 'table-wrap' }, table));

      tc.appendChild(pagination(data, (page) => {
        currentParams.pageNumber = page;
        loadTable();
      }));

    } catch (err) {
      clear(tc);
      if (err.name === 'AbortError') return;
      tc.appendChild(emptyState('Error loading saved searches', err.message));
      toast(err.message, 'error');
    }
  }

  function showCreateModal() {
    showModal(null);
  }

  function showEditModal(item) {
    showModal(item);
  }

  function showModal(item) {
    const isEdit = item != null;
    const modal = h('div', { className: 'modal', id: 'watchlist-modal' });
    modal.hidden = false;

    const scheduleOptions = ['Manual', 'Hourly', 'Daily', 'Weekly'];
    const currentSchedule = item?.schedule || 'Manual';

    modal.innerHTML = `
      <div class="modal-backdrop"></div>
      <div class="modal-panel">
        <div class="modal-header">
          <h2 class="modal-title">${isEdit ? 'EDIT' : 'NEW'} SAVED SEARCH</h2>
          <button class="modal-close" type="button" aria-label="Close">&times;</button>
        </div>
        <div class="modal-body">
          <label class="field-label">QUERY *</label>
          <input id="ws-query" type="text" class="input" placeholder="e.g., machine learning transformers" value="${item?.query || ''}" required>
          <label class="field-label">FIELD (optional)</label>
          <input id="ws-field" type="text" class="input" placeholder="e.g., computer science, biology" value="${item?.field || ''}">
          <label class="field-label">SCHEDULE</label>
          <select id="ws-schedule" class="input select">
            ${scheduleOptions.map(opt => `<option value="${opt}" ${opt === currentSchedule ? 'selected' : ''}>${opt}</option>`).join('')}
          </select>
          ${isEdit ? `
          <label class="field-label">STATUS</label>
          <select id="ws-active" class="input select">
            <option value="true" ${item.isActive ? 'selected' : ''}>Active</option>
            <option value="false" ${!item.isActive ? 'selected' : ''}>Inactive</option>
          </select>
          ` : ''}
        </div>
        <div class="modal-footer">
          <button id="ws-save" class="btn btn-primary" type="button">${isEdit ? 'SAVE' : 'CREATE'}</button>
          <button class="btn btn-ghost modal-close" type="button">CANCEL</button>
        </div>
      </div>
    `;

    document.body.appendChild(modal);

    const close = () => {
      modal.remove();
    };

    modal.querySelector('.modal-backdrop').addEventListener('click', close);
    modal.querySelectorAll('.modal-close').forEach(btn => btn.addEventListener('click', close));

    modal.querySelector('#ws-save').addEventListener('click', async () => {
      const query = modal.querySelector('#ws-query').value.trim();
      const field = modal.querySelector('#ws-field').value.trim();
      const schedule = modal.querySelector('#ws-schedule').value;

      if (!query) {
        toast('Query is required', 'error');
        return;
      }

      try {
        if (isEdit) {
          const active = modal.querySelector('#ws-active').value === 'true';
          await updateSavedSearch(item.id, { query, field: field || null, schedule, isActive: active });
          toast('Saved search updated', 'success');
        } else {
          await createSavedSearch({ query, field: field || null, schedule });
          toast('Saved search created', 'success');
        }
        close();
        loadTable();
      } catch (err) {
        toast(err.message, 'error');
      }
    });

    modal.querySelector('#ws-query').focus();
  }

  function startPolling() {
    stopPolling();
    pollingTimer = setInterval(() => {
      if (!document.getElementById('watchlist-table-container')) {
        stopPolling();
        return;
      }
      loadTable();
    }, 30_000);
  }
}

function stopPolling() {
  if (pollingTimer) {
    clearInterval(pollingTimer);
    pollingTimer = null;
  }
}

function formatSchedule(schedule) {
  const map = {
    'Manual': 'Manual',
    'Hourly': 'Hourly',
    'Daily': 'Daily',
    'Weekly': 'Weekly',
  };
  return map[schedule] || schedule;
}
