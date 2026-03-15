import {
  getPaper, updatePaper, getPaperSummaries, getPaperDocuments,
  approveSummary, rejectSummary, createSummarizeJob, queueDocumentProcessing
} from '../api.js';
import {
  h, clear, loading, badge, formatAuthors, formatDate, formatDateTime,
  jsonBlock, toast, emptyState
} from '../components.js';

export async function render(container, { navigate, params }) {
  const paperId = params.id;
  clear(container);
  container.appendChild(loading('Loading paper'));

  try {
    const [paper, summaries, documents] = await Promise.all([
      getPaper(paperId),
      getPaperSummaries(paperId).catch(() => []),
      getPaperDocuments(paperId).catch(() => []),
    ]);

    clear(container);

    // Back link
    container.appendChild(
      h('a', {
        className: 'detail-back',
        href: '#/papers',
        onClick: (e) => { e.preventDefault(); navigate('/papers'); }
      }, '\u2190 PAPERS')
    );

    // Title + status
    const headerRow = h('div', { className: 'flex items-center gap-4 mb-6' },
      h('h1', { className: 'detail-title', style: 'margin-bottom:0' }, paper.title),
      badge(paper.status),
    );
    container.appendChild(headerRow);

    // Meta row
    const meta = h('div', { className: 'detail-meta' });
    const metaItems = [
      ['Authors', formatAuthors(paper.authors, 10)],
      ['Year', paper.year ? String(paper.year) : '\u2014'],
      ['Venue', paper.venue || '\u2014'],
      ['Citations', String(paper.citationCount)],
      ['Source', paper.source],
      ['Added', formatDate(paper.createdAt)],
    ];
    if (paper.doi) metaItems.push(['DOI', paper.doi]);
    if (paper.semanticScholarId) metaItems.push(['S2 ID', paper.semanticScholarId]);

    for (const [label, value] of metaItems) {
      meta.appendChild(
        h('div', { className: 'meta-item' },
          h('span', { className: 'meta-label' }, label),
          h('span', { className: 'meta-value' }, value),
        )
      );
    }
    container.appendChild(meta);

    // Abstract
    if (paper.abstract) {
      container.appendChild(
        h('div', { className: 'section' },
          h('div', { className: 'section-header' },
            h('h2', { className: 'section-title' }, 'Abstract'),
          ),
          h('p', { className: 'detail-abstract' }, paper.abstract),
        )
      );
    }

    // Actions
    const actions = h('div', { className: 'page-actions mb-8' });
    const summarizeBtn = h('button', {
      className: 'btn btn-primary btn-sm',
      onClick: async () => {
        try {
          summarizeBtn.disabled = true;
          summarizeBtn.textContent = 'CREATING JOB\u2026';
          await createSummarizeJob({ paperId });
          toast('Summarize job created', 'success');
          navigate(`/papers/${paperId}`); // Refresh
        } catch (err) {
          toast(err.message, 'error');
          summarizeBtn.disabled = false;
          summarizeBtn.textContent = 'SUMMARIZE';
        }
      },
    }, 'SUMMARIZE');
    actions.appendChild(summarizeBtn);

    const statusBtns = ['Draft', 'Imported', 'Processing', 'Ready', 'Archived'];
    for (const s of statusBtns) {
      if (s === paper.status) continue;
      actions.appendChild(
        h('button', {
          className: 'btn btn-secondary btn-sm',
          onClick: async () => {
            try {
              await updatePaper(paperId, { status: s });
              toast(`Status changed to ${s}`, 'success');
              navigate(`/papers/${paperId}`);
            } catch (err) {
              toast(err.message, 'error');
            }
          },
        }, `SET ${s.toUpperCase()}`)
      );
    }
    container.appendChild(actions);

    // Summaries section
    const summariesSection = h('div', { className: 'section' });
    summariesSection.appendChild(
      h('div', { className: 'section-header' },
        h('h2', { className: 'section-title' }, `Summaries (${summaries.length})`),
      )
    );

    if (summaries.length === 0) {
      summariesSection.appendChild(emptyState('No summaries', 'Create a summarize job to generate one'));
    } else {
      for (const summary of summaries) {
        summariesSection.appendChild(renderSummaryCard(summary, paperId, navigate));
      }
    }
    container.appendChild(summariesSection);

    // Documents section
    const docsSection = h('div', { className: 'section' });
    docsSection.appendChild(
      h('div', { className: 'section-header' },
        h('h2', { className: 'section-title' }, `Documents (${documents.length})`),
      )
    );

    if (documents.length === 0) {
      docsSection.appendChild(emptyState('No documents', 'Documents can be attached to this paper'));
    } else {
      const docTable = h('table', { className: 'table' });
      docTable.appendChild(
        h('thead', {},
          h('tr', {},
            h('th', {}, 'File'),
            h('th', {}, 'Type'),
            h('th', {}, 'Status'),
            h('th', {}, 'Updated'),
            h('th', {}, ''),
          )
        )
      );
      const tbody = h('tbody');
      for (const doc of documents) {
        tbody.appendChild(
          h('tr', {},
            h('td', {},
              h('div', { className: 'cell-title' }, doc.fileName || 'Document'),
              h('div', { className: 'cell-meta cell-mono truncate', style: 'max-width:300px' }, doc.sourceUrl),
            ),
            h('td', {}, doc.mediaType || '\u2014'),
            h('td', {}, badge(doc.status)),
            h('td', { className: 'text-secondary' }, formatDateTime(doc.updatedAt)),
            h('td', {},
              doc.status !== 'Extracted'
                ? h('button', {
                    className: 'btn btn-secondary btn-sm',
                    onClick: async () => {
                      try {
                        await queueDocumentProcessing(paperId, doc.id, { force: false });
                        toast('Processing queued', 'success');
                        navigate(`/papers/${paperId}`);
                      } catch (err) {
                        toast(err.message, 'error');
                      }
                    },
                  }, 'PROCESS')
                : null,
            ),
          )
        );
      }
      docTable.appendChild(tbody);
      docsSection.appendChild(h('div', { className: 'table-wrap' }, docTable));
    }
    container.appendChild(docsSection);

    // Metadata
    if (paper.metadata) {
      container.appendChild(
        h('div', { className: 'section' },
          h('div', { className: 'section-header' },
            h('h2', { className: 'section-title' }, 'Metadata'),
          ),
          jsonBlock(paper.metadata),
        )
      );
    }

  } catch (err) {
    clear(container);
    if (err.name === 'AbortError') return;
    container.appendChild(emptyState('Paper not found', err.message));
    toast(err.message, 'error');
  }
}

