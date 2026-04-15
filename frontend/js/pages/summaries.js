import {
  getPapers, getPaperSummaries, getSummary,
  approveSummary, rejectSummary
} from '../api.js';
import {
  h, clear, loading, badge, formatAuthors, formatDate, formatDateTime,
  toast, emptyState
} from '../components.js';

let currentParams = {
  paperId: '',
  status: '',
};

export async function render(container, { navigate, params }) {
  currentParams = { ...currentParams, pageNumber: 1 };
  clear(container);

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Summaries'),
      h('p', { className: 'page-subtitle' }, 'Review and manage paper summaries'),
    )
  );

  const filterBar = h('div', { className: 'filter-bar' });

  const paperSelect = h('select', { className: 'input input-sm select', style: 'min-width:280px' });
  paperSelect.innerHTML = '<option value="">All Papers</option>';
  paperSelect.value = currentParams.paperId;
  paperSelect.addEventListener('change', (e) => {
    currentParams.paperId = e.target.value;
    loadSummaries();
  });
  filterBar.appendChild(paperSelect);

  const statusSelect = h('select', { className: 'input input-sm select' });
  statusSelect.innerHTML = `
    <option value="">All Statuses</option>
    <option value="Pending">Pending</option>
    <option value="Generated">Generated</option>
    <option value="Approved">Approved</option>
    <option value="Rejected">Rejected</option>
  `;
  statusSelect.value = currentParams.status;
  statusSelect.addEventListener('change', (e) => {
    currentParams.status = e.target.value;
    loadSummaries();
  });
  filterBar.appendChild(statusSelect);

  container.appendChild(filterBar);

  const content = h('div', { id: 'summaries-content' });
  container.appendChild(content);

  loadPapers();
  loadSummaries();

  async function loadPapers() {
    try {
      const data = await getPapers({ pageSize: 100 });
      paperSelect.innerHTML = '<option value="">All Papers</option>';
      for (const paper of data.items) {
        const opt = h('option', { value: paper.id }, paper.title.slice(0, 60) + (paper.title.length > 60 ? '\u2026' : ''));
        paperSelect.appendChild(opt);
      }
      if (currentParams.paperId) {
        paperSelect.value = currentParams.paperId;
      }
    } catch (err) {
      if (err.name !== 'AbortError') {
        toast('Failed to load papers', 'error');
      }
    }
  }

  async function loadSummaries() {
    const el = document.getElementById('summaries-content');
    if (!el) return;
    clear(el);
    el.appendChild(loading('Loading summaries'));

    try {
      let summaries = [];
      let papersMap = {};

      if (currentParams.paperId) {
        const paper = await getPapers({ id: currentParams.paperId, pageSize: 1 }).catch(() => null);
        if (paper && paper.items.length > 0) {
          papersMap[paper.items[0].id] = paper.items[0];
        }
        summaries = await getPaperSummaries(currentParams.paperId).catch(() => []);
      } else {
        const data = await getPapers({ pageSize: 100 });
        const BATCH_SIZE = 5;
        const results = [];
        for (let i = 0; i < data.items.length; i += BATCH_SIZE) {
          const batch = data.items.slice(i, i + BATCH_SIZE);
          const batchPromises = batch.map(async (paper) => {
            papersMap[paper.id] = paper;
            try {
              return await getPaperSummaries(paper.id);
            } catch {
              return [];
            }
          });
          const batchResults = await Promise.all(batchPromises);
          results.push(...batchResults);
        }
        summaries = results.flat();
      }

      clear(el);

      if (currentParams.status) {
        summaries = summaries.filter(s => s.status === currentParams.status);
      }

      if (summaries.length === 0) {
        el.appendChild(emptyState('No summaries found', currentParams.paperId ? 'This paper has no summaries yet' : 'No summaries match your filters'));
        return;
      }

      summaries.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));

      for (const summary of summaries) {
        const paper = papersMap[summary.paperId];
        el.appendChild(renderSummaryCard(summary, paper, navigate));
      }

    } catch (err) {
      clear(el);
      if (err.name === 'AbortError') return;
      el.appendChild(emptyState('Error loading summaries', err.message));
      toast(err.message, 'error');
    }
  }
}

function renderSummaryCard(summary, paper, navigate) {
  const card = h('div', { className: 'summary-card' });

  const header = h('div', { className: 'summary-card-header' },
    h('div', { className: 'flex items-center gap-3' },
      badge(summary.status),
      h('span', { className: 'summary-card-meta' }, `Model: ${summary.modelName || '\u2014'}`),
      summary.promptVersion ? h('span', { className: 'summary-card-meta' }, `v${summary.promptVersion}`) : null,
    ),
    h('span', { className: 'summary-card-meta' }, formatDateTime(summary.createdAt)),
  );
  card.appendChild(header);

  if (paper) {
    card.appendChild(
      h('div', { className: 'summary-card-meta mb-4', style: 'cursor:pointer; color:var(--c-link)' },
        h('a', {
          href: `#/papers/${paper.id}`,
          onClick: (e) => { e.preventDefault(); navigate(`/papers/${paper.id}`); }
        }, paper.title)
      )
    );
  }

  if (summary.summary) {
    const content = typeof summary.summary === 'string' ? summary.summary : JSON.stringify(summary.summary, null, 2);
    const preview = content.slice(0, 300) + (content.length > 300 ? '\u2026' : '');
    card.appendChild(h('div', { className: 'summary-card-body' },
      h('pre', { className: 'json-block', style: 'max-height:150px' }, preview),
    ));
  }

  if (summary.reviewedBy) {
    card.appendChild(h('div', { className: 'summary-card-meta mt-4' },
      `Reviewed by ${summary.reviewedBy} on ${formatDateTime(summary.reviewedAt)}`,
      summary.reviewNotes ? ` \u2014 ${summary.reviewNotes}` : '',
    ));
  }

  const actions = h('div', { className: 'summary-card-actions' });

  actions.appendChild(
    h('button', {
      className: 'btn btn-secondary btn-sm',
      onClick: () => navigate(`/summaries/${summary.id}`),
    }, 'VIEW DETAIL')
  );

  if (summary.status === 'Generated' || summary.status === 'Pending') {
    actions.appendChild(
      h('button', {
        className: 'btn btn-primary btn-sm',
        onClick: async () => {
          try {
            await approveSummary(summary.id, {});
            toast('Summary approved', 'success');
            window.location.hash = '#/summaries';
          } catch (err) {
            toast(err.message, 'error');
          }
        },
      }, 'APPROVE')
    );
    actions.appendChild(
      h('button', {
        className: 'btn btn-danger btn-sm',
        onClick: async () => {
          try {
            await rejectSummary(summary.id, {});
            toast('Summary rejected', 'success');
            window.location.hash = '#/summaries';
          } catch (err) {
            toast(err.message, 'error');
          }
        },
      }, 'REJECT')
    );
  }

  card.appendChild(actions);
  return card;
}
