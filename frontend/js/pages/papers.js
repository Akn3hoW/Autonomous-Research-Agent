import { getPapers } from '../api.js';
import { h, clear, loading, badge, pagination, timeAgo, formatAuthors, debounce, toast, emptyState } from '../components.js';

let currentParams = {
  pageNumber: 1,
  pageSize: 25,
  query: '',
  status: '',
  source: '',
  sortBy: 'createdAt',
  sortDirection: 'desc',
};

export async function render(container, { navigate }) {
  currentParams = { ...currentParams, pageNumber: 1 };
  clear(container);

  // Header
  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Papers'),
      h('p', { className: 'page-subtitle' }, 'Manage your research paper library'),
    )
  );

  // Filter bar
  const filterBar = h('div', { className: 'filter-bar' });

  const searchInput = h('input', {
    className: 'input input-sm search-input',
    type: 'search',
    placeholder: 'Search papers\u2026',
    value: currentParams.query,
  });
  searchInput.addEventListener('input', debounce((e) => {
    currentParams.query = e.target.value;
    currentParams.pageNumber = 1;
    loadTable();
  }, 300));
  filterBar.appendChild(searchInput);

  const statusSelect = h('select', { className: 'input input-sm select' });
  statusSelect.innerHTML = `
    <option value="">All Statuses</option>
    <option value="Draft">Draft</option>
    <option value="Imported">Imported</option>
    <option value="Processing">Processing</option>
    <option value="Ready">Ready</option>
    <option value="Archived">Archived</option>
  `;
  statusSelect.value = currentParams.status;
  statusSelect.addEventListener('change', (e) => {
    currentParams.status = e.target.value;
    currentParams.pageNumber = 1;
    loadTable();
  });
  filterBar.appendChild(statusSelect);

  const sourceSelect = h('select', { className: 'input input-sm select' });
  sourceSelect.innerHTML = `
    <option value="">All Sources</option>
    <option value="Manual">Manual</option>
    <option value="SemanticScholar">Semantic Scholar</option>
    <option value="BatchImport">Batch Import</option>
  `;
  sourceSelect.value = currentParams.source;
  sourceSelect.addEventListener('change', (e) => {
    currentParams.source = e.target.value;
    currentParams.pageNumber = 1;
    loadTable();
  });
  filterBar.appendChild(sourceSelect);

  container.appendChild(filterBar);

  // Table container
  const tableContainer = h('div', { id: 'papers-table-container' });
  container.appendChild(tableContainer);

  loadTable();

  async function loadTable() {
    const tc = document.getElementById('papers-table-container');
    if (!tc) return;
    clear(tc);
    tc.appendChild(loading());

    try {
      const data = await getPapers(currentParams);
      clear(tc);

      if (data.items.length === 0) {
        tc.appendChild(emptyState('No papers found', 'Try adjusting your filters or import some papers'));
        return;
      }

      const table = h('table', { className: 'table' });

      // Header
      const cols = [
        { key: 'title', label: 'Title' },
        { key: 'authors', label: 'Authors', sortable: false },
        { key: 'year', label: 'Year' },
        { key: 'venue', label: 'Venue', sortable: false },
        { key: 'status', label: 'Status' },
        { key: 'source', label: 'Source' },
        { key: 'citationCount', label: 'Citations', cls: 'cell-num' },
        { key: 'createdAt', label: 'Added' },
      ];

      const thead = h('thead');
      const headerRow = h('tr');
      for (const col of cols) {
        const sortable = col.sortable !== false;
        const isSorted = currentParams.sortBy === col.key;
        const cls = [
          col.cls || '',
          sortable ? 'sortable' : '',
          isSorted ? (currentParams.sortDirection === 'asc' ? 'sorted-asc' : 'sorted-desc') : '',
        ].filter(Boolean).join(' ');

        const th = h('th', { className: cls }, col.label);
        if (sortable) {
          th.addEventListener('click', () => {
            if (currentParams.sortBy === col.key) {
              currentParams.sortDirection = currentParams.sortDirection === 'asc' ? 'desc' : 'asc';
            } else {
              currentParams.sortBy = col.key;
              currentParams.sortDirection = 'desc';
            }
            loadTable();
          });
        }
        headerRow.appendChild(th);
      }
      thead.appendChild(headerRow);
      table.appendChild(thead);

      // Body
      const tbody = h('tbody');
      for (const paper of data.items) {
        const row = h('tr', {
          className: 'clickable',
          onClick: () => navigate(`/papers/${paper.id}`),
        },
          h('td', {},
            h('div', { className: 'cell-title truncate', style: 'max-width:360px' }, paper.title),
          ),
          h('td', { className: 'text-secondary' }, formatAuthors(paper.authors, 2)),
          h('td', {}, paper.year ? String(paper.year) : '\u2014'),
          h('td', { className: 'truncate text-secondary', style: 'max-width:140px' }, paper.venue || '\u2014'),
          h('td', {}, badge(paper.status)),
          h('td', {}, badge(paper.source)),
          h('td', { className: 'cell-num' }, String(paper.citationCount)),
          h('td', { className: 'text-secondary' }, timeAgo(paper.createdAt)),
        );
        tbody.appendChild(row);
      }
      table.appendChild(tbody);
      tc.appendChild(h('div', { className: 'table-wrap' }, table));

      // Pagination
      tc.appendChild(pagination(data, (page) => {
        currentParams.pageNumber = page;
        loadTable();
      }));

    } catch (err) {
      clear(tc);
      if (err.name === 'AbortError') return;
      tc.appendChild(emptyState('Error loading papers', err.message));
      toast(err.message, 'error');
    }
  }
}
