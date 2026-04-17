import { getCitationGraph, ingestCitations, escapeHtml } from '../api.js';
import { h, clear, loading, toast, emptyState } from '../components.js';

export async function renderCitationGraph(container, { paperId, navigate, signal } = {}) {
  clear(container);

  const depth = 2;
  container.appendChild(loading('Loading citation graph...'));

  try {
    const graph = await getCitationGraph(paperId, { depth }, signal);

    clear(container);

    if (!graph.nodes || graph.nodes.length === 0) {
      container.appendChild(emptyState('No citation data', 'Import citations to see the graph'));
      return;
    }

    const header = h('div', { className: 'flex items-center justify-between mb-4' },
      h('h3', { className: 'section-title' }, `Citation Graph (${graph.nodes.length} papers)`),
      h('button', {
        className: 'btn btn-secondary btn-sm',
        onClick: async () => {
          try {
            await ingestCitations(paperId);
            toast('Citations ingested', 'success');
            navigate(`/papers/${paperId}/graph`);
          } catch (err) {
            toast(err.message, 'error');
          }
        }
      }, 'INGEST CITATIONS')
    );
    container.appendChild(header);

    const graphContainer = h('div', {
      className: 'citation-graph-container',
      style: 'width:100%;height:500px;border:1px solid var(--c-border);border-radius:8px;overflow:hidden;background:var(--c-surface);'
    });

    renderForceGraph(graphContainer, graph, navigate, paperId);
    container.appendChild(graphContainer);

  } catch (err) {
    if (err.name === 'AbortError') return;
    clear(container);
    container.appendChild(emptyState('Failed to load graph', err.message));
    toast(err.message, 'error');
  }
}

