import { createImportJob, importPapers } from '../api.js';
import { h, clear, loading, badge, toast, emptyState, formatAuthors } from '../components.js';

export async function render(container, { navigate }) {
  clear(container);

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Import Papers'),
      h('p', { className: 'page-subtitle' }, 'Search and import papers from Semantic Scholar'),
    )
  );

  const form = h('div', { className: 'import-form' });

  // Query inputs
  form.appendChild(h('label', { className: 'field-label' }, 'Search Queries'));
  form.appendChild(h('p', { className: 'field-hint mb-4' }, 'Add one or more search queries to find papers'));

  const queryList = h('div', { className: 'query-list', id: 'query-list' });
  addQueryRow(queryList, '');
  form.appendChild(queryList);

  form.appendChild(
    h('button', {
      className: 'btn btn-ghost btn-sm mt-4',
      onClick: () => addQueryRow(document.getElementById('query-list'), ''),
    }, '+ ADD QUERY')
  );

  // Limit
  form.appendChild(h('label', { className: 'field-label' }, 'Results per query'));
  const limitInput = h('input', {
    className: 'input',
    type: 'number',
    value: '10',
    min: '1',
    max: '100',
    style: 'max-width:120px',
  });
  form.appendChild(limitInput);
  form.appendChild(h('p', { className: 'field-hint' }, 'Maximum papers to return per query (1\u2013100)'));

  // Store imported
  const checkbox = h('label', { className: 'checkbox-wrap mt-6' });
  const storeCheck = h('input', { type: 'checkbox', checked: '' });
  storeCheck.checked = true;
  checkbox.appendChild(storeCheck);
  checkbox.appendChild(h('span', { className: 'checkbox-label' }, 'Save imported papers to library'));
  form.appendChild(checkbox);

  // Mode selection
  form.appendChild(h('label', { className: 'field-label' }, 'Import Mode'));

  const modeBar = h('div', { className: 'search-mode-bar mb-4' });
  let selectedMode = 'direct';

  const directBtn = h('button', {
    className: 'search-mode-btn active',
    dataset: { mode: 'direct' },
    onClick: () => setMode('direct'),
  }, 'Direct Import');

  const jobBtn = h('button', {
    className: 'search-mode-btn',
    dataset: { mode: 'job' },
    onClick: () => setMode('job'),
  }, 'Background Job');

  modeBar.append(directBtn, jobBtn);
  form.appendChild(modeBar);

  form.appendChild(h('p', { className: 'field-hint mb-6' },
    'Direct import returns results immediately. Background job runs asynchronously.'
  ));

  function setMode(mode) {
    selectedMode = mode;
    modeBar.querySelectorAll('.search-mode-btn').forEach(b => {
      b.classList.toggle('active', b.dataset.mode === mode);
    });
  }

  // Submit
  const submitBtn = h('button', {
    className: 'btn btn-primary',
    onClick: handleSubmit,
  }, 'IMPORT PAPERS');
  form.appendChild(h('div', { className: 'mt-8' }, submitBtn));

  container.appendChild(form);

  // Results area
  const resultsArea = h('div', { id: 'import-results', className: 'mt-8' });
  container.appendChild(resultsArea);

  async function handleSubmit() {
    const ql = document.getElementById('query-list');
    const queries = Array.from(ql.querySelectorAll('input'))
      .map(i => i.value.trim())
      .filter(Boolean);

    if (queries.length === 0) {
      toast('Enter at least one search query', 'warning');
      return;
    }

    const limit = parseInt(limitInput.value) || 10;
    const storeImportedPapers = storeCheck.checked;

    submitBtn.disabled = true;
    submitBtn.textContent = 'IMPORTING\u2026';

    const resultsDiv = document.getElementById('import-results');
    clear(resultsDiv);
    resultsDiv.appendChild(loading('Importing papers'));

    try {
      if (selectedMode === 'job') {
        const job = await createImportJob({ queries, limit, storeImportedPapers });
        clear(resultsDiv);
        toast('Import job created', 'success');
        resultsDiv.appendChild(
          h('div', { className: 'section' },
            h('div', { className: 'section-header' },
              h('h2', { className: 'section-title' }, 'Job Created'),
            ),
            h('div', { className: 'flex items-center gap-4 mb-4' },
              badge(job.status),
              h('span', { className: 'cell-mono text-secondary' }, job.id),
            ),
            h('button', {
              className: 'btn btn-secondary btn-sm',
              onClick: () => navigate(`/jobs/${job.id}`),
            }, 'VIEW JOB'),
          )
        );
      } else {
        const result = await importPapers({ queries, limit, storeImportedPapers });
        clear(resultsDiv);
        toast(`Imported ${result.importedCount} papers`, 'success');

        resultsDiv.appendChild(
          h('div', { className: 'section' },
            h('div', { className: 'section-header' },
              h('h2', { className: 'section-title' }, `Imported ${result.importedCount} Papers`),
            ),
          )
        );

        if (result.papers?.length) {
          const table = h('table', { className: 'table' });
          table.appendChild(
            h('thead', {},
              h('tr', {},
                h('th', {}, 'Title'),
                h('th', {}, 'Authors'),
                h('th', {}, 'Year'),
                h('th', { className: 'cell-num' }, 'Citations'),
              )
            )
          );
          const tbody = h('tbody');
          for (const paper of result.papers) {
            tbody.appendChild(
              h('tr', {
                className: 'clickable',
                onClick: () => navigate(`/papers/${paper.id}`),
              },
                h('td', {},
                  h('div', { className: 'cell-title truncate', style: 'max-width:360px' }, paper.title),
                ),
                h('td', { className: 'text-secondary' }, formatAuthors(paper.authors, 2)),
                h('td', {}, paper.year ? String(paper.year) : '\u2014'),
                h('td', { className: 'cell-num' }, String(paper.citationCount)),
              )
            );
          }
          table.appendChild(tbody);
          resultsDiv.appendChild(h('div', { className: 'table-wrap' }, table));
        }
      }
    } catch (err) {
      clear(resultsDiv);
      if (err.name === 'AbortError') return;
      resultsDiv.appendChild(emptyState('Import failed', err.message));
      toast(err.message, 'error');
    } finally {
      submitBtn.disabled = false;
      submitBtn.textContent = 'IMPORT PAPERS';
    }
  }
}

function addQueryRow(container, value) {
  const row = h('div', { className: 'query-item' });
  const input = h('input', {
    className: 'input input-sm',
    type: 'text',
    placeholder: 'e.g. "transformer attention mechanism"',
    value,
  });
  const removeBtn = h('button', {
    className: 'btn-remove',
    onClick: () => {
      if (container.children.length > 1) row.remove();
    },
  }, '\u00D7');
  row.append(input, removeBtn);
  container.appendChild(row);
  requestAnimationFrame(() => input.focus());
}
