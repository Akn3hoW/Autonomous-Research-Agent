import { getPapers, request } from '../api.js';
import { h, clear, loading, badge, toast, emptyState } from '../components.js';

let currentStep = 1;
let wizardData = {
  title: '',
  researchQuestion: '',
  paperIds: [],
};

export async function render(container, { navigate, params }) {
  clear(container);

  wizardData = { title: '', researchQuestion: '', paperIds: [] };
  currentStep = 1;

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Literature Review'),
      h('p', { className: 'page-subtitle' }, 'Generate structured literature reviews from selected papers'),
    )
  );

  const wizard = h('div', { id: 'wizard-content' });
  container.appendChild(wizard);

  renderStep(wizard);
}

function renderStep(container) {
  clear(container);

  const stepIndicators = h('div', { className: 'wizard-steps' },
    h('div', { className: `wizard-step ${currentStep >= 1 ? 'active' : ''}` }, '1. Details'),
    h('div', { className: `wizard-step ${currentStep >= 2 ? 'active' : ''}` }, '2. Papers'),
    h('div', { className: `wizard-step ${currentStep >= 3 ? 'active' : ''}` }, '3. Preview'),
    h('div', { className: `wizard-step ${currentStep >= 4 ? 'active' : ''}` }, '4. Result'),
  );
  container.appendChild(stepIndicators);

  const content = h('div', { className: 'wizard-content' });
  container.appendChild(content);

  switch (currentStep) {
    case 1: renderStep1(content, container); break;
    case 2: renderStep2(content, container); break;
    case 3: renderStep3(content, container); break;
    case 4: renderStep4(content, container); break;
  }
}

function renderStep1(content, wizardContainer) {
  content.appendChild(
    h('div', { className: 'form-group' },
      h('label', { for: 'review-title', className: 'form-label' }, 'Review Title'),
      h('input', {
        id: 'review-title',
        type: 'text',
        className: 'input',
        placeholder: 'e.g., Machine Learning in Healthcare Diagnostics',
        value: wizardData.title,
      }),
    )
  );

  content.appendChild(
    h('div', { className: 'form-group' },
      h('label', { for: 'research-question', className: 'form-label' }, 'Research Question'),
      h('textarea', {
        id: 'research-question',
        className: 'input',
        rows: 4,
        placeholder: 'e.g., What are the main applications of machine learning in healthcare diagnostics?',
      }, wizardData.researchQuestion),
    )
  );

  content.appendChild(
    h('div', { className: 'wizard-actions' },
      h('button', {
        className: 'btn btn-primary',
        disabled: '',
        onClick: () => {
          wizardData.title = document.getElementById('review-title').value;
          wizardData.researchQuestion = document.getElementById('research-question').value;
          if (!wizardData.title.trim()) {
            toast('Please enter a title', 'error');
            return;
          }
          if (!wizardData.researchQuestion.trim()) {
            toast('Please enter a research question', 'error');
            return;
          }
          currentStep = 2;
          renderStep(wizardContainer);
        },
      }, 'Next: Select Papers'),
    )
  );
}

function renderStep2(content, wizardContainer) {
  const searchInput = h('input', {
    id: 'paper-search',
    type: 'text',
    className: 'input',
    placeholder: 'Search papers by title, author, or keyword...',
  });

  const papersList = h('div', { id: 'papers-list', className: 'papers-list' });
  const selectedPapers = h('div', { id: 'selected-papers', className: 'selected-papers' });
  const allPapers = h('div', { id: 'all-papers', style: 'display:none' });

  content.appendChild(
    h('div', { className: 'form-group' },
      h('label', { className: 'form-label' }, 'Search and Select Papers'),
      searchInput,
      allPapers,
    )
  );

  content.appendChild(
    h('div', { className: 'form-group' },
      h('label', { className: 'form-label' }, 'Selected Papers'),
      selectedPapers,
    )
  );

  content.appendChild(
    h('div', { className: 'wizard-actions' },
      h('button', {
        className: 'btn btn-secondary',
        onClick: () => {
          currentStep = 1;
          renderStep(wizardContainer);
        },
      }, 'Back'),
      h('button', {
        className: 'btn btn-primary',
        disabled: wizardData.paperIds.length === 0 ? '' : undefined,
        onClick: () => {
          if (wizardData.paperIds.length === 0) {
            toast('Please select at least one paper', 'error');
            return;
          }
          currentStep = 3;
          renderStep(wizardContainer);
        },
      }, 'Next: Preview'),
    )
  );

  let papersCache = [];

  async function loadPapers() {
    try {
      const data = await getPapers({ pageSize: 100 });
      papersCache = data.items;
      renderPapersList(papersCache);
    } catch (err) {
      toast('Failed to load papers', 'error');
    }
  }

  function renderPapersList(papers) {
    clear(papersList);
    clear(selectedPapers);

    for (const paper of papers) {
      const isSelected = wizardData.paperIds.includes(paper.id);
      const card = h('div', {
        className: `paper-card ${isSelected ? 'selected' : ''}`,
        onClick: () => togglePaper(paper.id, card),
      },
        h('div', { className: 'paper-card-header' },
          h('input', {
            type: 'checkbox',
            checked: isSelected ? '' : undefined,
            onClick: (e) => {
              e.stopPropagation();
              togglePaper(paper.id, card);
            },
          }),
          h('span', { className: 'paper-card-title' }, paper.title.slice(0, 80) + (paper.title.length > 80 ? '\u2026' : '')),
        ),
        h('div', { className: 'paper-card-meta' },
          `${paper.authors?.join(', ').slice(0, 50) || '\u2014'} ${paper.year ? '(' + paper.year + ')' : ''}`,
        ),
      );
      papersList.appendChild(card);
    }

    for (const paperId of wizardData.paperIds) {
      const paper = papersCache.find(p => p.id === paperId);
      if (paper) {
        const tag = h('div', { className: 'selected-paper-tag' },
          h('span', {}, paper.title.slice(0, 40) + '\u2026'),
          h('button', {
            className: 'remove-btn',
            onClick: () => {
              wizardData.paperIds = wizardData.paperIds.filter(id => id !== paperId);
              renderPapersList(papersCache);
            },
          }, '\u2715'),
        );
        selectedPapers.appendChild(tag);
      }
    }
  }

  function togglePaper(paperId, card) {
    if (wizardData.paperIds.includes(paperId)) {
      wizardData.paperIds = wizardData.paperIds.filter(id => id !== paperId);
    } else {
      wizardData.paperIds.push(paperId);
    }
    renderPapersList(papersCache);
  }

  let searchTimeout;
  searchInput.addEventListener('input', (e) => {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
      const query = e.target.value.toLowerCase();
      if (!query) {
        renderPapersList(papersCache);
        return;
      }
      const filtered = papersCache.filter(p =>
        p.title.toLowerCase().includes(query) ||
        p.authors?.some(a => a.toLowerCase().includes(query))
      );
      renderPapersList(filtered);
    }, 200);
  });

  loadPapers();
}

