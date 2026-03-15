import { getPapers, getJobs } from '../api.js';
import { h, clear, loading, badge, timeAgo, formatAuthors, toast, emptyState } from '../components.js';

export async function render(container, { navigate }) {
  clear(container);
  container.appendChild(loading('Loading dashboard'));

  try {
    const [papersRes, jobsRes, activeJobsRes, pendingPapers] = await Promise.all([
      getPapers({ pageSize: 8, sortBy: 'createdAt', sortDirection: 'desc' }),
      getJobs({ pageSize: 6, sortBy: 'createdAt', sortDirection: 'desc' }),
      getJobs({ status: 'Running', pageSize: 100 }),
      getPapers({ status: 'Draft', pageSize: 100 }),
    ]);

    clear(container);

    // Stats row
    const stats = h('div', { className: 'stats-grid' },
      statCard(papersRes.totalCount, 'Total Papers', 'accent'),
      statCard(activeJobsRes.totalCount, 'Active Jobs', 'blue'),
      statCard(pendingPapers.totalCount, 'Drafts', 'yellow'),
      statCard(
        jobsRes.items.filter(j => j.status === 'Completed').length,
        'Completed Today',
        'green'
      ),
    );
    container.appendChild(stats);

    // Two-column layout
    const grid = h('div', { className: 'grid-2col' });

    // Recent Papers
    const papersSection = h('div', { className: 'section' });
    papersSection.appendChild(sectionHeader('Recent Papers', 'View all', () => navigate('/papers')));

    if (papersRes.items.length === 0) {
      papersSection.appendChild(emptyState('No papers yet', 'Import papers to get started'));
    } else {
      const table = h('table', { className: 'table' });
      const thead = h('thead', {},
        h('tr', {},
          h('th', {}, 'Title'),
          h('th', {}, 'Year'),
          h('th', {}, 'Status'),
          h('th', { className: 'cell-num' }, 'Citations'),
        )
      );
      const tbody = h('tbody');

      for (const paper of papersRes.items) {
        const row = h('tr', {
          className: 'clickable',
          onClick: () => navigate(`/papers/${paper.id}`),
        },
          h('td', {},
            h('div', { className: 'cell-title truncate', style: 'max-width:320px' }, paper.title),
            h('div', { className: 'cell-meta' }, formatAuthors(paper.authors, 2)),
          ),
          h('td', {}, paper.year ? String(paper.year) : '\u2014'),
          h('td', {}, badge(paper.status)),
          h('td', { className: 'cell-num' }, String(paper.citationCount)),
        );
        tbody.appendChild(row);
      }

      table.appendChild(thead);
      table.appendChild(tbody);
      papersSection.appendChild(h('div', { className: 'table-wrap' }, table));
    }

    grid.appendChild(papersSection);

    // Active Jobs
    const jobsSection = h('div', { className: 'section' });
    jobsSection.appendChild(sectionHeader('Recent Jobs', 'View all', () => navigate('/jobs')));

    if (jobsRes.items.length === 0) {
      jobsSection.appendChild(emptyState('No jobs', 'Jobs will appear here when created'));
    } else {
      const jobTable = h('table', { className: 'table' });
      const jobThead = h('thead', {},
        h('tr', {},
          h('th', {}, 'Type'),
          h('th', {}, 'Status'),
          h('th', {}, 'Created'),
        )
      );
      const jobTbody = h('tbody');

      for (const job of jobsRes.items) {
        const row = h('tr', {
          className: 'clickable',
          onClick: () => navigate(`/jobs/${job.id}`),
        },
          h('td', {},
            h('div', { className: 'cell-title' }, formatJobType(job.type)),
          ),
          h('td', {}, badge(job.status)),
          h('td', { className: 'text-secondary' }, timeAgo(job.createdAt)),
        );
        jobTbody.appendChild(row);
      }

      jobTable.appendChild(jobThead);
      jobTable.appendChild(jobTbody);
      jobsSection.appendChild(h('div', { className: 'table-wrap' }, jobTable));
    }

    grid.appendChild(jobsSection);
    container.appendChild(grid);

  } catch (err) {
    clear(container);
    if (err.name === 'AbortError') return;
    container.appendChild(
      emptyState(
        'Unable to connect',
        `Check that the API is running and the URL is correct. Error: ${err.message}`
      )
    );
    toast(err.message, 'error');
  }
}

function statCard(value, label, color = '') {
  return h('div', { className: `stat-card ${color}` },
    h('div', { className: 'stat-value' }, String(value)),
    h('div', { className: 'stat-label' }, label),
  );
}

function sectionHeader(title, actionText, onAction) {
  return h('div', { className: 'section-header' },
    h('h2', { className: 'section-title' }, title),
    actionText ? h('a', { className: 'section-action', href: 'javascript:void(0)', onClick: onAction }, actionText) : null,
  );
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
