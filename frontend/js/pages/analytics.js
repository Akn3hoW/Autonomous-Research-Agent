import { request, ApiError } from '../api.js';
import { h, clear, loading, emptyState, toast } from '../components.js';

export async function render(container, { signal }) {
  clear(container);
  container.appendChild(loading('Loading analytics...'));

  try {
    const data = await request('GET', '/api/v1/admin/analytics', null, { signal });
    clear(container);
    renderAnalytics(container, data);
  } catch (err) {
    clear(container);
    if (err.name === 'AbortError') return;
    if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
      container.appendChild(emptyState('Access Denied', 'You do not have permission to view analytics. Please contact an administrator.'));
    } else {
      container.appendChild(emptyState('Error loading analytics', err.message));
      toast(err.message, 'error');
    }
  }
}

function renderAnalytics(container, data) {
  const header = h('div', { className: 'page-header' },
    h('h1', { className: 'page-title' }, 'Analytics'),
    h('p', { className: 'page-subtitle' }, 'System overview and corpus statistics'),
    h('div', { className: 'stat-value', style: 'font-size:2rem;margin-top:0.5rem' },
      data.totalPapers.toLocaleString(), h('span', { className: 'text-secondary', style: 'font-size:1rem' }, ' total papers')
    )
  );
  container.appendChild(header);

  const metricsRow = h('div', { className: 'metrics-grid' },
    metricCard('Avg Processing Time', `${data.averageProcessingTimeMs.toLocaleString()}ms`),
  );
  container.appendChild(metricsRow);

  const chartsGrid = h('div', { className: 'charts-grid' });

  chartsGrid.appendChild(chartCard('Papers Added Over Time', renderBarChart('papers-over-time', data.papersOverTime)));
  chartsGrid.appendChild(chartCard('Papers by Source', renderPieChart('papers-by-source', data.papersBySource)));
  chartsGrid.appendChild(chartCard('Papers by Status', renderHorizontalBarChart('papers-by-status', data.papersByStatus)));
  chartsGrid.appendChild(chartCard('Job Throughput', renderLineChart('job-throughput', data.jobThroughput)));

  container.appendChild(chartsGrid);

  const venueSection = h('div', { className: 'section' },
    h('div', { className: 'section-header' },
      h('h2', { className: 'section-title' }, 'Top Venues'),
    ),
    renderVenueTable(data.papersByVenue)
  );
  container.appendChild(venueSection);

  const keywordsSection = h('div', { className: 'section' },
    h('div', { className: 'section-header' },
      h('h2', { className: 'section-title' }, 'Top Keywords'),
    ),
    renderKeywordsTable(data.topKeywords)
  );
  container.appendChild(keywordsSection);
}

function metricCard(label, value) {
  return h('div', { className: 'metric-card' },
    h('div', { className: 'metric-value' }, value),
    h('div', { className: 'metric-label' }, label)
  );
}

function chartCard(title, content) {
  return h('div', { className: 'chart-card' },
    h('div', { className: 'chart-title' }, title),
    content
  );
}

