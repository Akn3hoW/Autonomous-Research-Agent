import { getTrends } from '../api.js';
import { h, clear, loading, toast, emptyState } from '../components.js';
import { store } from '../store.js';

let chartInstance = null;
let topicEvolutionChart = null;

export async function render(container, { signal }) {
  clear(container);

  loadChartLibrary();
  loadD3Library();

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Trend Analysis'),
      h('p', { className: 'page-subtitle' }, 'Research trend analysis across years'),
    )
  );

  const filterRow = h('div', { className: 'filter-row', style: 'margin-bottom: var(--s-6)' });

  const fieldInput = h('input', {
    className: 'input',
    type: 'text',
    id: 'trend-field',
    placeholder: 'Research field (e.g., machine learning)',
    style: 'flex: 1'
  });

  const startYearInput = h('input', {
    className: 'input',
    type: 'number',
    id: 'trend-start-year',
    placeholder: 'Start Year',
    style: 'width: 120px'
  });

  const endYearInput = h('input', {
    className: 'input',
    type: 'number',
    id: 'trend-end-year',
    placeholder: 'End Year',
    style: 'width: 120px'
  });

  const loadBtn = h('button', {
    className: 'btn btn-primary',
    onClick: () => loadTrends(),
  }, 'LOAD TRENDS');

  filterRow.appendChild(fieldInput);
  filterRow.appendChild(startYearInput);
  filterRow.appendChild(endYearInput);
  filterRow.appendChild(loadBtn);
  container.appendChild(filterRow);

  const chartContainer = h('div', { className: 'section' },
    h('div', { className: 'section-header' },
      h('h2', { className: 'section-title' }, 'Topic Trends Over Time')
    ),
    h('div', { style: 'position: relative; height: 400px;' },
      h('canvas', { id: 'trend-chart' })
    )
  );
  container.appendChild(chartContainer);

  const citationChartContainer = h('div', { className: 'section' },
    h('div', { className: 'section-header' },
      h('h2', { className: 'section-title' }, 'Citation Trends Over Time')
    ),
    h('div', { style: 'position: relative; height: 350px;' },
      h('canvas', { id: 'citation-trend-chart' })
    )
  );
  container.appendChild(citationChartContainer);

  const topicEvolutionContainer = h('div', { className: 'section' },
    h('div', { className: 'section-header' },
      h('h2', { className: 'section-title' }, 'Topic Evolution')
    ),
    h('div', { id: 'topic-evolution-chart', style: 'height: 400px;' })
  );
  container.appendChild(topicEvolutionContainer);

  const themesContainer = h('div', { className: 'section', id: 'themes-container' });
  container.appendChild(themesContainer);

  loadTrends();

  async function loadTrends() {
    const field = fieldInput.value.trim() || undefined;
    const startYear = parseInt(startYearInput.value) || undefined;
    const endYear = parseInt(endYearInput.value) || undefined;

    const loadingEl = h('div', { className: 'loading' },
      h('div', { className: 'spinner' }),
      h('span', { className: 'loading-text' }, 'Analyzing trends\u2026'),
    );
    chartContainer.appendChild(loadingEl);

    try {
      const data = await getTrends({ field, startYear, endYear }, signal);
      store.setSlice('papers', { trendData: data });
      renderTrends(data);
    } catch (err) {
      if (err.name === 'AbortError') return;
      toast(err.message, 'error');
      renderError(err.message);
    }
  }

  function renderTrends(data) {
    const existingLoading = chartContainer.querySelector('.loading');
    if (existingLoading) existingLoading.remove();

    if (!data.buckets || data.buckets.length === 0) {
      chartContainer.appendChild(emptyState('No Data', 'No trend data available for the selected range.'));
      return;
    }

    renderChart(data);
    renderCitationTrend(data);
    renderTopicEvolution(data);
    renderThemes(data);
  }

  function renderChart(data) {
    const canvas = document.getElementById('trend-chart');
    if (!canvas || typeof Chart === 'undefined') return;

    if (chartInstance) {
      chartInstance.destroy();
    }

    const allTopics = new Map();
    data.buckets.forEach(bucket => {
      bucket.topics.forEach(topic => {
        if (!allTopics.has(topic.topic)) {
          allTopics.set(topic.topic, []);
        }
      });
    });

    const labels = data.buckets.map(b => b.year.toString());

    const datasets = [];
    const topicColors = [
      '#4f46e5', '#06b6d4', '#10b981', '#f59e0b', '#ef4444',
      '#8b5cf6', '#ec4899', '#14b8a6', '#f97316', '#6366f1'
    ];

    let colorIndex = 0;
    for (const [topicName] of allTopics) {
      const color = topicColors[colorIndex % topicColors.length];
      const values = data.buckets.map(bucket => {
        const found = bucket.topics.find(t => t.topic === topicName);
        return found ? found.paperCount : 0;
      });

      datasets.push({
        label: topicName,
        data: values,
        borderColor: color,
        backgroundColor: color + '20',
        tension: 0.3,
        fill: false
      });
      colorIndex++;
    }

    chartInstance = new Chart(canvas, {
      type: 'line',
      data: { labels, datasets },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: {
          mode: 'index',
          intersect: false,
        },
        plugins: {
          legend: {
            position: 'bottom',
            labels: { boxWidth: 12, padding: 8 }
          },
          tooltip: {
            callbacks: {
              label: ctx => `${ctx.dataset.label}: ${ctx.raw} papers`
            }
          }
        },
        scales: {
          x: { title: { display: true, text: 'Year' } },
          y: { title: { display: true, text: 'Paper Count' }, beginAtZero: true }
        }
      }
    });
  }

  function renderCitationTrend(data) {
    const canvas = document.getElementById('citation-trend-chart');
    if (!canvas || typeof Chart === 'undefined') return;

    if (data.citationTrend) {
      const citationData = data.citationTrend;
      const labels = citationData.years.map(y => y.toString());

      const citationsDataset = {
        label: 'Total Citations',
        data: citationData.citations,
        borderColor: '#10b981',
        backgroundColor: 'rgba(16, 185, 129, 0.1)',
        fill: true,
        tension: 0.4,
      };

      const avgCitationsDataset = {
        label: 'Avg Citations Per Paper',
        data: citationData.avgCitations,
        borderColor: '#f59e0b',
        backgroundColor: 'rgba(245, 158, 11, 0.1)',
        fill: true,
        tension: 0.4,
      };

      new Chart(canvas, {
        type: 'line',
        data: { labels, datasets: [citationsDataset, avgCitationsDataset] },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: {
              position: 'bottom',
              labels: { boxWidth: 12, padding: 8 }
            },
            tooltip: {
              callbacks: {
                label: ctx => `${ctx.dataset.label}: ${ctx.raw}`
              }
            }
          },
          scales: {
            x: { title: { display: true, text: 'Year' } },
            y: { title: { display: true, text: 'Citations' }, beginAtZero: true }
          }
        }
      });
    }
  }

  function renderTopicEvolution(data) {
    const container = document.getElementById('topic-evolution-chart');
    if (!container || typeof d3 === 'undefined') return;

    clear(container);

    if (!data.buckets || data.buckets.length < 2) return;

    const width = container.clientWidth || 800;
    const height = 400;

    const svg = d3.select(container)
      .append('svg')
      .attr('width', width)
      .attr('height', height)
      .attr('viewBox', `0 0 ${width} ${height}`);

    const years = data.buckets.map(b => b.year);
    const allTopics = [];
    data.buckets.forEach(bucket => {
      bucket.topics.forEach(t => {
        if (!allTopics.find(at => at.topic === t.topic)) {
          allTopics.push({ topic: t.topic, papers: [] });
        }
      });
    });

    allTopics.forEach(t => {
      t.papers = data.buckets.map(bucket => {
        const found = bucket.topics.find(bt => bt.topic === t.topic);
        return found ? found.paperCount : 0;
      });
    });

    const x = d3.scaleLinear()
      .domain([0, years.length - 1])
      .range([50, width - 50]);

    const y = d3.scaleLinear()
      .domain([0, d3.max(allTopics, t => d3.max(t.papers))])
      .range([height - 50, 50]);

    const line = d3.line()
      .x((d, i) => x(i))
      .y(d => y(d))
      .curve(d3.curveMonotoneX);

    const topicColors = [
      '#4f46e5', '#06b6d4', '#10b981', '#f59e0b', '#ef4444',
      '#8b5cf6', '#ec4899', '#14b8a6', '#f97316', '#6366f1'
    ];

    allTopics.slice(0, 8).forEach((topic, idx) => {
      const color = topicColors[idx % topicColors.length];

      svg.append('path')
        .datum(topic.papers)
        .attr('fill', 'none')
        .attr('stroke', color)
        .attr('stroke-width', 2)
        .attr('d', line);

      svg.selectAll(`.dot-${idx}`)
        .data(topic.papers)
        .enter()
        .append('circle')
        .attr('class', `dot-${idx}`)
        .attr('cx', (d, i) => x(i))
        .attr('cy', d => y(d))
        .attr('r', 4)
        .attr('fill', color)
        .attr('opacity', 0.7)
        .append('title')
        .text(`${topic.topic}: ${topic.papers}`);
    });

    svg.selectAll('.year-label')
      .data(years)
      .enter()
      .append('text')
      .attr('class', 'year-label')
      .attr('x', (d, i) => x(i))
      .attr('y', height - 20)
      .attr('text-anchor', 'middle')
      .attr('fill', '#666')
      .attr('font-size', '12px')
      .text(d => d);
  }

  function renderThemes(data) {
    clear(themesContainer);

    themesContainer.appendChild(
      h('div', { className: 'section-header' },
        h('h2', { className: 'section-title' }, 'Emerging & Declining Themes')
      )
    );

    const themesRow = h('div', { className: 'themes-grid', style: 'display: grid; grid-template-columns: 1fr 1fr; gap: var(--s-4); margin-top: var(--s-4);' });

    const emergingDiv = h('div', { className: 'theme-card', style: 'padding: var(--s-4); border: 1px solid var(--c-green); border-radius: var(--radius);' });
    emergingDiv.appendChild(h('h3', { style: 'color: var(--c-green); margin-bottom: var(--s-3);' }, '\u2191 Emerging Themes'));
    if (data.emergingThemes && data.emergingThemes.length > 0) {
      const list = h('ul', { style: 'list-style: none; padding: 0; margin: 0;' });
      data.emergingThemes.forEach(theme => {
        list.appendChild(h('li', { style: 'padding: 4px 0; border-bottom: 1px solid var(--c-border);' }, theme));
      });
      emergingDiv.appendChild(list);
    } else {
      emergingDiv.appendChild(h('p', { style: 'color: var(--c-text-secondary);' }, 'No emerging themes detected.'));
    }

    const decliningDiv = h('div', { className: 'theme-card', style: 'padding: var(--s-4); border: 1px solid var(--c-red); border-radius: var(--radius);' });
    decliningDiv.appendChild(h('h3', { style: 'color: var(--c-red); margin-bottom: var(--s-3);' }, '\u2193 Declining Themes'));
    if (data.decliningThemes && data.decliningThemes.length > 0) {
      const list = h('ul', { style: 'list-style: none; padding: 0; margin: 0;' });
      data.decliningThemes.forEach(theme => {
        list.appendChild(h('li', { style: 'padding: 4px 0; border-bottom: 1px solid var(--c-border);' }, theme));
      });
      decliningDiv.appendChild(list);
    } else {
      decliningDiv.appendChild(h('p', { style: 'color: var(--c-text-secondary);' }, 'No declining themes detected.'));
    }

    themesRow.appendChild(emergingDiv);
    themesRow.appendChild(decliningDiv);
    themesContainer.appendChild(themesRow);
  }

  function renderError(message) {
    const existingLoading = chartContainer.querySelector('.loading');
    if (existingLoading) existingLoading.remove();
    chartContainer.appendChild(emptyState('Error', message));
  }
}

function loadChartLibrary() {
  if (document.querySelector('script[src*="chart.js"]')) return;
  const script = document.createElement('script');
  script.src = 'https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js';
  document.head.appendChild(script);
}

function loadD3Library() {
  if (document.querySelector('script[src*="d3"]')) return;
  const script = document.createElement('script');
  script.src = 'https://cdn.jsdelivr.net/npm/d3@7.8.5/dist/d3.min.js';
  document.head.appendChild(script);
}

export const init = () => {};
export const cleanup = () => {
  if (chartInstance) {
    chartInstance.destroy();
    chartInstance = null;
  }
  if (topicEvolutionChart) {
    topicEvolutionChart.destroy();
    topicEvolutionChart = null;
  }
};