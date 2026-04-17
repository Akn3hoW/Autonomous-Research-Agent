import {
  getSummary, getPaper, approveSummary, rejectSummary
} from '../api.js';
import {
  h, clear, loading, badge, formatAuthors, formatDate, formatDateTime,
  toast, emptyState
} from '../components.js';

export async function render(container, { navigate, params }) {
  const summaryId = params.id;
  clear(container);
  container.appendChild(loading('Loading summary'));

  try {
    const summary = await getSummary(summaryId);
    clear(container);

    container.appendChild(
      h('a', {
        className: 'detail-back',
        href: '#/summaries',
        onClick: (e) => { e.preventDefault(); navigate('/summaries'); }
      }, '\u2190 SUMMARIES')
    );

    let paper = null;
    try {
      paper = await getPaper(summary.paperId);
    } catch {}

    const header = h('div', { className: 'flex items-center gap-4 mb-6' },
      h('h1', { className: 'detail-title', style: 'margin-bottom:0' }, paper ? paper.title : 'Summary'),
      badge(summary.status),
    );
    container.appendChild(header);

    if (paper) {
      const meta = h('div', { className: 'detail-meta' });
      const metaItems = [
        ['Authors', formatAuthors(paper.authors, 10)],
        ['Year', paper.year ? String(paper.year) : '\u2014'],
        ['Venue', paper.venue || '\u2014'],
        ['Citations', String(paper.citationCount)],
        ['Added', formatDate(paper.createdAt)],
      ];
      for (const [label, value] of metaItems) {
        meta.appendChild(
          h('div', { className: 'meta-item' },
            h('span', { className: 'meta-label' }, label),
            h('span', { className: 'meta-value' }, value),
          )
        );
      }
      container.appendChild(meta);
    }

    const summaryMeta = h('div', { className: 'detail-meta' });
    summaryMeta.appendChild(
      h('div', { className: 'meta-item' },
        h('span', { className: 'meta-label' }, 'Model'),
        h('span', { className: 'meta-value' }, summary.modelName || '\u2014'),
      )
    );
    if (summary.promptVersion) {
      summaryMeta.appendChild(
        h('div', { className: 'meta-item' },
          h('span', { className: 'meta-label' }, 'Prompt Version'),
          h('span', { className: 'meta-value' }, `v${summary.promptVersion}`),
        )
      );
    }
    summaryMeta.appendChild(
      h('div', { className: 'meta-item' },
        h('span', { className: 'meta-label' }, 'Created'),
        h('span', { className: 'meta-value' }, formatDateTime(summary.createdAt)),
      )
    );
    summaryMeta.appendChild(
      h('div', { className: 'meta-item' },
        h('span', { className: 'meta-label' }, 'Updated'),
        h('span', { className: 'meta-value' }, formatDateTime(summary.updatedAt)),
      )
    );
    container.appendChild(summaryMeta);

    if (summary.reviewedBy) {
      container.appendChild(
        h('div', { className: 'section' },
          h('div', { className: 'section-header' },
            h('h2', { className: 'section-title' }, 'Review'),
          ),
          h('div', { className: 'summary-card-body' },
            `Reviewed by ${summary.reviewedBy} on ${formatDateTime(summary.reviewedAt)}`,
            summary.reviewNotes ? h('p', { style: 'margin-top:8px' }, `Notes: ${summary.reviewNotes}`) : null,
          ),
        )
      );
    }

    container.appendChild(
      h('div', { className: 'section' },
        h('div', { className: 'section-header' },
          h('h2', { className: 'section-title' }, 'Summary'),
        ),
        h('div', { className: 'detail-abstract', style: 'max-width:none;white-space:pre-wrap;font-family:var(--font);font-size:var(--text-base);line-height:1.7' },
          typeof summary.summary === 'string' ? summary.summary : JSON.stringify(summary.summary, null, 2)
        ),
      )
    );

    if (summary.status === 'Generated' || summary.status === 'Pending') {
      const actions = h('div', { className: 'page-actions' });
      actions.appendChild(
        h('button', {
          className: 'btn btn-primary',
          onClick: async () => {
            try {
              await approveSummary(summaryId, {});
              toast('Summary approved', 'success');
              navigate(`/summaries/${summaryId}`);
            } catch (err) {
              toast(err.message, 'error');
            }
          },
        }, 'APPROVE SUMMARY')
      );
      actions.appendChild(
        h('button', {
          className: 'btn btn-danger',
          onClick: async () => {
            try {
              await rejectSummary(summaryId, {});
              toast('Summary rejected', 'success');
              navigate(`/summaries/${summaryId}`);
            } catch (err) {
              toast(err.message, 'error');
            }
          },
        }, 'REJECT SUMMARY')
      );
      container.appendChild(actions);
    }

  } catch (err) {
    clear(container);
    if (err.name === 'AbortError') return;
    container.appendChild(emptyState('Summary not found', err.message));
    toast(err.message, 'error');
  }
}
