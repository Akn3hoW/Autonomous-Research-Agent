import { getClusterMap, createClusteringJob } from '../api.js';
import { h, clear, loading, toast, emptyState, formatAuthors } from '../components.js';

let clusterData = null;
let hoveredPaper = null;

export async function render(container, { navigate }) {
  clear(container);

  container.appendChild(h('div', { className: 'page-header' },
    h('h1', { className: 'page-title' }, 'Topic Map'),
    h('p', { className: 'page-subtitle' }, 'Interactive visualization of paper clusters based on semantic embeddings')
  ));

  const toolbar = h('div', { className: 'filter-bar' });
  toolbar.appendChild(h('button', {
    className: 'btn btn-primary',
    onClick: async () => {
      try {
        await createClusteringJob();
        toast('Clustering job started', 'success');
      } catch (err) {
        toast(err.message, 'error');
      }
    }
  }, 'Regenerate Clusters'));
  container.appendChild(toolbar);

  const chartContainer = h('div', { id: 'cluster-chart', className: 'cluster-chart-container' });
  container.appendChild(chartContainer);

  try {
    clusterData = await getClusterMap();
    clear(chartContainer);

    if (!clusterData.papers || clusterData.papers.length === 0) {
      chartContainer.appendChild(emptyState('No clustered papers', 'Generate embeddings and run clustering to see the topic map'));
      return;
    }

    renderScatterPlot(chartContainer, clusterData.papers, navigate);
  } catch (err) {
    clear(chartContainer);
    if (err.name === 'AbortError') return;
    chartContainer.appendChild(emptyState('Error loading cluster map', err.message));
    toast(err.message, 'error');
  }
}

function renderScatterPlot(container, papers, navigate) {
  const tooltip = h('div', {
    id: 'cluster-tooltip',
    className: 'cluster-tooltip',
    style: 'position:fixed;display:none;z-index:1000;pointer-events:none;'
  });
  container.appendChild(tooltip);

  const width = container.clientWidth || 800;
  const height = 600;
  const padding = 60;

  const svg = h('svg', {
    width: width,
    height: height,
    style: 'background:#fafafa;border-radius:8px;'
  });

  const xMin = Math.min(...papers.map(p => p.x));
  const xMax = Math.max(...papers.map(p => p.x));
  const yMin = Math.min(...papers.map(p => p.y));
  const yMax = Math.max(...papers.map(p => p.y));

  const xScale = (x) => padding + ((x - xMin) / (xMax - xMin || 1)) * (width - 2 * padding);
  const yScale = (y) => height - padding - ((y - yMin) / (yMax - yMin || 1)) * (height - 2 * padding);

  const colors = ['#6366f1', '#ec4899', '#10b981', '#f59e0b', '#3b82f6', '#8b5cf6', '#ef4444', '#14b8a6'];

  for (let i = 0; i < papers.length; i++) {
    const paper = papers[i];
    const cx = xScale(paper.x);
    const cy = yScale(paper.y);
    const color = colors[i % colors.length];

    const dot = h('circle', {
      cx: cx,
      cy: cy,
      r: 8,
      fill: color,
      opacity: 0.7,
      style: 'cursor:pointer;transition:opacity 0.2s,r 0.2s;',
      'data-index': i
    });

    dot.addEventListener('mouseenter', (e) => {
      dot.setAttribute('opacity', 1);
      dot.setAttribute('r', 12);

      const rect = container.getBoundingClientRect();
      tooltip.innerHTML = `
        <div style="font-weight:600;margin-bottom:4px;max-width:250px">${escapeHtml(paper.title)}</div>
        <div style="color:#666;font-size:12px">${escapeHtml(formatAuthors(paper.authors, 2))}</div>
        ${paper.year ? `<div style="color:#666;font-size:12px">${paper.year}</div>` : ''}
      `;
      tooltip.style.display = 'block';
      tooltip.style.left = (e.clientX + 15) + 'px';
      tooltip.style.top = (e.clientY - 10) + 'px';
      hoveredPaper = paper;
    });

    dot.addEventListener('mousemove', (e) => {
      tooltip.style.left = (e.clientX + 15) + 'px';
      tooltip.style.top = (e.clientY - 10) + 'px';
    });

    dot.addEventListener('mouseleave', () => {
      dot.setAttribute('opacity', 0.7);
      dot.setAttribute('r', 8);
      tooltip.style.display = 'none';
      hoveredPaper = null;
    });

    dot.addEventListener('click', () => {
      navigate(`/papers/${paper.id}`);
    });

    svg.appendChild(dot);
  }

  const xAxis = h('line', {
    x1: padding, y1: height - padding,
    x2: width - padding, y2: height - padding,
    stroke: '#ccc', 'stroke-width': 1
  });
  svg.appendChild(xAxis);

  const yAxis = h('line', {
    x1: padding, y1: padding,
    x2: padding, y2: height - padding,
    stroke: '#ccc', 'stroke-width': 1
  });
  svg.appendChild(yAxis);

  container.appendChild(svg);

  const info = h('div', { style: 'margin-top:16px;color:#666;font-size:14px' },
    `${papers.length} papers clustered • Hover for details • Click to open paper`
  );
  container.appendChild(info);
}

function escapeHtml(text) {
  if (!text) return '';
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}
