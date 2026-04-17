import { getPaper, getConfig } from '../api.js';
import { h, clear, loading, toast, emptyState, formatAuthors, badge } from '../components.js';
import { store } from '../store.js';

let clusterChart = null;

async function tryCompareFieldsApi(leftId, rightId) {
  const { baseUrl, token } = getConfig();
  const url = `${baseUrl}/api/v1/analysis/compare-fields?left=${encodeURIComponent(leftId)}&right=${encodeURIComponent(rightId)}`;
  const headers = { 'Accept': 'application/json' };
  if (token) headers['Authorization'] = token.startsWith('Bearer ') ? token : `Bearer ${token}`;

  const res = await fetch(url, { method: 'GET', headers });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

export async function render(container, { navigate, params, signal }) {
  const leftId = params.left;
  const rightId = params.right;

  if (!leftId || !rightId) {
    clear(container);
    container.appendChild(
      h('div', { className: 'compare-error' },
        emptyState('Missing Parameters', 'Both left and right paper IDs are required'),
        h('a', {
          className: 'btn btn-primary',
          href: '#/papers',
          onClick: (e) => { e.preventDefault(); navigate('/papers'); }
        }, '\u2190 BACK TO PAPERS')
      )
    );
    return;
  }

  if (leftId === rightId) {
    clear(container);
    container.appendChild(
      h('div', { className: 'compare-error' },
        h('div', { className: 'compare-warning' },
          h('span', { className: 'warning-icon' }, '\u26A0'),
          h('span', {}, 'You are comparing the same paper with itself')
        ),
        h('a', {
          className: 'btn btn-primary',
          href: '#/papers',
          onClick: (e) => { e.preventDefault(); navigate('/papers'); }
        }, '\u2190 BACK TO PAPERS')
      )
    );
    return;
  }

  clear(container);
  container.appendChild(loading('Loading papers...'));

  loadD3Library();

  try {
    const [leftPaper, rightPaper] = await Promise.all([
      getPaper(leftId, signal),
      getPaper(rightId, signal)
    ]);

    let fieldComparison = null;
    try {
      fieldComparison = await tryCompareFieldsApi(leftId, rightId);
    } catch {
    }

    clear(container);
    renderCompareView(container, leftPaper, rightPaper, navigate, fieldComparison);

  } catch (err) {
    if (err.name === 'AbortError') return;
    clear(container);
    container.appendChild(
      h('div', { className: 'compare-error' },
        emptyState('Paper not found', err.message),
        h('a', {
          className: 'btn btn-primary',
          href: '#/papers',
          onClick: (e) => { e.preventDefault(); navigate('/papers'); }
        }, '\u2190 BACK TO PAPERS')
      )
    );
    toast(err.message, 'error');
  }
}

function renderCompareView(container, left, right, navigate, fieldComparison) {
  const diffFields = computeFieldDiffs(left, right);

  container.appendChild(
    h('div', { className: 'compare-page' },
      h('div', { className: 'compare-header' },
        h('a', {
          className: 'detail-back',
          href: '#/papers',
          onClick: (e) => { e.preventDefault(); navigate('/papers'); }
        }, '\u2190 PAPERS'),
        h('h1', { className: 'page-title' }, 'Paper Comparison')
      ),
      h('div', { className: 'compare-grid' },
        h('div', { className: 'compare-column' },
          h('div', { className: 'compare-column-header' },
            h('span', { className: 'compare-label' }, 'LEFT'),
            h('a', {
              className: 'compare-paper-link',
              href: `#/papers/${left.id}`,
              onClick: (e) => { e.preventDefault(); navigate(`/papers/${left.id}`); }
            }, '\u2192 VIEW PAPER')
          ),
          renderPaperCard(left, diffFields)
        ),
        h('div', { className: 'compare-column' },
          h('div', { className: 'compare-column-header' },
            h('span', { className: 'compare-label' }, 'RIGHT'),
            h('a', {
              className: 'compare-paper-link',
              href: `#/papers/${right.id}`,
              onClick: (e) => { e.preventDefault(); navigate(`/papers/${right.id}`); }
            }, '\u2192 VIEW PAPER')
          ),
          renderPaperCard(right, diffFields)
        )
      ),
      h('div', { className: 'compare-section' },
        h('div', { className: 'section-header' },
          h('h2', { className: 'section-title' }, 'Abstract Comparison')
        ),
        h('div', { className: 'compare-abstracts' },
          h('div', { className: 'compare-abstract compare-abstract-left' },
            left.abstract ? renderAbstractDiff(left.abstract, right.abstract, 'left') :
              h('div', { className: 'compare-identical' }, 'No abstract available')
          ),
          h('div', { className: 'compare-abstract compare-abstract-right' },
            right.abstract ? renderAbstractDiff(right.abstract, left.abstract, 'right') :
              h('div', { className: 'compare-identical' }, 'No abstract available')
          )
        )
      ),
      h('div', { className: 'compare-section' },
        h('div', { className: 'section-header' },
          h('h2', { className: 'section-title' }, 'Semantic Cluster Map')
        ),
        h('div', { id: 'cluster-map-container', className: 'section', style: 'min-height: 500px;' })
      )
    )
  );

  if (fieldComparison && fieldComparison.semanticClusters) {
    renderClusterMap(fieldComparison.semanticClusters);
  } else {
    renderPlaceholderClusterMap(container);
  }
}

function renderClusterMap(clusters) {
  const container = document.getElementById('cluster-map-container');
  if (!container || typeof d3 === 'undefined') return;

  clear(container);

  const width = container.clientWidth || 700;
  const height = 500;

  const svg = d3.select(container)
    .append('svg')
    .attr('width', width)
    .attr('height', height)
    .attr('viewBox', `0 0 ${width} ${height}`);

  const nodes = [];
  const links = [];
  const clusterColors = {};

  clusters.forEach((cluster, idx) => {
    const color = d3.schemeCategory10[idx % 10];
    clusterColors[cluster.id] = color;

    nodes.push({
      id: cluster.id,
      label: cluster.label || cluster.id,
      type: 'cluster',
      color: color,
      size: cluster.papers ? cluster.papers.length * 3 + 10 : 20,
    });

    if (cluster.papers) {
      cluster.papers.forEach(paper => {
        nodes.push({
          id: paper.id,
          label: paper.title || paper.id,
          type: 'paper',
          color: color,
          size: 8,
          clusterId: cluster.id,
        });

        links.push({
          source: cluster.id,
          target: paper.id,
          type: 'belongs_to',
        });
      });
    }

    if (cluster.connections) {
      cluster.connections.forEach(conn => {
        links.push({
          source: cluster.id,
          target: conn,
          type: 'connected_to',
        });
      });
    }
  });

  const simulation = d3.forceSimulation(nodes)
    .force('link', d3.forceLink(links).id(d => d.id).distance(100))
    .force('charge', d3.forceManyBody().strength(-300))
    .force('center', d3.forceCenter(width / 2, height / 2))
    .force('collision', d3.forceCollide().radius(d => d.size + 5));

  const linkElements = svg.append('g')
    .selectAll('line')
    .data(links)
    .enter()
    .append('line')
    .attr('stroke', d => d.type === 'belongs_to' ? '#ccc' : '#999')
    .attr('stroke-width', d => d.type === 'belongs_to' ? 1 : 2)
    .attr('stroke-dasharray', d => d.type === 'connected_to' ? '4,4' : 'none');

  const nodeElements = svg.append('g')
    .selectAll('circle')
    .data(nodes)
    .enter()
    .append('circle')
    .attr('r', d => d.size)
    .attr('fill', d => d.color)
    .attr('stroke', '#fff')
    .attr('stroke-width', 2)
    .style('cursor', 'pointer')
    .call(d3.drag
      .on('start', dragStarted)
      .on('drag', dragged)
      .on('end', dragEnded));

  nodeElements.append('title')
    .text(d => d.label);

  const labelElements = svg.append('g')
    .selectAll('text')
    .data(nodes.filter(n => n.type === 'cluster'))
    .enter()
    .append('text')
    .attr('text-anchor', 'middle')
    .attr('dy', d => d.size + 15)
    .attr('fill', '#333')
    .attr('font-size', '12px')
    .attr('font-weight', '600')
    .text(d => d.label);

  simulation.on('tick', () => {
    linkElements
      .attr('x1', d => d.source.x)
      .attr('y1', d => d.source.y)
      .attr('x2', d => d.target.x)
      .attr('y2', d => d.target.y);

    nodeElements
      .attr('cx', d => d.x)
      .attr('cy', d => d.y);

    labelElements
      .attr('x', d => d.x)
      .attr('y', d => d.y);
  });

  function dragStarted(event, d) {
    if (!event.active) simulation.alphaTarget(0.3).restart();
    d.fx = d.x;
    d.fy = d.y;
  }

  function dragged(event, d) {
    d.fx = event.x;
    d.fy = event.y;
  }

  function dragEnded(event, d) {
    if (!event.active) simulation.alphaTarget(0);
    d.fx = null;
    d.fy = null;
  }

  clusterChart = { simulation, nodes, links };
}

function renderPlaceholderClusterMap(container) {
  const mapContainer = container.querySelector('#cluster-map-container');
  if (!mapContainer) return;

  mapContainer.innerHTML = '';

  const width = mapContainer.clientWidth || 700;
  const height = 500;

  const svg = d3.select(mapContainer)
    .append('svg')
    .attr('width', width)
    .attr('height', height)
    .attr('viewBox', `0 0 ${width} ${height}`);

  const centerX = width / 2;
  const centerY = height / 2;

  const clusterData = [
    { id: 'left', label: 'Left Paper', x: centerX - 150, y: centerY, color: '#4f46e5' },
    { id: 'right', label: 'Right Paper', x: centerX + 150, y: centerY, color: '#10b981' },
    { id: 'common', label: 'Common Concepts', x: centerX, y: centerY - 120, color: '#f59e0b' },
    { id: 'unique-left', label: 'Unique Left', x: centerX - 180, y: centerY + 120, color: '#6366f1' },
    { id: 'unique-right', label: 'Unique Right', x: centerX + 180, y: centerY + 120, color: '#14b8a6' },
  ];

  const links = [
    { source: 'left', target: 'common' },
    { source: 'right', target: 'common' },
    { source: 'left', target: 'unique-left' },
    { source: 'right', target: 'unique-right' },
    { source: 'common', target: 'unique-left' },
    { source: 'common', target: 'unique-right' },
  ];

  const linkElements = svg.append('g')
    .selectAll('line')
    .data(links)
    .enter()
    .append('line')
    .attr('x1', d => {
      const s = clusterData.find(c => c.id === d.source);
      return s.x;
    })
    .attr('y1', d => {
      const s = clusterData.find(c => c.id === d.source);
      return s.y;
    })
    .attr('x2', d => {
      const t = clusterData.find(c => c.id === d.target);
      return t.x;
    })
    .attr('y2', d => {
      const t = clusterData.find(c => c.id === d.target);
      return t.y;
    })
    .attr('stroke', '#ccc')
    .attr('stroke-width', 2)
    .attr('stroke-dasharray', '4,4');

  const nodeElements = svg.append('g')
    .selectAll('circle')
    .data(clusterData)
    .enter()
    .append('circle')
    .attr('cx', d => d.x)
    .attr('cy', d => d.y)
    .attr('r', 30)
    .attr('fill', d => d.color)
    .attr('stroke', '#fff')
    .attr('stroke-width', 3);

  nodeElements.append('title')
    .text(d => d.label);

  svg.selectAll('text')
    .data(clusterData)
    .enter()
    .append('text')
    .attr('x', d => d.x)
    .attr('y', d => d.y + 50)
    .attr('text-anchor', 'middle')
    .attr('fill', '#333')
    .attr('font-size', '12px')
    .attr('font-weight', '600')
    .text(d => d.label);
}

function computeFieldDiffs(left, right) {
  return {
    title: left.title !== right.title,
    authors: formatAuthors(left.authors, 10) !== formatAuthors(right.authors, 10),
    year: String(left.year ?? '') !== String(right.year ?? ''),
    venue: (left.venue || '') !== (right.venue || ''),
    source: (left.source || '') !== (right.source || ''),
    citationCount: String(left.citationCount ?? '') !== String(right.citationCount ?? ''),
    abstract: (left.abstract || '') !== (right.abstract || ''),
  };
}

function renderPaperCard(paper, diffFields) {
  const fields = [
    { label: 'Title', value: paper.title, key: 'title' },
    { label: 'Authors', value: formatAuthors(paper.authors, 10), key: 'authors' },
    { label: 'Year', value: paper.year ? String(paper.year) : '\u2014', key: 'year' },
    { label: 'Venue', value: paper.venue || '\u2014', key: 'venue' },
    { label: 'Source', value: paper.source || '\u2014', key: 'source' },
    { label: 'Citation Count', value: String(paper.citationCount ?? '\u2014'), key: 'citationCount' },
  ];

  const card = h('div', { className: 'compare-card' });

  for (const field of fields) {
    const isDiff = diffFields[field.key];
    card.appendChild(
      h('div', { className: `compare-field${isDiff ? ' compare-field-diff' : ''}` },
        h('div', { className: 'compare-field-label' },
          field.label,
          isDiff ? h('span', { className: 'diff-indicator' }, '\u2260') : null
        ),
        h('div', { className: 'compare-field-value' }, field.value)
      )
    );
  }

  return card;
}

function renderAbstractDiff(source, target, side) {
  if (!source) {
    return h('div', { className: 'compare-identical' }, 'No abstract');
  }
  if (!target) {
    return h('div', { className: 'compare-abstract-text' }, source);
  }

  const diff = computeLCSDiff(source, target);

  const hasChanges = diff.some(s => s.type !== 'equal');
  if (!hasChanges) {
    return h('div', { className: 'compare-identical' },
      h('span', { className: 'identical-badge' }, '\u2713 IDENTICAL')
    );
  }

  const container = h('div', { className: 'compare-abstract-text' });

  for (const segment of diff) {
    if (segment.type === 'equal') {
      container.appendChild(document.createTextNode(segment.text));
    } else if (segment.type === 'delete' && side === 'left') {
      container.appendChild(
        h('span', { className: 'diff-remove' }, segment.text)
      );
    } else if (segment.type === 'insert' && side === 'right') {
      container.appendChild(
        h('span', { className: 'diff-add' }, segment.text)
      );
    } else if (segment.type === 'insert' && side === 'left') {
      container.appendChild(
        h('span', { className: 'diff-add-inline' }, segment.text)
      );
    } else if (segment.type === 'delete' && side === 'right') {
      container.appendChild(
        h('span', { className: 'diff-remove-inline' }, segment.text)
      );
    }
  }

  return container;
}

function computeLCSDiff(text1, text2) {
  const a = text1.split('');
  const b = text2.split('');

  const m = a.length;
  const n = b.length;

  const dp = Array(m + 1).fill(null).map(() => Array(n + 1).fill(0));

  for (let i = 1; i <= m; i++) {
    for (let j = 1; j <= n; j++) {
      if (a[i - 1] === b[j - 1]) {
        dp[i][j] = dp[i - 1][j - 1] + 1;
      } else {
        dp[i][j] = Math.max(dp[i - 1][j], dp[i][j - 1]);
      }
    }
  }

  const result = [];
  let i = m, j = n;

  while (i > 0 || j > 0) {
    if (i > 0 && j > 0 && a[i - 1] === b[j - 1]) {
      result.unshift({ type: 'equal', text: a[i - 1] });
      i--;
      j--;
    } else if (j > 0 && (i === 0 || dp[i][j - 1] >= dp[i - 1][j])) {
      result.unshift({ type: 'insert', text: b[j - 1] });
      j--;
    } else {
      result.unshift({ type: 'delete', text: a[i - 1] });
      i--;
    }
  }

  return mergeSegments(result);
}

function mergeSegments(segments) {
  if (segments.length === 0) return segments;

  const merged = [];
  let current = { ...segments[0] };

  for (let i = 1; i < segments.length; i++) {
    const seg = segments[i];
    if (seg.type === current.type) {
      current.text += seg.text;
    } else {
      merged.push(current);
      current = { ...seg };
    }
  }
  merged.push(current);

  return merged;
}

function loadD3Library() {
  if (document.querySelector('script[src*="d3"]')) return;
  const script = document.createElement('script');
  script.src = 'https://cdn.jsdelivr.net/npm/d3@7.8.5/dist/d3.min.js';
  document.head.appendChild(script);
}

export const init = () => {};
export const cleanup = () => {
  if (clusterChart && clusterChart.simulation) {
    clusterChart.simulation.stop();
    clusterChart = null;
  }
};