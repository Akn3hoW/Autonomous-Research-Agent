import { getReadingList, updateReadingSession, deleteReadingSession, getPapers } from '../api.js';
import { h, clear, loading, badge, pagination, timeAgo, formatAuthors, debounce, toast, emptyState } from '../components.js';

let currentParams = {
  pageNumber: 1,
  pageSize: 25,
  status: '',
};

export async function render(container, { navigate }) {
  currentParams = { ...currentParams, pageNumber: 1 };
  clear(container);

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Reading List'),
      h('p', { className: 'page-subtitle' }, 'Track your paper reading progress'),
    )
  );

  const filterBar = h('div', { className: 'filter-bar' });

  const statusSelect = h('select', { className: 'input input-sm select' });
  statusSelect.innerHTML = `
    <option value="">All Statuses</option>
    <option value="ToRead">To Read</option>
    <option value="Reading">Reading</option>
    <option value="Read">Read</option>
  `;
  statusSelect.value = currentParams.status;
  statusSelect.addEventListener('change', (e) => {
    currentParams.status = e.target.value;
    currentParams.pageNumber = 1;
    loadTable();
  });
  filterBar.appendChild(statusSelect);

  container.appendChild(filterBar);

  const tableContainer = h('div', { id: 'reading-list-table-container' });
  container.appendChild(tableContainer);

  loadTable();

  async function loadTable() {
    const tc = document.getElementById('reading-list-table-container');
    if (!tc) return;
    clear(tc);
    tc.appendChild(loading());

    try {
      const data = await getReadingList(currentParams);
      clear(tc);

      if (data.items.length === 0) {
        tc.appendChild(emptyState('No reading sessions', 'Start by adding papers to your reading list from the papers page'));
        return;
      }

      const table = h('table', { className: 'table' });

      const thead = h('thead');
      const headerRow = h('tr');
      const cols = [
        { key: 'paperTitle', label: 'Paper' },
        { key: 'status', label: 'Status' },
        { key: 'updatedAt', label: 'Last Updated' },
        { key: 'actions', label: '', sortable: false },
      ];

      for (const col of cols) {
        headerRow.appendChild(h('th', {}, col.label));
      }
      thead.appendChild(headerRow);
      table.appendChild(thead);

      const tbody = h('tbody');
      for (const item of data.items) {
        const row = h('tr', { className: 'clickable' },
          h('td', {},
            h('div', { className: 'cell-title truncate', style: 'max-width:360px' }, item.paperTitle),
            h('div', { className: 'cell-meta' }, formatAuthors(item.paperAuthors, 2)),
          ),
          h('td', {}, readingBadge(item.status)),
          h('td', { className: 'text-secondary' }, timeAgo(item.updatedAt)),
          h('td', {},
            h('div', { className: 'flex gap-2' },
              h('button', {
                className: 'btn btn-secondary btn-sm',
                onClick: (e) => {
                  e.stopPropagation();
                  showUpdateModal(item);
                }
              }, 'EDIT'),
              h('button', {
                className: 'btn btn-danger btn-sm',
                onClick: (e) => {
                  e.stopPropagation();
                  handleDelete(item.id);
                }
              }, 'REMOVE'),
            ),
          ),
        );
        row.addEventListener('click', () => navigate(`/papers/${item.paperId}`));
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
      tc.appendChild(emptyState('Error loading reading list', err.message));
      toast(err.message, 'error');
    }
  }

  function readingBadge(status) {
    const cls = status === 'Read' ? 'badge-green' : status === 'Reading' ? 'badge-blue' : 'badge-gray';
    return h('span', { className: `badge ${cls}` }, status);
  }

  function showUpdateModal(item) {
    const modal = h('div', { className: 'modal-backdrop' },
      h('div', { className: 'modal-content' },
        h('h3', { className: 'modal-title' }, 'Update Reading Status'),
        h('div', { className: 'field-group' },
          h('label', { className: 'field-label' }, 'Status'),
          h('select', { className: 'input', id: 'update-status-select' },
            h('option', { value: 'ToRead' }, 'To Read'),
            h('option', { value: 'Reading' }, 'Reading'),
            h('option', { value: 'Read' }, 'Read'),
          )
        ),
        h('div', { className: 'field-group' },
          h('label', { className: 'field-label' }, 'Notes'),
          h('textarea', { className: 'input', id: 'update-notes-input', rows: 4 }, item.notes || ''),
        ),
        h('div', { className: 'modal-actions' },
          h('button', { className: 'btn btn-secondary', onClick: () => modal.remove() }, 'CANCEL'),
          h('button', {
            className: 'btn btn-primary',
            onClick: async () => {
              const status = document.getElementById('update-status-select').value;
              const notes = document.getElementById('update-notes-input').value;
              try {
                await updateReadingSession(item.id, { status, notes });
                toast('Reading session updated', 'success');
                modal.remove();
                loadTable();
              } catch (err) {
                toast(err.message, 'error');
              }
            }
          }, 'SAVE'),
        ),
      )
    );
    document.body.appendChild(modal);
    modal.querySelector('#update-status-select').value = item.status;
  }

  async function handleDelete(id) {
    if (!confirm('Are you sure you want to remove this paper from your reading list?')) return;
    try {
      await deleteReadingSession(id);
      toast('Reading session removed', 'success');
      loadTable();
    } catch (err) {
      toast(err.message, 'error');
    }
  }
}
