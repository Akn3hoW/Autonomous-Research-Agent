import {
  getHypotheses,
  getHypothesis,
  createHypothesis,
  updateHypothesis,
  updateHypothesisStatus,
  deleteHypothesis,
  addHypothesisPaper,
  removeHypothesisPaper,
  getPaper
} from '../api.js';
import {
  h,
  clear,
  loading,
  toast,
  emptyState,
  badge,
  formatDateTime,
  truncate
} from '../components.js';

const STATUS_COLUMNS = [
  { key: 'Proposed', label: 'Proposed', color: 'gray' },
  { key: 'Supported', label: 'Supported', color: 'green' },
  { key: 'Refuted', label: 'Refuted', color: 'red' },
  { key: 'Open', label: 'Open', color: 'blue' },
];

let hypotheses = [];
let expandedId = null;

export async function render(container, { signal, navigate }) {
  clear(container);
  expandedId = null;

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Hypothesis Tracker'),
      h('p', { className: 'page-subtitle' }, 'Track and manage research hypotheses with supporting evidence'),
    )
  );

  const board = h('div', { className: 'hypothesis-board' });
  container.appendChild(board);

  const createBtn = h('button', {
    className: 'btn btn-primary',
    style: 'margin-bottom: var(--s-4)',
    onClick: () => showCreateModal()
  }, 'NEW HYPOTHESIS');
  container.appendChild(createBtn);

  await loadHypotheses(board, signal);

  container.appendChild(board);
}

async function loadHypotheses(board, signal) {
  clear(board);
  board.appendChild(loading('Loading hypotheses...'));

  try {
    hypotheses = await getHypotheses(signal);
    clear(board);
    renderBoard(board);
  } catch (err) {
    if (err.name === 'AbortError') return;
    clear(board);
    board.appendChild(emptyState('Error', err.message));
    toast(err.message, 'error');
  }
}

function renderBoard(board) {
  const columns = h('div', { className: 'board-columns' });

  for (const status of STATUS_COLUMNS) {
    const column = renderColumn(status);
    columns.appendChild(column);
  }

  board.appendChild(columns);
}

function renderColumn(status) {
  const columnHypotheses = hypotheses.filter(h => h.status === status.key);

  const column = h('div', { className: 'board-column' },
    h('div', { className: 'column-header' },
      h('span', { className: `badge badge-${status.color}` }, status.label),
      h('span', { className: 'column-count' }, columnHypotheses.length)
    ),
    h('div', { className: 'column-cards' })
  );

  const cardsContainer = column.querySelector('.column-cards');

  if (columnHypotheses.length === 0) {
    cardsContainer.appendChild(
      h('div', { className: 'empty-column' }, 'No hypotheses')
    );
  } else {
    for (const hypothesis of columnHypotheses) {
      const card = renderHypothesisCard(hypothesis);
      cardsContainer.appendChild(card);
    }
  }

  return column;
}

