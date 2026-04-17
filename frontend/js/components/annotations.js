import {
  h, clear, loading, badge, formatDateTime,
  toast, emptyState
} from '../components.js';

export function renderAnnotationSidebar(container, { paperId, annotations, hypotheses = [], onCreate, onUpdate, onDelete }) {
  clear(container);

  const wrapper = h('div', { className: 'annotation-sidebar' });

  wrapper.appendChild(
    h('div', { className: 'section-header' },
      h('h2', { className: 'section-title' }, `Annotations (${annotations.length})`),
    )
  );

  if (hypotheses.length > 0) {
    const hypothesisSection = h('div', { className: 'hypothesis-links-section', style: 'margin-bottom: var(--s-6); padding: var(--s-4); background: var(--c-bg); border-radius: var(--radius);' });
    hypothesisSection.appendChild(
      h('h3', { style: 'font-size: 14px; color: var(--c-text-secondary); margin-bottom: var(--s-3);' }, 'LINKED HYPOTHESES')
    );
    for (const hyp of hypotheses) {
      hypothesisSection.appendChild(
        h('div', { className: 'hypothesis-link-item', style: 'padding: var(--s-2) 0; border-bottom: 1px solid var(--c-border);' },
          h('span', { style: 'font-weight: 500;' }, hyp.title || hyp.id),
          hyp.status ? h('span', { style: 'margin-left: 8px; font-size: 12px; color: var(--c-text-secondary);' }, `(${hyp.status})`) : null
        )
      );
    }
    wrapper.appendChild(hypothesisSection);
  }

  if (annotations.length === 0) {
    wrapper.appendChild(emptyState('No annotations', 'Highlight text to create an annotation'));
    return container.appendChild(wrapper);
  }

  const filterRow = h('div', { className: 'flex gap-2 mb-4' });
  const uniqueUsers = [...new Set(annotations.map(a => a.userName))];
  const allBtn = h('button', {
    className: 'btn btn-secondary btn-xs active',
    onClick: () => filterAnnotations(null)
  }, 'All');
  filterRow.appendChild(allBtn);

  for (const userName of uniqueUsers) {
    const btn = h('button', {
      className: 'btn btn-secondary btn-xs',
      onClick: () => filterAnnotations(userName)
    }, userName);
    filterRow.appendChild(btn);
  }
  wrapper.appendChild(filterRow);

  const list = h('div', { className: 'annotation-list' });
  for (const annotation of annotations) {
    list.appendChild(renderAnnotationCard(annotation, paperId, onUpdate, onDelete));
  }
  wrapper.appendChild(list);

  container.appendChild(wrapper);
}

function renderAnnotationCard(annotation, paperId, onUpdate, onDelete) {
  const card = h('div', {
    className: 'annotation-card',
    dataset: { annotationId: annotation.id }
  });

  const header = h('div', { className: 'annotation-card-header' },
    h('span', { className: 'annotation-user' }, annotation.userName),
    h('span', { className: 'annotation-date' }, formatDateTime(annotation.createdAt))
  );
  card.appendChild(header);

  const highlight = h('div', { className: 'annotation-highlight' }, annotation.highlightedText);
  card.appendChild(highlight);

  if (annotation.note) {
    card.appendChild(h('div', { className: 'annotation-note' }, annotation.note));
  }

  const actions = h('div', { className: 'annotation-actions' });
  actions.appendChild(
    h('button', {
      className: 'btn btn-secondary btn-xs',
      onClick: async () => {
        const newNote = prompt('Edit note:', annotation.note || '');
        if (newNote !== null) {
          try {
            await onUpdate(annotation.id, { note: newNote });
            toast('Annotation updated', 'success');
          } catch (err) {
            toast(err.message, 'error');
          }
        }
      }
    }, 'EDIT')
  );
  actions.appendChild(
    h('button', {
      className: 'btn btn-danger btn-xs',
      onClick: async () => {
        if (confirm('Delete this annotation?')) {
          try {
            await onDelete(annotation.id);
            toast('Annotation deleted', 'success');
          } catch (err) {
            toast(err.message, 'error');
          }
        }
      }
    }, 'DELETE')
  );
  card.appendChild(actions);

  return card;
}

function filterAnnotations(userName) {
  document.querySelectorAll('.annotation-card').forEach(card => {
    const annotationUser = card.querySelector('.annotation-user')?.textContent;
    if (!userName || annotationUser === userName) {
      card.style.display = '';
    } else {
      card.style.display = 'none';
    }
  });

  document.querySelectorAll('.annotation-sidebar .btn-xs').forEach(btn => {
    btn.classList.toggle('active', btn.textContent === (userName || 'All'));
  });
}

export function renderHighlightOverlay(container, annotations) {
  clear(container);

  for (const annotation of annotations) {
    const overlay = h('div', {
      className: `highlight-overlay highlight-${getUserColor(annotation.userId)}`,
      title: `${annotation.userName}: ${annotation.highlightedText}`,
      onClick: () => showAnnotationTooltip(annotation)
    });

    if (annotation.note) {
      overlay.dataset.note = annotation.note;
    }

    container.appendChild(overlay);
  }
}

function getUserColor(userId) {
  const colors = ['blue', 'green', 'purple', 'orange', 'red', 'teal'];
  let hash = 0;
  const str = String(userId);
  for (let i = 0; i < str.length; i++) {
    hash = ((hash << 5) - hash) + str.charCodeAt(i);
    hash |= 0;
  }
  return colors[Math.abs(hash) % colors.length];
}

function showAnnotationTooltip(annotation) {
  const existing = document.querySelector('.annotation-tooltip');
  if (existing) existing.remove();

  const tooltip = h('div', { className: 'annotation-tooltip' },
    h('div', { className: 'tooltip-header' },
      h('span', { className: 'tooltip-user' }, annotation.userName),
      h('span', { className: 'tooltip-date' }, formatDateTime(annotation.createdAt))
    ),
    h('div', { className: 'tooltip-highlight' }, annotation.highlightedText)
  );

  if (annotation.note) {
    tooltip.appendChild(h('div', { className: 'tooltip-note' }, annotation.note));
  }

  document.body.appendChild(tooltip);

  setTimeout(() => {
    document.addEventListener('click', function handler(e) {
      if (!tooltip.contains(e.target)) {
        tooltip.remove();
        document.removeEventListener('click', handler);
      }
    });
  }, 0);
}

export function createHighlightButton(text, onConfirm) {
  return h('button', {
    className: 'btn btn-primary btn-sm',
    onClick: () => {
      const note = prompt('Add a note (optional):');
      if (note !== null) {
        onConfirm({ highlightedText: text, note: note || null });
      }
    }
  }, 'ANNOTATE');
}