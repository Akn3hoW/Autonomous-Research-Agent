import { comparePapers, compareFields, generateInsights, getAnalysisJob, identifyResearchGap } from '../api.js';
import { h, clear, loading, toast, emptyState, jsonBlock } from '../components.js';

let pollingTimer = null;

export async function render(container, { signal }) {
  clear(container);

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Analysis'),
      h('p', { className: 'page-subtitle' }, 'Compare papers and generate insights across your research library'),
    )
  );

  const tabs = h('div', { className: 'tabs' });
  const tabBtns = [
    { id: 'compare-papers', label: 'Compare Papers' },
    { id: 'compare-fields', label: 'Compare Fields' },
    { id: 'generate-insights', label: 'Generate Insights' },
    { id: 'research-gap', label: 'Research Gap' },
  ];

  let activeTab = 'compare-papers';

  tabBtns.forEach((t) => {
    const btn = h('button', {
      className: `tab ${activeTab === t.id ? 'active' : ''}`,
      onClick: () => {
        activeTab = t.id;
        tabs.querySelectorAll('.tab').forEach((tb) => {
          tb.classList.toggle('active', tb.dataset.tab === t.id);
        });
        showTab(t.id);
      },
    }, t.label);
    btn.dataset.tab = t.id;
    tabs.appendChild(btn);
  });

  container.appendChild(tabs);

  const panels = h('div', { id: 'analysis-panels' });

  const comparePapersPanel = h('div', { id: 'panel-compare-papers', className: 'analysis-panel' });
  const compareFieldsPanel = h('div', { id: 'panel-compare-fields', className: 'analysis-panel', hidden: true });
  const generateInsightsPanel = h('div', { id: 'panel-generate-insights', className: 'analysis-panel', hidden: true });
  const researchGapPanel = h('div', { id: 'panel-research-gap', className: 'analysis-panel', hidden: true });

  panels.appendChild(comparePapersPanel);
  panels.appendChild(compareFieldsPanel);
  panels.appendChild(generateInsightsPanel);
  panels.appendChild(researchGapPanel);
  container.appendChild(panels);

  const resultsSection = h('div', { id: 'analysis-results' });
  container.appendChild(resultsSection);

  renderComparePapersForm(comparePapersPanel);
  renderCompareFieldsForm(compareFieldsPanel);
  renderGenerateInsightsForm(generateInsightsPanel);
  renderResearchGapForm(researchGapPanel);

  function showTab(tabId) {
    document.querySelectorAll('.analysis-panel').forEach((p) => {
      p.hidden = p.id !== `panel-${tabId}`;
    });
    stopPolling();
  }

  function renderComparePapersForm(panel) {
    const form = h('div', { className: 'analysis-form' });

    const title = h('h2', { className: 'section-title mb-4' }, 'Compare Two Papers');
    form.appendChild(title);

    const leftId = h('input', {
      className: 'input',
      type: 'text',
      id: 'cp-left-id',
      placeholder: 'Left paper ID (e.g., 3fa85f64-5717-4562-b3fc-2c963f66afa6)',
    });

    const rightId = h('input', {
      className: 'input',
      type: 'text',
      id: 'cp-right-id',
      placeholder: 'Right paper ID (e.g., 3fa85f64-5717-4562-b3fc-2c963f66afa6)',
    });

    const submitBtn = h('button', {
      className: 'btn btn-primary',
      onClick: async () => {
        const left = leftId.value.trim();
        const right = rightId.value.trim();
        if (!left || !right) {
          toast('Please enter both paper IDs', 'error');
          return;
        }
        let leftGuid, rightGuid;
        try {
          leftGuid = UUID.parse(left);
          rightGuid = UUID.parse(right);
        } catch {
          toast('Invalid paper ID format', 'error');
          return;
        }
        submitBtn.disabled = true;
        clearResults();
        showLoading('Comparing papers\u2026');
        try {
          const result = await comparePapers({ leftPaperId: leftGuid, rightPaperId: rightGuid });
          showResults(result, 'Compare Papers');
        } catch (err) {
          if (err.name === 'AbortError') return;
          showError(err.message);
          toast(err.message, 'error');
        } finally {
          submitBtn.disabled = false;
        }
      },
    }, 'COMPARE PAPERS');

    form.appendChild(h('label', { className: 'field-label' }, 'Left Paper ID'));
    form.appendChild(leftId);
    form.appendChild(h('label', { className: 'field-label' }, 'Right Paper ID'));
    form.appendChild(rightId);
    form.appendChild(h('div', { className: 'mt-6' }, submitBtn));

    panel.appendChild(form);
  }

  function renderCompareFieldsForm(panel) {
    const form = h('div', { className: 'analysis-form' });

    const title = h('h2', { className: 'section-title mb-4' }, 'Compare Fields');
    form.appendChild(title);

    const leftFilter = h('input', {
      className: 'input',
      type: 'text',
      id: 'cf-left-filter',
      placeholder: 'Left filter (e.g., year:2024, venue:"NeurIPS")',
    });

    const rightFilter = h('input', {
      className: 'input',
      type: 'text',
      id: 'cf-right-filter',
      placeholder: 'Right filter (e.g., year:2023, venue:"ICML")',
    });

    const submitBtn = h('button', {
      className: 'btn btn-primary',
      onClick: async () => {
        const left = leftFilter.value.trim();
        const right = rightFilter.value.trim();
        if (!left || !right) {
          toast('Please enter both filters', 'error');
          return;
        }
        submitBtn.disabled = true;
        clearResults();
        showLoading('Comparing fields\u2026');
        try {
          const result = await compareFields({ leftFilter: left, rightFilter: right });
          showResults(result, 'Compare Fields');
        } catch (err) {
          if (err.name === 'AbortError') return;
          showError(err.message);
          toast(err.message, 'error');
        } finally {
          submitBtn.disabled = false;
        }
      },
    }, 'COMPARE FIELDS');

    form.appendChild(h('label', { className: 'field-label' }, 'Left Filter'));
    form.appendChild(leftFilter);
    form.appendChild(h('label', { className: 'field-label' }, 'Right Filter'));
    form.appendChild(rightFilter);
    form.appendChild(h('div', { className: 'mt-6' }, submitBtn));

    panel.appendChild(form);
  }

  function renderGenerateInsightsForm(panel) {
    const form = h('div', { className: 'analysis-form' });

    const title = h('h2', { className: 'section-title mb-4' }, 'Generate Insights');
    form.appendChild(title);

    const filter = h('input', {
      className: 'input',
      type: 'text',
      id: 'gi-filter',
      placeholder: 'Filter (e.g., year:2024, hasAbstract:true)',
    });

    const submitBtn = h('button', {
      className: 'btn btn-primary',
      onClick: async () => {
        const filterValue = filter.value.trim();
        if (!filterValue) {
          toast('Please enter a filter', 'error');
          return;
        }
        submitBtn.disabled = true;
        clearResults();
        showLoading('Generating insights\u2026');
        try {
          const job = await generateInsights({ filter: filterValue });
          startPolling(job.jobId);
        } catch (err) {
          if (err.name === 'AbortError') return;
          showError(err.message);
          toast(err.message, 'error');
          submitBtn.disabled = false;
        }
      },
    }, 'GENERATE INSIGHTS');

    form.appendChild(h('label', { className: 'field-label' }, 'Filter'));
    form.appendChild(filter);
    form.appendChild(h('div', { className: 'mt-6' }, submitBtn));

    panel.appendChild(form);
  }

  function renderResearchGapForm(panel) {
    const form = h('div', { className: 'analysis-form' });

    const title = h('h2', { className: 'section-title mb-4' }, 'Identify Research Gaps');
    form.appendChild(title);

    const topic = h('input', {
      className: 'input',
      type: 'text',
      id: 'rg-topic',
      placeholder: 'Topic or research area (e.g., "machine learning in healthcare")',
    });

    const submitBtn = h('button', {
      className: 'btn btn-primary',
      onClick: async () => {
        const topicValue = topic.value.trim();
        if (!topicValue) {
          toast('Please enter a topic', 'error');
          return;
        }
        submitBtn.disabled = true;
        clearResults();
        showLoading('Analyzing research gaps\u2026');
        try {
          const result = await identifyResearchGap({ topic: topicValue });
          showGapReport(result);
        } catch (err) {
          if (err.name === 'AbortError') return;
          showError(err.message);
          toast(err.message, 'error');
        } finally {
          submitBtn.disabled = false;
        }
      },
    }, 'IDENTIFY GAPS');

    form.appendChild(h('label', { className: 'field-label' }, 'Topic'));
    form.appendChild(topic);
    form.appendChild(h('div', { className: 'mt-6' }, submitBtn));

    panel.appendChild(form);
  }

  function showGapReport(result) {
    stopPolling();
    const rs = document.getElementById('analysis-results');
    clear(rs);

    const content = h('div', { className: 'section' });
    content.appendChild(h('div', { className: 'section-header' },
      h('h2', { className: 'section-title' }, 'Research Gap Report'),
    ));

    if (result.gapAnalysis) {
      const gapSection = h('div', { className: 'mb-6' });
      gapSection.appendChild(h('h3', { className: 'section-title mb-2' }, 'Understudied Angles'));
      gapSection.appendChild(jsonBlock(result.gapAnalysis));
      content.appendChild(gapSection);
    }

    if (result.suggestedQueries) {
      const queriesSection = h('div', { className: 'mb-6' });
      queriesSection.appendChild(h('h3', { className: 'section-title mb-2' }, 'Suggested Queries'));
      queriesSection.appendChild(jsonBlock(result.suggestedQueries));
      content.appendChild(queriesSection);
    }

    if (result.corpusCoverage) {
      const corpusSection = h('div', { className: 'mb-6' });
      corpusSection.appendChild(h('h3', { className: 'section-title mb-2' }, 'Corpus Coverage'));
      corpusSection.appendChild(jsonBlock(result.corpusCoverage));
      content.appendChild(corpusSection);
    }

    if (result.externalCoverage) {
      const externalSection = h('div', { className: 'mb-6' });
      externalSection.appendChild(h('h3', { className: 'section-title mb-2' }, 'External Coverage (Semantic Scholar)'));
      externalSection.appendChild(jsonBlock(result.externalCoverage));
      content.appendChild(externalSection);
    }

    const meta = h('div', { className: 'detail-meta', style: 'margin-top:var(--s-6)' });
    const metaItems = [
      ['Report ID', result.id],
      ['Topic', result.topic],
      ['Created', new Date(result.createdAt).toLocaleString()],
    ];
    if (result.createdBy) metaItems.push(['Created By', result.createdBy]);
    for (const [label, value] of metaItems) {
      meta.appendChild(
        h('div', { className: 'meta-item' },
          h('span', { className: 'meta-label' }, label),
          h('span', { className: 'meta-value cell-mono' }, String(value)),
        )
      );
    }
    content.appendChild(meta);
    rs.appendChild(content);
  }

  function showLoading(text) {
    const rs = document.getElementById('analysis-results');
    clear(rs);
    rs.appendChild(
      h('div', { className: 'section' },
        h('div', { className: 'section-header' },
          h('h2', { className: 'section-title' }, 'Results'),
        ),
        h('div', { className: 'loading' },
          h('div', { className: 'spinner' }),
          h('span', { className: 'loading-text' }, text),
        ),
      )
    );
  }

  function showError(message) {
    const rs = document.getElementById('analysis-results');
    clear(rs);
    rs.appendChild(
      h('div', { className: 'section' },
        h('div', { className: 'section-header' },
          h('h2', { className: 'section-title' }, 'Results'),
        ),
        emptyState('Error', message),
      )
    );
  }

  function showResults(result, type) {
    stopPolling();
    const rs = document.getElementById('analysis-results');
    clear(rs);

    const header = h('div', { className: 'section-header' },
      h('h2', { className: 'section-title' }, type + ' Results'),
    );

    const resultSection = h('div', { className: 'section' });
    resultSection.appendChild(header);

    if (result.result) {
      resultSection.appendChild(jsonBlock(result.result));
    } else {
      resultSection.appendChild(emptyState('No results', 'The analysis returned empty results'));
    }

    const meta = h('div', { className: 'detail-meta', style: 'margin-top:var(--s-6)' });
    const metaItems = [
      ['Analysis ID', result.id],
      ['Type', result.analysisType],
      ['Created', new Date(result.createdAt).toLocaleString()],
    ];
    if (result.createdBy) metaItems.push(['Created By', result.createdBy]);
    for (const [label, value] of metaItems) {
      meta.appendChild(
        h('div', { className: 'meta-item' },
          h('span', { className: 'meta-label' }, label),
          h('span', { className: 'meta-value cell-mono' }, String(value)),
        )
      );
    }
    resultSection.appendChild(meta);

    rs.appendChild(resultSection);
  }

  function showJobStatus(job) {
    const rs = document.getElementById('analysis-results');
    clear(rs);

    const statusBadge = h('span', {
      className: `badge badge-${job.status === 'Completed' ? 'green' : job.status === 'Failed' ? 'red' : job.status === 'Running' ? 'blue' : 'gray'}`,
    }, job.status);

    const header = h('div', { className: 'section-header' },
      h('h2', { className: 'section-title' }, 'Generate Insights \u2014 Job Status'),
    );

    const content = h('div', { className: 'section' });
    content.appendChild(header);

    const meta = h('div', { className: 'detail-meta' });
    const metaItems = [
      ['Job ID', job.jobId],
      ['Status', ''],
    ];
    const statusRow = h('div', { className: 'meta-item' });
    statusRow.appendChild(h('span', { className: 'meta-label' }, 'Status'));
    statusRow.appendChild(statusBadge);
    meta.appendChild(statusRow);

    if (job.errorMessage) {
      meta.appendChild(
        h('div', { className: 'meta-item' },
          h('span', { className: 'meta-label' }, 'Error'),
          h('span', { className: 'meta-value', style: 'color:var(--c-red)' }, job.errorMessage),
        )
      );
    }
    content.appendChild(meta);

    if (job.status === 'Completed' && job.result) {
      content.appendChild(h('div', { className: 'section-header', style: 'margin-top:var(--s-6)' },
        h('h2', { className: 'section-title' }, 'Results'),
      ));
      content.appendChild(jsonBlock(job.result));
    } else if (job.status !== 'Completed' && job.status !== 'Failed') {
      content.appendChild(
        h('p', { className: 'text-secondary', style: 'margin-top:var(--s-4)' },
          'Waiting for job to complete\u2026',
        )
      );
    }

    rs.appendChild(content);
  }

  function clearResults() {
    stopPolling();
    const rs = document.getElementById('analysis-results');
    clear(rs);
  }

  function startPolling(jobId) {
    stopPolling();
    pollingTimer = setInterval(async () => {
      const rs = document.getElementById('analysis-results');
      if (!rs || !document.getElementById('panel-generate-insights')) {
        stopPolling();
        return;
      }
      try {
        const job = await getAnalysisJob(jobId);
        showJobStatus(job);
        if (job.status === 'Completed' || job.status === 'Failed') {
          stopPolling();
          document.querySelector('#panel-generate-insights .btn-primary').disabled = false;
        }
      } catch (err) {
        if (err.name === 'AbortError') return;
        stopPolling();
      }
    }, 3000);
  }

  function stopPolling() {
    if (pollingTimer) {
      clearInterval(pollingTimer);
      pollingTimer = null;
    }
  }
}

const UUID = {
  pattern: /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
  parse(str) {
    if (!this.pattern.test(str)) throw new Error('Invalid UUID');
    return str;
  },
};