function renderHypothesisCard(hypothesis) {
  const supportingCount = hypothesis.supportingPapers?.length || 0;
  const refutingCount = hypothesis.refutingPapers?.length || 0;
  const isExpanded = expandedId === hypothesis.id;

  const card = h('div', {
    className: `hypothesis-card ${isExpanded ? 'expanded' : ''}`,
    onClick: (e) => {
      e.stopPropagation();
      expandedId = isExpanded ? null : hypothesis.id;
      const board = document.querySelector('.hypothesis-board');
      if (board) renderBoard(board);
    }
  });

  card.appendChild(
    h('div', { className: 'card-title' }, truncate(hypothesis.title, 80))
  );

  card.appendChild(
    h('div', { className: 'card-meta' },
      h('span', { className: 'evidence-count supporting' },
        h('span', { className: 'evidence-icon' }, '\u2713'),
        supportingCount
      ),
      h('span', { className: 'evidence-count refuting' },
        h('span', { className: 'evidence-icon' }, '\u2717'),
        refutingCount
      )
    )
  );

  if (isExpanded) {
    card.appendChild(
      h('div', { className: 'card-expanded' },
        h('div', { className: 'card-description' }, hypothesis.description || 'No description'),
        h('div', { className: 'card-evidence' },
          h('h4', {}, 'Supporting Evidence'),
          renderEvidenceList(hypothesis.supportingPapers, 'supporting')
        ),
        h('div', { className: 'card-evidence' },
          h('h4', {}, 'Refuting Evidence'),
          renderEvidenceList(hypothesis.refutingPapers, 'refuting')
        ),
        h('div', { className: 'card-actions' },
          h('button', {
            className: 'btn btn-sm btn-ghost',
            onClick: (e) => {
              e.stopPropagation();
              showAddPaperModal(hypothesis.id, 'Supporting');
            }
          }, 'Add Supporting'),
          h('button', {
            className: 'btn btn-sm btn-ghost',
            onClick: (e) => {
              e.stopPropagation();
              showAddPaperModal(hypothesis.id, 'Refuting');
            }
          }, 'Add Refuting'),
          h('button', {
            className: 'btn btn-sm btn-ghost',
            onClick: (e) => {
              e.stopPropagation();
              showStatusModal(hypothesis);
            }
          }, 'Change Status'),
          h('button', {
            className: 'btn btn-sm btn-ghost',
            style: 'color: var(--c-red)',
            onClick: (e) => {
              e.stopPropagation();
              confirmDelete(hypothesis);
            }
          }, 'Delete')
        )
      )
    );
  }

  return card;
}

function renderEvidenceList(papers, type) {
  if (!papers || papers.length === 0) {
    return h('div', { className: 'empty-evidence' }, 'No evidence');
  }

  const list = h('div', { className: `evidence-list ${type}` });

  for (const paper of papers) {
    const item = h('div', { className: 'evidence-item' },
      h('div', { className: 'evidence-paper-title' },
        h('a', {
          href: `#/papers/${paper.paperId}`,
          onClick: (e) => e.stopPropagation()
        }, paper.paperTitle || 'Unknown Paper')
      )
    );

    if (paper.evidenceText) {
      item.appendChild(
        h('div', { className: 'evidence-text' }, truncate(paper.evidenceText, 150))
      );
    }

    list.appendChild(item);
  }

  return list;
}

function showCreateModal() {
  const modal = createModal('Create Hypothesis');

  const titleInput = h('input', {
    className: 'input',
    type: 'text',
    placeholder: 'Hypothesis title'
  });

  const descInput = h('textarea', {
    className: 'input',
    placeholder: 'Description (optional)',
    rows: 3
  });

  modal.body.appendChild(
    h('div', { className: 'form-group' },
      h('label', { className: 'field-label' }, 'Title'),
      titleInput
    ),
    h('div', { className: 'form-group' },
      h('label', { className: 'field-label' }, 'Description'),
      descInput
    )
  );

  modal.footer.innerHTML = '';
  modal.footer.appendChild(
    h('button', {
      className: 'btn btn-primary',
      onClick: async () => {
        const title = titleInput.value.trim();
        if (!title) {
          toast('Title is required', 'error');
          return;
        }
        try {
          await createHypothesis({
            title,
            description: descInput.value.trim()
          });
          toast('Hypothesis created', 'success');
          modal.close();
          const board = document.querySelector('.hypothesis-board');
          if (board) await loadHypotheses(board, {});
        } catch (err) {
          toast(err.message, 'error');
        }
      }
    }, 'CREATE'),
    h('button', {
      className: 'btn btn-ghost modal-close',
      onClick: () => modal.close()
    }, 'CANCEL')
  );
}

