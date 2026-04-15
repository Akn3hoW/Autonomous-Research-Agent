import { getConcepts, getConceptStatistics, createConceptExtractionJob } from '../api.js';
import { h, clear, loading, badge, pagination, toast, emptyState, debounce } from '../components.js';

let currentParams = {
  pageNumber: 1,
  pageSize: 50,
  conceptType: '',
  search: ''
};
let statsData = null;

export async function render(container, { navigate }) {
  currentParams = { ...currentParams, pageNumber: 1 };
  clear(container);

  container.appendChild(h('div', { className: 'page-header' },
    h('h1', { className: 'page-title' }, 'Concepts'),
    h('p', { className: 'page-subtitle' }, 'Extracted research methods, datasets, metrics, and models')
  ));

  const toolbar = h('div', { className: 'filter-bar' });

  const searchInput = h('input', {
    className: 'input input-sm search-input',
    type: 'search',
    placeholder: 'Search concepts...',
    value: currentParams.search
  });
  searchInput.addEventListener('input', debounce((e) => {
    currentParams.search = e.target.value;
    currentParams.pageNumber = 1;
    loadTable();
  }, 300));
  toolbar.appendChild(searchInput);

  const typeSelect = h('select', { className: 'input input-sm select' });
  typeSelect.innerHTML = `
    <option value="">All Types</option>
    <option value="Method">Method</option>
    <option value="Dataset">Dataset</option>
    <option value="Metric">Metric</option>
    <option value="Model">Model</option>
  `;
  typeSelect.value = currentParams.conceptType;
  typeSelect.addEventListener('change', (e) => {
    currentParams.conceptType = e.target.value;
    currentParams.pageNumber = 1;
    loadTable();
  });
  toolbar.appendChild(typeSelect);

  toolbar.appendChild(h('button', {
    className: 'btn btn-primary',
    onClick: async () => {
      try {
        await createConceptExtractionJob();
        toast('Concept extraction job started', 'success');
      } catch (err) {
        toast(err.message, 'error');
      }
    }
  }, 'Extract Concepts'));

  container.appendChild(toolbar);

  const statsSection = h('div', { id: 'concept-stats', className: 'stats-grid' });
  container.appendChild(statsSection);

  const tableContainer = h('div', { id: 'concepts-table-container' });
  container.appendChild(tableContainer);

  try {
    statsData = await getConceptStatistics();
    renderStats();
  } catch (err) {
    console.warn('Could not load concept statistics:', err.message);
  }

  loadTable();

  async function loadTable() {
    const tc = document.getElementById('concepts-table-container');
    if (!tc) return;
    clear(tc);
    tc.appendChild(loading());

    try {
      const params = { ...currentParams };
      if (!params.conceptType) delete params.conceptType;
      if (!params.search) delete params.search;

      const data = await getConcepts(params);
      clear(tc);

      if (!data.items || data.items.length === 0) {
        tc.appendChild(emptyState('No concepts found', 'Run concept extraction to identify research entities'));
        return;
      }

      const table = h('table', { className: 'table' });
      const thead = h('thead',
        h('tr',
          h('th', {}, 'Name'),
          h('th', {}, 'Type'),
          h('th', {}, 'Paper'),
          h('th', { className: 'cell-num' }, 'Confidence')
        )
      );
      table.appendChild(thead);

      const tbody = h('tbody');
      for (const concept of data.items) {
        const row = h('tr', { className: 'clickable' },
          h('td', {},
            h('span', { className: 'concept-name' }, concept.name)
          ),
          h('td', {}, badge(concept.conceptType)),
          h('td', {},
            h('a', {
              href: 'javascript:void(0)',
              onClick: () => navigate(`/papers/${concept.paperId}`)
            }, concept.paperId.slice(0, 8) + '...')
          ),
          h('td', { className: 'cell-num' }, concept.confidence.toFixed(2))
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
      tc.appendChild(emptyState('Error loading concepts', err.message));
      toast(err.message, 'error');
    }
  }

  function renderStats() {
    const statsEl = document.getElementById('concept-stats');
    if (!statsEl || !statsData) return;
    clear(statsEl);

    if (statsData.byType && statsData.byType.length > 0) {
      for (const typeCount of statsData.byType) {
        const card = h('div', { className: 'stat-card' },
          h('div', { className: 'stat-value' }, String(typeCount.count)),
          h('div', { className: 'stat-label' }, typeCount.conceptType),
          h('div', { className: 'stat-sublabel' }, `${typeCount.paperCount} papers`)
        );
        statsEl.appendChild(card);
      }
    }

    const totalCard = h('div', { className: 'stat-card accent' },
      h('div', { className: 'stat-value' }, String(statsData.totalConcepts)),
      h('div', { className: 'stat-label' }, 'Total Concepts')
    );
    statsEl.appendChild(totalCard);
  }
}