function renderSummaryCard(summary, paperId, navigate) {
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

  // Summary content
  if (summary.summary) {
    const content = typeof summary.summary === 'string' ? summary.summary : JSON.stringify(summary.summary, null, 2);
    card.appendChild(h('div', { className: 'summary-card-body' },
      h('pre', { className: 'json-block', style: 'max-height:200px' }, content),
    ));
  }

  // Review info
  if (summary.reviewedBy) {
    card.appendChild(h('div', { className: 'summary-card-meta mt-4' },
      `Reviewed by ${summary.reviewedBy} on ${formatDateTime(summary.reviewedAt)}`,
      summary.reviewNotes ? ` \u2014 ${summary.reviewNotes}` : '',
    ));
  }

  // Actions
  if (summary.status === 'Generated' || summary.status === 'Pending') {
    const actions = h('div', { className: 'summary-card-actions' });
    actions.appendChild(
      h('button', {
        className: 'btn btn-primary btn-sm',
        onClick: async () => {
          try {
            await approveSummary(summary.id, {});
            toast('Summary approved', 'success');
            navigate(`/papers/${paperId}`);
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
            navigate(`/papers/${paperId}`);
          } catch (err) {
            toast(err.message, 'error');
          }
        },
      }, 'REJECT')
    );
    card.appendChild(actions);
  }

  return card;
}