function renderBarChart(id, data) {
  if (!data || data.length === 0) {
    return h('div', { className: 'chart-empty' }, 'No data available');
  }
  const width = 600, height = 200, padding = { top: 10, right: 10, bottom: 30, left: 40 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;

  const maxVal = Math.max(...data.map(d => d.count), 1);
  const barWidth = chartWidth / data.length;
  const barGap = Math.max(2, barWidth * 0.15);
  const actualBarWidth = barWidth - barGap;

  let bars = '';
  for (let i = 0; i < data.length; i++) {
    const d = data[i];
    const barHeight = (d.count / maxVal) * chartHeight;
    const x = padding.left + i * barWidth;
    const y = padding.top + chartHeight - barHeight;
    bars += `<rect x="${x}" y="${y}" width="${actualBarWidth}" height="${barHeight}" fill="var(--c-accent)" rx="2"/>`;
    if (data.length <= 12) {
      const labelX = x + actualBarWidth / 2;
      const labelY = height - 5;
      bars += `<text x="${labelX}" y="${labelY}" text-anchor="middle" class="chart-label">${d.month.slice(5)}</text>`;
    }
  }

  return h('div', { className: 'chart-container' },
    h('svg', {
      viewBox: `0 0 ${width} ${height}`,
      className: 'bar-chart',
      preserveAspectRatio: 'xMidYMid meet'
    },
      h('g', { innerHTML: bars })
    )
  );
}

function renderPieChart(id, data) {
  if (!data || data.length === 0) {
    return h('div', { className: 'chart-empty' }, 'No data available');
  }
  const colors = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899', '#06b6d4', '#84cc16'];
  const total = data.reduce((s, d) => s + d.count, 0);
  const cx = 100, cy = 100, r = 80;

  let paths = '';
  let startAngle = -90;
  for (let i = 0; i < data.length; i++) {
    const d = data[i];
    const angle = (d.count / total) * 360;
    const endAngle = startAngle + angle;
    const largeArc = angle > 180 ? 1 : 0;

    const x1 = cx + r * Math.cos((startAngle * Math.PI) / 180);
    const y1 = cy + r * Math.sin((startAngle * Math.PI) / 180);
    const x2 = cx + r * Math.cos((endAngle * Math.PI) / 180);
    const y2 = cy + r * Math.sin((endAngle * Math.PI) / 180);

    const path = `M ${cx} ${cy} L ${x1} ${y1} A ${r} ${r} 0 ${largeArc} 1 ${x2} ${y2} Z`;
    paths += `<path d="${path}" fill="${colors[i % colors.length]}" stroke="var(--c-bg)" stroke-width="2"/>`;

    if (angle > 10) {
      const midAngle = (startAngle + endAngle) / 2;
      const labelR = r * 0.65;
      const lx = cx + labelR * Math.cos((midAngle * Math.PI) / 180);
      const ly = cy + labelR * Math.sin((midAngle * Math.PI) / 180);
      paths += `<text x="${lx}" y="${ly}" text-anchor="middle" dominant-baseline="middle" class="chart-label" fill="white" font-size="11">${d.count}</text>`;
    }

    startAngle = endAngle;
  }

  const legend = h('div', { className: 'pie-legend' });
  for (let i = 0; i < data.length; i++) {
    const d = data[i];
    legend.appendChild(h('div', { className: 'legend-item' },
      h('span', { className: 'legend-color', style: `background:${colors[i % colors.length]}` }),
      h('span', { className: 'legend-label' }, d.source),
      h('span', { className: 'legend-value' }, ` ${d.count}`)
    ));
  }

  return h('div', { className: 'chart-container' },
    h('svg', {
      viewBox: '0 0 200 200',
      className: 'pie-chart',
      preserveAspectRatio: 'xMidYMid meet'
    },
      h('g', { innerHTML: paths })
    ),
    legend
  );
}

function renderHorizontalBarChart(id, data) {
  if (!data || data.length === 0) {
    return h('div', { className: 'chart-empty' }, 'No data available');
  }
  const width = 500, height = 200, padding = { top: 10, right: 60, bottom: 10, left: 80 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;

  const maxVal = Math.max(...data.map(d => d.count), 1);
  const rowHeight = chartHeight / data.length;
  const barHeight = rowHeight * 0.6;
  const barGap = (chartHeight - data.length * barHeight) / (data.length + 1);

  let bars = '';
  for (let i = 0; i < data.length; i++) {
    const d = data[i];
    const barWidth = (d.count / maxVal) * chartWidth;
    const y = padding.top + barGap + i * (barHeight + barGap);
    const x = padding.left;

    bars += `<rect x="${x}" y="${y}" width="${barWidth}" height="${barHeight}" fill="var(--c-accent)" rx="2"/>`;
    bars += `<text x="${x - 5}" y="${y + barHeight / 2}" text-anchor="end" dominant-baseline="middle" class="chart-label">${d.status}</text>`;
    bars += `<text x="${x + barWidth + 5}" y="${y + barHeight / 2}" text-anchor="start" dominant-baseline="middle" class="chart-label">${d.count}</text>`;
  }

  return h('div', { className: 'chart-container' },
    h('svg', {
      viewBox: `0 0 ${width} ${height}`,
      className: 'hbar-chart',
      preserveAspectRatio: 'xMidYMid meet'
    },
      h('g', { innerHTML: bars })
    )
  );
}

function renderLineChart(id, data) {
  if (!data || data.length === 0) {
    return h('div', { className: 'chart-empty' }, 'No data available');
  }

  const completedData = data.map(d => ({ month: d.month, value: d.completed }));
  const failedData = data.map(d => ({ month: d.month, value: d.failed }));

  const width = 600, height = 200, padding = { top: 10, right: 10, bottom: 30, left: 40 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;

  const allValues = [...completedData, ...failedData].map(d => d.value);
  const maxVal = Math.max(...allValues, 1);

  function buildLinePoints(series) {
    return series.map((d, i) => {
      const x = padding.left + (i / (data.length - 1)) * chartWidth;
      const y = padding.top + chartHeight - (d.value / maxVal) * chartHeight;
      return `${x},${y}`;
    }).join(' ');
  }

  function buildAreaPoints(series) {
    const points = series.map((d, i) => {
      const x = padding.left + (i / (data.length - 1)) * chartWidth;
      const y = padding.top + chartHeight - (d.value / maxVal) * chartHeight;
      return `${x},${y}`;
    });
    const baseline = padding.top + chartHeight;
    return `${padding.left},${baseline} ${points.join(' ')} ${padding.left + chartWidth},${baseline}`;
  }

  const completedArea = buildAreaPoints(completedData);
  const failedArea = buildAreaPoints(failedData);
  const completedPoints = buildLinePoints(completedData);
  const failedPoints = buildLinePoints(failedData);

  let labels = '';
  for (let i = 0; i < data.length; i++) {
    const labelX = padding.left + (i / (data.length - 1)) * chartWidth;
    labels += `<text x="${labelX}" y="${height - 5}" text-anchor="middle" class="chart-label">${data[i].month.slice(5)}</text>`;
  }

  return h('div', { className: 'chart-container' },
    h('div', { className: 'line-legend' },
      h('div', { className: 'legend-item' },
        h('span', { className: 'legend-color', style: 'background:var(--c-green)' }),
        h('span', { className: 'legend-label' }, 'Completed')
      ),
      h('div', { className: 'legend-item' },
        h('span', { className: 'legend-color', style: 'background:var(--c-red)' }),
        h('span', { className: 'legend-label' }, 'Failed')
      ),
      h('div', { className: 'legend-item' },
        h('span', { className: 'legend-color', style: 'background:var(--c-blue)' }),
        h('span', { className: 'legend-label' }, 'Pending')
      )
    ),
    h('svg', {
      viewBox: `0 0 ${width} ${height}`,
      className: 'line-chart',
      preserveAspectRatio: 'xMidYMid meet'
    },
      h('g', { innerHTML: `
        <polygon points="${completedArea}" fill="var(--c-green)" opacity="0.2"/>
        <polygon points="${failedArea}" fill="var(--c-red)" opacity="0.2"/>
        <polyline points="${completedPoints}" fill="none" stroke="var(--c-green)" stroke-width="2"/>
        <polyline points="${failedPoints}" fill="none" stroke="var(--c-red)" stroke-width="2"/>
        ${data.map((d, i) => {
          const x = padding.left + (i / (data.length - 1)) * chartWidth;
          const yC = padding.top + chartHeight - (d.completed / maxVal) * chartHeight;
          const yF = padding.top + chartHeight - (d.failed / maxVal) * chartHeight;
          const yP = padding.top + chartHeight - (d.pending / maxVal) * chartHeight;
          return `<circle cx="${x}" cy="${yC}" r="3" fill="var(--c-green)"/><circle cx="${x}" cy="${yF}" r="3" fill="var(--c-red)"/><circle cx="${x}" cy="${yP}" r="3" fill="var(--c-blue)"/>`;
        }).join('')}
        ${labels}
      ` })
    )
  );
}

function renderVenueTable(venues) {
  if (!venues || venues.length === 0) {
    return h('div', { className: 'chart-empty' }, 'No venue data available');
  }
  const table = h('table', { className: 'table' });
  table.appendChild(
    h('thead', {},
      h('tr', {},
        h('th', {}, '#'),
        h('th', {}, 'Venue'),
        h('th', { className: 'cell-num' }, 'Papers'),
      )
    )
  );
  const tbody = h('tbody');
  for (let i = 0; i < venues.length; i++) {
    const v = venues[i];
    const row = h('tr', {},
      h('td', { className: 'cell-num' }, String(i + 1)),
      h('td', {}, v.venue),
      h('td', { className: 'cell-num' }, String(v.count))
    );
    tbody.appendChild(row);
  }
  table.appendChild(tbody);
  return h('div', { className: 'table-wrap' }, table);
}

function renderKeywordsTable(keywords) {
  if (!keywords || keywords.length === 0) {
    return h('div', { className: 'chart-empty' }, 'No keyword data available');
  }
  const table = h('table', { className: 'table' });
  table.appendChild(
    h('thead', {},
      h('tr', {},
        h('th', {}, '#'),
        h('th', {}, 'Keyword'),
        h('th', { className: 'cell-num' }, 'Count'),
      )
    )
  );
  const tbody = h('tbody');
  for (let i = 0; i < keywords.length; i++) {
    const k = keywords[i];
    const row = h('tr', {},
      h('td', { className: 'cell-num' }, String(i + 1)),
      h('td', {}, k.keyword),
      h('td', { className: 'cell-num' }, String(k.count))
    );
    tbody.appendChild(row);
  }
  table.appendChild(tbody);
  return h('div', { className: 'table-wrap' }, table);
}