function renderStep3(content, wizardContainer) {
  content.appendChild(
    h('div', { className: 'preview-section' },
      h('h3', {}, 'Review Details'),
      h('div', { className: 'preview-field' },
        h('strong', {}, 'Title: '),
        h('span', {}, wizardData.title),
      ),
      h('div', { className: 'preview-field' },
        h('strong', {}, 'Research Question: '),
        h('span', {}, wizardData.researchQuestion),
      ),
      h('div', { className: 'preview-field' },
        h('strong', {}, `Selected Papers: ${wizardData.paperIds.length}`),
      ),
    )
  );

  content.appendChild(
    h('div', { className: 'wizard-actions' },
      h('button', {
        className: 'btn btn-secondary',
        onClick: () => {
          currentStep = 2;
          renderStep(wizardContainer);
        },
      }, 'Back'),
      h('button', {
        className: 'btn btn-primary',
        onClick: async () => {
          try {
            const result = await request('POST', '/api/v1/literature-reviews', {
              title: wizardData.title,
              researchQuestion: wizardData.researchQuestion,
              paperIds: wizardData.paperIds,
            });
            wizardData.reviewId = result.id;
            currentStep = 4;
            renderStep(wizardContainer);
          } catch (err) {
            toast('Failed to create literature review: ' + err.message, 'error');
          }
        },
      }, 'Generate Review'),
    )
  );
}

function renderStep4(content, wizardContainer) {
  const statusEl = h('div', { id: 'review-status', className: 'review-status' });
  content.appendChild(statusEl);

  async function checkStatus() {
    try {
      const review = await request('GET', `/api/v1/literature-reviews/${wizardData.reviewId}`);

      clear(statusEl);
      statusEl.appendChild(badge(review.status));

      if (review.status === 'Generating') {
        statusEl.appendChild(h('span', {}, ' Generating review...'));
        setTimeout(checkStatus, 2000);
      } else if (review.status === 'Completed') {
        statusEl.appendChild(h('span', {}, ' Review generated successfully!'));
        renderReviewContent(content, review);
      } else if (review.status === 'Failed') {
        statusEl.appendChild(h('span', { style: 'color:var(--c-danger)' }, ' Generation failed. Please try again.'));
        content.appendChild(
          h('div', { className: 'wizard-actions' },
            h('button', {
              className: 'btn btn-secondary',
              onClick: () => {
                wizardData = { title: '', researchQuestion: '', paperIds: [] };
                currentStep = 1;
                renderStep(wizardContainer);
              },
            }, 'Start Over'),
          )
        );
      }
    } catch (err) {
      if (err.name !== 'AbortError') {
        toast('Failed to load review', 'error');
      }
    }
  }

  checkStatus();
}

function renderReviewContent(content, review) {
  const exportBar = h('div', { className: 'export-bar' },
    h('a', {
      href: `${localStorage.getItem('ara_api_url')}/api/v1/literature-reviews/${review.id}/export/markdown`,
      target: '_blank',
      className: 'btn btn-secondary btn-sm',
    }, 'Export Markdown'),
    h('a', {
      href: `${localStorage.getItem('ara_api_url')}/api/v1/literature-reviews/${review.id}/export/pdf`,
      target: '_blank',
      className: 'btn btn-secondary btn-sm',
    }, 'Export PDF'),
  );
  content.appendChild(exportBar);

  const reviewContent = h('div', { className: 'review-content' });
  content.appendChild(reviewContent);

  reviewContent.appendChild(h('h2', {}, review.title));
  reviewContent.appendChild(h('p', { className: 'review-question' },
    h('strong', {}, 'Research Question: '),
    review.researchQuestion
  ));
  reviewContent.appendChild(h('hr', {}));

  for (const section of review.sections) {
    reviewContent.appendChild(h('h3', {}, section.heading));
    reviewContent.appendChild(h('p', {}, section.content));

    if (section.citedPaperIds?.length > 0) {
      reviewContent.appendChild(h('p', { className: 'cited-papers' },
        '*Cited papers: ' + section.citedPaperIds.map(id => `[${id}]`).join(', ')
      ));
    }
  }

  content.appendChild(
    h('div', { className: 'wizard-actions' },
      h('button', {
        className: 'btn btn-primary',
        onClick: () => {
          wizardData = { title: '', researchQuestion: '', paperIds: [] };
          currentStep = 1;
          renderStep(wizardContainer);
        },
      }, 'Create New Review'),
    )
  );
}
