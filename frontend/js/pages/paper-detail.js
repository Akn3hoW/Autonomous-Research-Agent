import {
  getPaper, updatePaper, getPaperSummaries, getPaperDocuments,
  approveSummary, rejectSummary, createSummarizeJob, queueDocumentProcessing,
  createPaperDocument, getPaperAnnotations, createAnnotation,
  updateAnnotation, deleteAnnotation, getHypotheses
} from '../api.js';
import {
  h, clear, loading, badge, formatAuthors, formatDate, formatDateTime,
  jsonBlock, toast, emptyState
} from '../components.js';
import { renderAnnotationSidebar, createHighlightButton } from '../components/annotations.js';
import { renderCitationGraph } from '../components/CitationGraph.js';
import { store } from '../store.js';

export async function render(container, { navigate, params }) {
  const paperId = params.id;
  clear(container);
  container.appendChild(loading('Loading paper'));

  try {
    const [paper, summaries, documents, annotations, hypotheses] = await Promise.all([
      getPaper(paperId),
      getPaperSummaries(paperId).catch(() => []),
      getPaperDocuments(paperId).catch(() => []),
      getPaperAnnotations(paperId).catch(() => []),
      getHypotheses().catch(() => []),
    ]);

    clear(container);

    store.setSlice('papers', { currentPaper: paper });

    container.appendChild(
      h('a', {
        className: 'detail-back',
        href: '#/papers',
        onClick: (e) => { e.preventDefault(); navigate('/papers'); }
      }, '\u2190 PAPERS')
    );

    const headerRow = h('div', { className: 'flex items-center gap-4 mb-6' },
      h('h1', { className: 'detail-title', style: 'margin-bottom:0' }, paper.title),
      badge(paper.status),
    );
    container.appendChild(headerRow);

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

    const tabs = h('div', { className: 'tabs' });
    const tabNames = ['Overview', 'Documents', 'Citation Graph', 'Annotations'];
    let activeTab = 'Overview';

    const tabBtns = tabNames.map(name => {
      const tab = h('button', {
        className: `tab ${activeTab === name ? 'active' : ''}`,
        onClick: () => {
          activeTab = name;
          tabBtns.forEach((t, i) => t.classList.toggle('active', tabNames[i] === name));
          overviewContent.style.display = name === 'Overview' ? '' : 'none';
          docsContent.style.display = name === 'Documents' ? '' : 'none';
          graphContent.style.display = name === 'Citation Graph' ? '' : 'none';
          annotationContent.style.display = name === 'Annotations' ? '' : 'none';
        },
      }, name);
      if (name === 'Documents') {
        tab.appendChild(h('span', { className: 'tab-count' }, documents.length));
      }
      if (name === 'Annotations') {
        tab.appendChild(h('span', { className: 'tab-count' }, annotations.length));
      }
      return tab;
    });
    tabs.appendChild(...tabBtns);
    container.appendChild(tabs);

    const overviewContent = h('div', { id: 'tab-overview' });
    const docsContent = h('div', { id: 'tab-documents', style: 'display:none' });
    const graphContent = h('div', { id: 'tab-graph', style: 'display:none' });
    const annotationContent = h('div', { id: 'tab-annotations', style: 'display:none' });

    if (paper.abstract) {
      overviewContent.appendChild(
        h('div', { className: 'section' },
          h('div', { className: 'section-header' },
            h('h2', { className: 'section-title' }, 'Abstract'),
          ),
          h('p', { className: 'detail-abstract' }, paper.abstract),
        )
      );
    }

    const actions = h('div', { className: 'page-actions mb-8' });
    const summarizeBtn = h('button', {
      className: 'btn btn-primary btn-sm',
      onClick: async () => {
        try {
          summarizeBtn.disabled = true;
          summarizeBtn.textContent = 'CREATING JOB\u2026';
          await createSummarizeJob({ paperId });
          toast('Summarize job created', 'success');
          navigate(`/papers/${paperId}`);
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
    overviewContent.appendChild(actions);

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
    overviewContent.appendChild(summariesSection);

    if (paper.metadata) {
      overviewContent.appendChild(
        h('div', { className: 'section' },
          h('div', { className: 'section-header' },
            h('h2', { className: 'section-title' }, 'Metadata'),
          ),
          jsonBlock(paper.metadata),
        )
      );
    }

    const addDocForm = h('div', { className: 'section' });
    addDocForm.appendChild(
      h('div', { className: 'section-header' },
        h('h2', { className: 'section-title' }, 'Add Document'),
      )
    );

    const urlField = h('div', { className: 'field-group' },
      h('label', { className: 'field-label' }, 'Source URL'),
      h('input', { type: 'url', className: 'input', id: 'doc-url-input', placeholder: 'https://example.com/paper.pdf' })
    );
    addDocForm.appendChild(urlField);

    const addDocActions = h('div', { className: 'page-actions' });
    const addDocBtn = h('button', {
      className: 'btn btn-primary btn-sm',
      onClick: async () => {
        const urlInput = document.getElementById('doc-url-input');
        const sourceUrl = urlInput.value.trim();
        if (!sourceUrl) {
          toast('Please enter a source URL', 'error');
          return;
        }
        try {
          addDocBtn.disabled = true;
          addDocBtn.textContent = 'ADDING\u2026';
          await createPaperDocument(paperId, { sourceUrl });
          toast('Document added', 'success');
          navigate(`/papers/${paperId}`);
        } catch (err) {
          toast(err.message, 'error');
          addDocBtn.disabled = false;
          addDocBtn.textContent = 'ADD DOCUMENT';
        }
      },
    }, 'ADD DOCUMENT');
    addDocActions.appendChild(addDocBtn);
    addDocForm.appendChild(addDocActions);

    docsContent.appendChild(addDocForm);

    const docsSection = h('div', { className: 'section' });
    docsSection.appendChild(
      h('div', { className: 'section-header' },
        h('h2', { className: 'section-title' }, `Documents (${documents.length})`),
      )
    );

    if (documents.length === 0) {
      docsSection.appendChild(emptyState('No documents', 'Add a document URL above'));
    } else {
      for (const doc of documents) {
        docsSection.appendChild(renderDocumentCard(doc, paperId, navigate));
      }
    }
    docsContent.appendChild(docsSection);

    container.appendChild(overviewContent);
    container.appendChild(docsContent);
    container.appendChild(graphContent);
    container.appendChild(annotationContent);

    if (paper.semanticScholarId) {
      renderCitationGraph(graphContent, { paperId, navigate });
    } else {
      graphContent.appendChild(emptyState('No Semantic Scholar ID', 'Import this paper from Semantic Scholar to view its citation graph'));
    }

    const handleCreateAnnotation = async (data) => {
      try {
        await createAnnotation(paperId, data);
        toast('Annotation created', 'success');
        const updatedAnnotations = await getPaperAnnotations(paperId);
        store.setSlice('papers', { annotations: updatedAnnotations });
      } catch (err) {
        toast(err.message, 'error');
      }
    };

    const handleUpdateAnnotation = async (id, data) => {
      try {
        await updateAnnotation(id, data);
      } catch (err) {
        toast(err.message, 'error');
      }
    };

    const handleDeleteAnnotation = async (id) => {
      try {
        await deleteAnnotation(id);
      } catch (err) {
        toast(err.message, 'error');
      }
    };

    const linkedHypotheses = hypotheses.filter(hyp =>
      hyp.paperIds && hyp.paperIds.includes(paperId)
    );

    renderAnnotationSidebar(annotationContent, {
      paperId,
      annotations,
      hypotheses: linkedHypotheses,
      onCreate: handleCreateAnnotation,
      onUpdate: handleUpdateAnnotation,
      onDelete: handleDeleteAnnotation
    });

    setupTextHighlighting(annotationContent, annotations, handleCreateAnnotation);

  } catch (err) {
    clear(container);
    if (err.name === 'AbortError') return;
    container.appendChild(emptyState('Paper not found', err.message));
    toast(err.message, 'error');
  }
}

function setupTextHighlighting(container, annotations, onCreateAnnotation) {
  const highlightToolbar = h('div', {
    id: 'highlight-toolbar',
    className: 'highlight-toolbar',
    style: 'position: fixed; bottom: 20px; right: 20px; background: var(--c-surface); border: 1px solid var(--c-border); border-radius: var(--radius); padding: 8px 12px; display: none; z-index: 1000; box-shadow: 0 4px 12px rgba(0,0,0,0.15);'
  });

  highlightToolbar.appendChild(
    h('button', {
      className: 'btn btn-primary btn-sm',
      id: 'create-annotation-btn',
      style: 'margin-right: 8px;'
    }, 'ANNOTATE')
  );

  highlightToolbar.appendChild(
    h('button', {
      className: 'btn btn-secondary btn-sm',
      id: 'cancel-highlight-btn'
    }, 'CANCEL')
  );

  document.body.appendChild(highlightToolbar);

  let selection = null;

  document.addEventListener('mouseup', (e) => {
    const selectedText = window.getSelection().toString().trim();
    const abstractEl = document.querySelector('.detail-abstract');
    const textPanelEls = document.querySelectorAll('.text-panel-content');

    let isInSelectableArea = false;
    if (abstractEl && abstractEl.contains(e.target)) isInSelectableArea = true;
    textPanelEls.forEach(el => {
      if (el.contains(e.target)) isInSelectableArea = true;
    });

    if (selectedText.length > 10 && isInSelectableArea) {
      selection = window.getSelection().getRangeAt(0);
      showHighlightToolbar(e.clientX, e.clientY);
    } else {
      hideHighlightToolbar();
    }
  });

  document.getElementById('create-annotation-btn')?.addEventListener('click', async () => {
    if (!selection) return;
    const selectedText = window.getSelection().toString().trim();
    if (selectedText.length > 0) {
      const note = prompt('Add a note (optional):');
      try {
        await onCreateAnnotation({ highlightedText: selectedText, note: note || null });
        toast('Highlight saved', 'success');
      } catch (err) {
        toast(err.message, 'error');
      }
    }
    hideHighlightToolbar();
    window.getSelection().removeAllRanges();
  });

  document.getElementById('cancel-highlight-btn')?.addEventListener('click', () => {
    hideHighlightToolbar();
    window.getSelection().removeAllRanges();
  });

  function showHighlightToolbar(x, y) {
    highlightToolbar.style.display = 'flex';
    highlightToolbar.style.left = `${Math.min(x, window.innerWidth - 200)}px`;
    highlightToolbar.style.top = `${Math.min(y - 50, window.innerHeight - 80)}px`;
  }

  function hideHighlightToolbar() {
    highlightToolbar.style.display = 'none';
    selection = null;
  }

  renderHighlights(annotations);
}

function renderHighlights(annotations) {
  document.querySelectorAll('.text-highlight').forEach(el => el.remove());

  annotations.forEach(ann => {
    if (!ann.highlightedText) return;

    const abstractEl = document.querySelector('.detail-abstract');
    if (abstractEl && abstractEl.textContent.includes(ann.highlightedText)) {
      highlightText(abstractEl, ann.highlightedText, ann);
    }

    document.querySelectorAll('.text-panel-content').forEach(panel => {
      if (panel.textContent.includes(ann.highlightedText)) {
        highlightText(panel, ann.highlightedText, ann);
      }
    });
  });
}

function highlightText(container, searchText, annotation) {
  const regex = new RegExp(`(${escapeRegex(searchText)})`, 'gi');
  const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT, null, false);

  const textNodes = [];
  while (walker.nextNode()) {
    textNodes.push(walker.currentNode);
  }

  for (const node of textNodes) {
    const text = node.textContent;
    if (regex.test(text)) {
      const fragment = document.createDocumentFragment();
      let lastIndex = 0;
      let match;

      regex.lastIndex = 0;
      while ((match = regex.exec(text)) !== null) {
        if (match.index > lastIndex) {
          fragment.appendChild(document.createTextNode(text.slice(lastIndex, match.index)));
        }
        const span = document.createElement('span');
        span.className = 'text-highlight';
        span.style.backgroundColor = getHighlightColor(annotation.userId);
        span.style.cursor = 'pointer';
        span.textContent = match[0];
        span.dataset.annotationId = annotation.id;
        span.title = `${annotation.userName}: ${annotation.note || 'No note'}`;
        fragment.appendChild(span);
        lastIndex = regex.lastIndex;
      }

      if (lastIndex < text.length) {
        fragment.appendChild(document.createTextNode(text.slice(lastIndex)));
      }

      node.parentNode.replaceChild(fragment, node);
      break;
    }
  }
}

function getHighlightColor(userId) {
  const colors = [
    'rgba(255, 235, 59, 0.4)',
    'rgba(129, 199, 132, 0.4)',
    'rgba(128, 203, 196, 0.4)',
    'rgba(255, 138, 101, 0.4)',
    'rgba(144, 202, 249, 0.4)',
  ];
  let hash = 0;
  const str = String(userId);
  for (let i = 0; i < str.length; i++) {
    hash = ((hash << 5) - hash) + str.charCodeAt(i);
    hash |= 0;
  }
  return colors[Math.abs(hash) % colors.length];
}

function escapeRegex(string) {
  return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
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

  if (summary.summary) {
    const content = typeof summary.summary === 'string' ? summary.summary : JSON.stringify(summary.summary, null, 2);
    card.appendChild(h('div', { className: 'summary-card-body' },
      h('pre', { className: 'json-block', style: 'max-height:200px' }, content),
    ));
  }

  if (summary.reviewedBy) {
    card.appendChild(h('div', { className: 'summary-card-meta mt-4' },
      `Reviewed by ${summary.reviewedBy} on ${formatDateTime(summary.reviewedAt)}`,
      summary.reviewNotes ? ` \u2014 ${summary.reviewNotes}` : '',
    ));
  }

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

function renderDocumentCard(doc, paperId, navigate) {
  const card = h('div', { className: 'summary-card' });

  const header = h('div', { className: 'summary-card-header' },
    h('div', { className: 'flex items-center gap-3' },
      badge(doc.status),
      h('span', { className: 'summary-card-meta' }, doc.mediaType || 'Document'),
    ),
    h('span', { className: 'summary-card-meta' }, formatDateTime(doc.updatedAt)),
  );
  card.appendChild(header);

  if (doc.sourceUrl) {
    card.appendChild(h('div', { className: 'cell-mono truncate', style: 'max-width:500px;font-size:11px;color:var(--c-text-secondary);margin-bottom:var(--s-3)' }, doc.sourceUrl));
  }

  if (doc.lastError) {
    card.appendChild(h('div', { className: 'summary-card-meta', style: 'color:var(--c-red);margin-bottom:var(--s-3)' }, `Error: ${doc.lastError}`));
  }

  if (doc.extractedText) {
    const toggleBtn = h('button', {
      className: 'btn btn-secondary btn-sm',
      onClick: () => {
        const panel = document.getElementById(`doc-text-${doc.id}`);
        if (panel.hidden) {
          panel.hidden = false;
          toggleBtn.textContent = 'HIDE TEXT';
        } else {
          panel.hidden = true;
          toggleBtn.textContent = 'VIEW TEXT';
        }
      },
    }, 'VIEW TEXT');
    card.appendChild(h('div', { style: 'margin-bottom:var(--s-3)' }, toggleBtn));

    const textPanel = h('div', {
      id: `doc-text-${doc.id}`,
      hidden: true,
      className: 'summary-card-body text-panel-content',
    },
      h('pre', {
        className: 'json-block',
        style: 'max-height:400px;font-size:11px;white-space:pre-wrap'
      }, doc.extractedText)
    );
    card.appendChild(textPanel);
  } else if (doc.status === 'Extracted' || doc.status === 'Downloaded') {
    card.appendChild(h('div', { className: 'summary-card-meta', style: 'margin-bottom:var(--s-3)' }, 'No extracted text available'));
  }

  if (doc.status !== 'Extracted' && doc.status !== 'Failed') {
    const processBtn = h('button', {
      className: 'btn btn-primary btn-sm',
      onClick: async () => {
        try {
          processBtn.disabled = true;
          processBtn.textContent = 'QUEUING\u2026';
          await queueDocumentProcessing(paperId, doc.id, { force: false });
          toast('Processing queued', 'success');
          navigate(`/papers/${paperId}`);
        } catch (err) {
          toast(err.message, 'error');
          processBtn.disabled = false;
          processBtn.textContent = 'PROCESS';
        }
      },
    }, 'PROCESS');
    card.appendChild(h('div', { className: 'summary-card-actions' }, processBtn));
  }

  return card;
}

export const init = () => {};
export const cleanup = () => {
  const toolbar = document.getElementById('highlight-toolbar');
  if (toolbar) toolbar.remove();
};