function showAddPaperModal(hypothesisId, evidenceType) {
  const modal = createModal(`Add ${evidenceType} Paper`);

  const paperIdInput = h('input', {
    className: 'input',
    type: 'text',
    placeholder: 'Paper ID (e.g., 3fa85f64-5717-4562-b3fc-2c963f66afa6)'
  });

  const evidenceInput = h('textarea', {
    className: 'input',
    placeholder: 'Evidence text (optional)',
    rows: 3
  });

  modal.body.appendChild(
    h('div', { className: 'form-group' },
      h('label', { className: 'field-label' }, 'Paper ID'),
      paperIdInput
    ),
    h('div', { className: 'form-group' },
      h('label', { className: 'field-label' }, 'Evidence'),
      evidenceInput
    )
  );

  modal.footer.innerHTML = '';
  modal.footer.appendChild(
    h('button', {
      className: 'btn btn-primary',
      onClick: async () => {
        const paperId = paperIdInput.value.trim();
        if (!paperId) {
          toast('Paper ID is required', 'error');
          return;
        }
        try {
          await addHypothesisPaper(hypothesisId, {
            paperId,
            evidenceType,
            evidenceText: evidenceInput.value.trim() || null
          });
          toast('Paper added', 'success');
          modal.close();
          const board = document.querySelector('.hypothesis-board');
          if (board) await loadHypotheses(board, {});
        } catch (err) {
          toast(err.message, 'error');
        }
      }
    }, 'ADD'),
    h('button', {
      className: 'btn btn-ghost modal-close',
      onClick: () => modal.close()
    }, 'CANCEL')
  );
}

function showStatusModal(hypothesis) {
  const modal = createModal('Change Status');

  const statusSelect = h('select', { className: 'input select' });
  for (const status of STATUS_COLUMNS) {
    statusSelect.appendChild(
      h('option', { value: status.key }, status.label)
    );
  }
  statusSelect.value = hypothesis.status;

  const evidenceInput = h('textarea', {
    className: 'input',
    placeholder: 'Evidence text (optional)',
    rows: 3
  });

  modal.body.appendChild(
    h('div', { className: 'form-group' },
      h('label', { className: 'field-label' }, 'Status'),
      statusSelect
    ),
    h('div', { className: 'form-group' },
      h('label', { className: 'field-label' }, 'Evidence'),
      evidenceInput
    )
  );

  modal.footer.innerHTML = '';
  modal.footer.appendChild(
    h('button', {
      className: 'btn btn-primary',
      onClick: async () => {
        try {
          await updateHypothesisStatus(hypothesis.id, {
            status: statusSelect.value,
            evidenceText: evidenceInput.value.trim() || null
          });
          toast('Status updated', 'success');
          modal.close();
          const board = document.querySelector('.hypothesis-board');
          if (board) await loadHypotheses(board, {});
        } catch (err) {
          toast(err.message, 'error');
        }
      }
    }, 'UPDATE'),
    h('button', {
      className: 'btn btn-ghost modal-close',
      onClick: () => modal.close()
    }, 'CANCEL')
  );
}

function confirmDelete(hypothesis) {
  if (!confirm(`Delete hypothesis "${hypothesis.title}"?`)) return;

  deleteHypothesis(hypothesis.id)
    .then(() => {
      toast('Hypothesis deleted', 'success');
      expandedId = null;
      const board = document.querySelector('.hypothesis-board');
      if (board) loadHypotheses(board, {});
    })
    .catch(err => toast(err.message, 'error'));
}

function createModal(title) {
  const existing = document.getElementById('hypothesis-modal');
  if (existing) existing.remove();

  const backdrop = h('div', { className: 'modal-backdrop' });
  const modal = h('div', {
    className: 'modal-panel',
    id: 'hypothesis-modal',
    onClick: (e) => e.stopPropagation()
  });

  const header = h('div', { className: 'modal-header' },
    h('h2', { className: 'modal-title' }, title),
    h('button', {
      className: 'modal-close',
      type: 'button',
      onClick: () => modalClose()
    }, '\u00D7')
  );

  const body = h('div', { className: 'modal-body' });
  const footer = h('div', { className: 'modal-footer' });

  modal.appendChild(header);
  modal.appendChild(body);
  modal.appendChild(footer);
  document.body.appendChild(modal);
  document.body.appendChild(backdrop);

  backdrop.addEventListener('click', modalClose);

  function modalClose() {
    backdrop.remove();
    modal.remove();
  }

  modal.close = modalClose;

  requestAnimationFrame(() => {
    modal.classList.add('open');
    backdrop.classList.add('open');
  });

  return { modal, body, footer };
}