function renderForceGraph(container, graph, navigate, currentPaperId) {
  const width = container.clientWidth || 800;
  const height = container.clientHeight || 500;

  const svg = h('svg', {
    width: width,
    height: height,
    style: 'display:block;'
  });
  container.appendChild(svg);

  const nodes = graph.nodes.map(n => ({
    ...n,
    x: width / 2 + (Math.random() - 0.5) * 200,
    y: height / 2 + (Math.random() - 0.5) * 200,
    vx: 0,
    vy: 0
  }));

  const nodeMap = new Map(nodes.map(n => [n.id, n]));

  const links = graph.edges
    .filter(e => nodeMap.has(e.sourceId) && nodeMap.has(e.targetId))
    .map(e => ({
      source: nodeMap.get(e.sourceId),
      target: nodeMap.get(e.targetId),
      context: e.context
    }));

  const defs = h('defs', {});
  svg.appendChild(defs);

  const marker = h('marker', {
    id: 'arrowhead',
    markerWidth: '10',
    markerHeight: '7',
    refX: '20',
    refY: '3.5',
    orient: 'auto'
  });
  defs.appendChild(marker);
  marker.appendChild(h('polygon', {
    points: '0 0, 10 3.5, 0 7',
    fill: 'var(--c-text-secondary)'
  }));

  const linkGroup = h('g', { className: 'links' });
  svg.appendChild(linkGroup);

  const nodeGroup = h('g', { className: 'nodes' });
  svg.appendChild(nodeGroup);

  const linkElements = [];
  const nodeElements = [];

  const simulation = {
    nodes: nodes,
    links: links,
    alpha: 1,
    alphaMin: 0.001,
    alphaDecay: 0.02,
    velocityDecay: 0.4,
    forces: {
      link: { force: linkForce, distance: 100, strength: 0.5 },
      charge: { force: chargeForce, strength: -300 },
      center: { force: centerForce, x: width / 2, y: height / 2, strength: 0.05 },
      collision: { force: collisionForce, radius: 40 }
    }
  };

  function linkForce(link) {
    const dx = link.target.x - link.source.x;
    const dy = link.target.y - link.source.y;
    const distance = Math.sqrt(dx * dx + dy * dy) || 1;
    const force = (distance - simulation.forces.link.distance) * simulation.forces.link.strength;
    const fx = (dx / distance) * force;
    const fy = (dy / distance) * force;
    link.target.vx -= fx;
    link.target.vy -= fy;
    link.source.vx += fx;
    link.source.vy += fy;
  }

  function chargeForce(node1, idx) {
    nodes.forEach((node2, idx2) => {
      if (idx === idx2) return;
      const dx = node2.x - node1.x;
      const dy = node2.y - node1.y;
      const distance = Math.sqrt(dx * dx + dy * dy) || 1;
      const force = simulation.forces.charge.strength * node1.citationCount * 0.01 / distance;
      const fx = (dx / distance) * force;
      const fy = (dy / distance) * force;
      node1.vx -= fx;
      node1.vy -= fy;
    });
  }

  function centerForce(node) {
    const dx = simulation.forces.center.x - node.x;
    const dy = simulation.forces.center.y - node.y;
    node.vx += dx * simulation.forces.center.strength;
    node.vy += dy * simulation.forces.center.strength;
  }

  function collisionForce(node) {
    nodes.forEach(other => {
      if (node === other) return;
      const dx = other.x - node.x;
      const dy = other.y - node.y;
      const distance = Math.sqrt(dx * dx + dy * dy) || 1;
      const minDist = simulation.forces.collision.radius * 2;
      if (distance < minDist) {
        const overlap = (minDist - distance) / 2;
        const fx = (dx / distance) * overlap;
        const fy = (dy / distance) * overlap;
        node.x -= fx;
        node.y -= fy;
        other.x += fx;
        other.y += fy;
      }
    });
  }

  function tick() {
    linkElements.forEach(link => {
      const x1 = link.__data__.source.x;
      const y1 = link.__data__.source.y;
      const x2 = link.__data__.target.x;
      const y2 = link.__data__.target.y;
      link.setAttribute('x1', x1);
      link.setAttribute('y1', y1);
      link.setAttribute('x2', x2);
      link.setAttribute('y2', y2);
    });

    nodeElements.forEach(node => {
      node.setAttribute('transform', `translate(${node.__data__.x},${node.__data__.y})`);
    });
  }

  links.forEach(link => {
    const line = h('line', {
      className: 'graph-link',
      x1: link.source.x,
      y1: link.source.y,
      x2: link.target.x,
      y2: link.target.y,
      'marker-end': 'url(#arrowhead)',
      style: 'stroke:var(--c-border);stroke-width:1.5;'
    });
    line.__data__ = link;
    linkGroup.appendChild(line);
    linkElements.push(line);
  });

  const tooltip = h('div', {
    className: 'graph-tooltip',
    style: 'position:absolute;display:none;padding:8px 12px;background:var(--c-bg);border:1px solid var(--c-border);border-radius:4px;font-size:12px;max-width:300px;z-index:1000;pointer-events:none;'
  });
  container.style.position = 'relative';
  container.appendChild(tooltip);

  nodes.forEach(node => {
    const isMain = node.id === nodes[0].id;
    const g = h('g', {
      className: 'graph-node',
      transform: `translate(${node.x},${node.y})`,
      style: 'cursor:pointer;',
      onClick: () => {
        if (!node.isInDatabase) {
          toast('Paper not in database', 'info');
          return;
        }
        navigate(`/papers/${node.id}`);
      }
    });

    const radius = isMain ? 12 : 6 + Math.min(node.citationCount * 0.5, 8);

    const circle = h('circle', {
      r: radius,
      fill: isMain ? 'var(--c-primary)' : node.isInDatabase ? 'var(--c-accent)' : 'var(--c-text-secondary)',
      stroke: isMain ? 'var(--c-primary)' : 'transparent',
      'stroke-width': '2',
      style: `transition:fill 0.2s;`
    });

    circle.addEventListener('mouseenter', (e) => {
      circle.setAttribute('fill', 'var(--c-primary)');
      tooltip.innerHTML = `
        <strong>${escapeHtml(node.title)}</strong><br/>
        <span style="color:var(--c-text-secondary)">Year: ${escapeHtml(node.year) || 'Unknown'}</span><br/>
        <span style="color:var(--c-text-secondary)">Citations: ${escapeHtml(String(node.citationCount))}</span><br/>
        <span style="color:var(--c-text-secondary)">${node.isInDatabase ? 'In Database' : 'External'}</span>
      `;
      tooltip.style.display = 'block';
      tooltip.style.left = `${e.offsetX + 15}px`;
      tooltip.style.top = `${e.offsetY + 15}px`;
    });

    circle.addEventListener('mouseleave', () => {
      circle.setAttribute('fill', isMain ? 'var(--c-primary)' : node.isInDatabase ? 'var(--c-accent)' : 'var(--c-text-secondary)');
      tooltip.style.display = 'none';
    });

    circle.addEventListener('mousemove', (e) => {
      tooltip.style.left = `${e.offsetX + 15}px`;
      tooltip.style.top = `${e.offsetY + 15}px`;
    });

    g.appendChild(circle);
    g.__data__ = node;
    nodeGroup.appendChild(g);
    nodeElements.push(g);
  });

  let animating = true;

  function animate() {
    if (!animating) return;

    simulation.alpha *= (1 - simulation.alphaDecay);
    if (simulation.alpha < simulation.alphaMin) {
      animating = false;
      return;
    }

    simulation.links.forEach(link => simulation.forces.link.force(link));

    nodes.forEach((node, idx) => {
      simulation.forces.charge.force(node, idx);
      simulation.forces.center.force(node);
      simulation.forces.collision.force(node);
    });

    nodes.forEach(node => {
      node.vx *= (1 - simulation.velocityDecay);
      node.vy *= (1 - simulation.velocityDecay);
      node.x += node.vx * simulation.alpha;
      node.y += node.vy * simulation.alpha;

      node.x = Math.max(20, Math.min(width - 20, node.x));
      node.y = Math.max(20, Math.min(height - 20, node.y));
    });

    tick();
    requestAnimationFrame(animate);
  }

  animate();

  const zoom = h('div', {
    className: 'graph-zoom',
    style: 'position:absolute;bottom:10px;right:10px;display:flex;gap:4px;'
  });

  const zoomIn = h('button', {
    className: 'btn btn-secondary btn-sm',
    innerHTML: '+',
    onClick: () => {
      svg.setAttribute('width', width * 1.2);
      svg.setAttribute('height', height * 1.2);
      container.style.width = (width * 1.2) + 'px';
      container.style.height = (height * 1.2) + 'px';
    }
  });

  const zoomOut = h('button', {
    className: 'btn btn-secondary btn-sm',
    innerHTML: '-',
    onClick: () => {
      svg.setAttribute('width', width * 0.8);
      svg.setAttribute('height', height * 0.8);
      container.style.width = (width * 0.8) + 'px';
      container.style.height = (height * 0.8) + 'px';
    }
  });

  zoom.appendChild(zoomOut);
  zoom.appendChild(zoomIn);
  container.appendChild(zoom);
}
