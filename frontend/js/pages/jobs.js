import { getJobs, getJob, retryJob } from '../api.js';
import {
  h, clear, loading, badge, pagination, timeAgo, formatDateTime,
  jsonBlock, toast, emptyState
} from '../components.js';

let currentParams = {
  pageNumber: 1,
  pageSize: 25,
  type: '',
  status: '',
};

let pollingTimer = null;

export async function render(container, { navigate, params }) {
  // Job detail view
  if (params?.id) {
    return renderJobDetail(container, params.id, navigate);
  }

  currentParams = { ...currentParams, pageNumber: 1 };
  clear(container);

  // Header
  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Jobs'),
      h('p', { className: 'page-subtitle' }, 'Monitor background processing tasks'),
    )
  );

  // Status tabs
  const tabs = h('div', { className: 'tabs' });
  const statuses = ['', 'Queued', 'Running', 'Completed', 'Failed', 'Cancelled'];
  const statusLabels = ['All', 'Queued', 'Running', 'Completed', 'Failed', 'Cancelled'];

  for (let i = 0; i < statuses.length; i++) {
    const tab = h('button', {
      className: `tab ${currentParams.status === statuses[i] ? 'active' : ''}`,
      onClick: () => {
        currentParams.status = statuses[i];
        currentParams.pageNumber = 1;
        // Update active tab
        tabs.querySelectorAll('.tab').forEach((t, j) => {
          t.classList.toggle('active', j === i);
        });
        loadTable();
      },
    }, statusLabels[i]);
    tabs.appendChild(tab);
  }
  container.appendChild(tabs);

  // Type filter
  const filterBar = h('div', { className: 'filter-bar' });
  const typeSelect = h('select', { className: 'input input-sm select' });
  typeSelect.innerHTML = `
    <option value="">All Types</option>
    <option value="ImportPapers">Import Papers</option>
    <option value="SummarizePaper">Summarize Paper</option>
    <option value="GenerateEmbeddings">Generate Embeddings</option>
    <option value="Analysis">Analysis</option>
    <option value="ProcessPaperDocument">Process Document</option>
  `;
  typeSelect.value = currentParams.type;
  typeSelect.addEventListener('change', (e) => {
    currentParams.type = e.target.value;
    currentParams.pageNumber = 1;
    loadTable();
  });
  filterBar.appendChild(typeSelect);

  const refreshBtn = h('button', {
    className: 'btn btn-secondary btn-sm',
    onClick: loadTable,
  }, 'REFRESH');
  filterBar.appendChild(refreshBtn);

  container.appendChild(filterBar);

  // Table container
  const tableContainer = h('div', { id: 'jobs-table-container' });
  container.appendChild(tableContainer);

  loadTable();

  // Auto-refresh when Running jobs exist
  startPolling();

  async function loadTable() {
    const tc = document.getElementById('jobs-table-container');
    if (!tc) { stopPolling(); return; }
    clear(tc);
    tc.appendChild(loading());

    try {
      const data = await getJobs(currentParams);
      clear(tc);

      if (data.items.length === 0) {
        tc.appendChild(emptyState('No jobs found', 'Jobs are created when you import papers or generate summaries'));
        return;
      }

      const table = h('table', { className: 'table' });
      table.appendChild(
        h('thead', {},
          h('tr', {},
            h('th', {}, 'Type'),
            h('th', {}, 'Status'),
            h('th', {}, 'Error'),
            h('th', {}, 'Created'),
            h('th', {}, 'Updated'),
            h('th', {}, ''),
          )
        )
      );

      const tbody = h('tbody');
      for (const job of data.items) {
        const row = h('tr', {
          className: 'clickable',
          onClick: (e) => {
            if (e.target.closest('button')) return;
            navigate(`/jobs/${job.id}`);
          },
        },
          h('td', {},
            h('div', { className: 'cell-title' }, formatJobType(job.type)),
            job.createdBy ? h('div', { className: 'cell-meta' }, `by ${job.createdBy}`) : null,
          ),
          h('td', {}, badge(job.status)),
          h('td', {},
            h('span', {
              className: 'truncate text-secondary',
              style: 'max-width:200px;display:block',
            }, job.errorMessage || '\u2014'),
          ),
          h('td', { className: 'text-secondary' }, timeAgo(job.createdAt)),
          h('td', { className: 'text-secondary' }, timeAgo(job.updatedAt)),
          h('td', {},
            job.status === 'Failed'
              ? h('button', {
                  className: 'btn btn-secondary btn-sm',
                  onClick: async (e) => {
                    e.stopPropagation();
                    try {
                      await retryJob(job.id, {});
                      toast('Job retry queued', 'success');
                      loadTable();
                    } catch (err) {
                      toast(err.message, 'error');
                    }
                  },
                }, 'RETRY')
              : null,
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
      tc.appendChild(emptyState('Error loading jobs', err.message));
      toast(err.message, 'error');
    }
  }

  function startPolling() {
    stopPolling();
    pollingTimer = setInterval(() => {
      if (!document.getElementById('jobs-table-container')) {
        stopPolling();
        return;
      }
      if (currentParams.status === '' || currentParams.status === 'Running' || currentParams.status === 'Queued') {
        loadTable();
      }
    }, 10_000);
  }
}

function stopPolling() {
  if (pollingTimer) {
    clearInterval(pollingTimer);
    pollingTimer = null;
  }
}

async function renderJobDetail(container, jobId, navigate) {
  clear(container);
  container.appendChild(loading('Loading job'));

  try {
    const job = await getJob(jobId);
    clear(container);

    container.appendChild(
      h('a', {
        className: 'detail-back',
        href: '#/jobs',
        onClick: (e) => { e.preventDefault(); navigate('/jobs'); }
      }, '\u2190 JOBS')
    );

    container.appendChild(
      h('div', { className: 'flex items-center gap-4 mb-6' },
        h('h1', { className: 'detail-title', style: 'margin-bottom:0' }, formatJobType(job.type)),
        badge(job.status),
      )
    );

    // Meta
    const meta = h('div', { className: 'detail-meta' });
    const metaItems = [
      ['Job ID', job.id],
      ['Type', job.type],
      ['Status', job.status],
      ['Created', formatDateTime(job.createdAt)],
      ['Updated', formatDateTime(job.updatedAt)],
    ];
    if (job.createdBy) metaItems.push(['Created By', job.createdBy]);
    if (job.targetEntityId) metaItems.push(['Target Entity', job.targetEntityId]);

    for (const [label, value] of metaItems) {
      meta.appendChild(
        h('div', { className: 'meta-item' },
          h('span', { className: 'meta-label' }, label),
          h('span', { className: 'meta-value cell-mono' }, String(value)),
        )
      );
    }
    container.appendChild(meta);

    // Error message
    if (job.errorMessage) {
      container.appendChild(
        h('div', { className: 'section' },
          h('div', { className: 'section-header' },
            h('h2', { className: 'section-title' }, 'Error'),
          ),
          h('pre', { className: 'json-block', style: 'border-left:3px solid var(--c-red)' }, job.errorMessage),
        )
      );
    }

    // Actions
    if (job.status === 'Failed') {
      container.appendChild(
        h('div', { className: 'page-actions mb-8' },
          h('button', {
            className: 'btn btn-primary btn-sm',
            onClick: async () => {
              try {
                await retryJob(job.id, {});
                toast('Job retry queued', 'success');
                navigate(`/jobs/${job.id}`);
              } catch (err) {
                toast(err.message, 'error');
              }
            },
          }, 'RETRY JOB'),
        )
      );
    }

    // Payload
    if (job.payload) {
      container.appendChild(
        h('div', { className: 'section' },
          h('div', { className: 'section-header' },
            h('h2', { className: 'section-title' }, 'Payload'),
          ),
          jsonBlock(job.payload),
        )
      );
    }

    // Result
    if (job.result) {
      container.appendChild(
        h('div', { className: 'section' },
          h('div', { className: 'section-header' },
            h('h2', { className: 'section-title' }, 'Result'),
          ),
          jsonBlock(job.result),
        )
      );
    }

  } catch (err) {
    clear(container);
    if (err.name === 'AbortError') return;
    container.appendChild(emptyState('Job not found', err.message));
    toast(err.message, 'error');
  }
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
