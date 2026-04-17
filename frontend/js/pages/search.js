import { searchKeyword, searchSemantic, searchHybrid } from '../api.js';
import {
  h, clear, loading, pagination, debounce, toast, emptyState, truncate
} from '../components.js';

let searchState = {
  mode: 'keyword', // keyword | semantic | hybrid
  query: '',
  keywordWeight: 0.5,
  semanticWeight: 0.5,
  pageNumber: 1,
  pageSize: 25,
};

export async function render(container, { navigate }) {
  clear(container);

  // Search hero
  const hero = h('div', { className: 'search-hero' });
  hero.appendChild(h('h1', { className: 'page-title' }, 'Search'));

  // Search input
  const inputWrap = h('div', { className: 'search-input-wrap' });
  inputWrap.innerHTML = `<svg class="search-icon" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z" clip-rule="evenodd"/></svg>`;

  const searchInput = h('input', {
    className: 'input',
    type: 'search',
    placeholder: 'Search research papers\u2026',
    value: searchState.query,
  });

  const doSearch = debounce(() => {
    searchState.query = searchInput.value;
    searchState.pageNumber = 1;
    if (searchState.query.trim()) {
      executeSearch();
    } else {
      const rc = document.getElementById('search-results');
      if (rc) clear(rc);
    }
  }, 400);

  searchInput.addEventListener('input', doSearch);
  searchInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      searchState.query = searchInput.value;
      searchState.pageNumber = 1;
      if (searchState.query.trim()) executeSearch();
    }
  });
  inputWrap.appendChild(searchInput);
  hero.appendChild(inputWrap);

  // Mode selector
  const modeBar = h('div', { className: 'search-mode-bar' });
  const modes = [
    { id: 'keyword', label: 'Keyword' },
    { id: 'semantic', label: 'Semantic' },
    { id: 'hybrid', label: 'Hybrid' },
  ];
  for (const mode of modes) {
    const btn = h('button', {
      className: `search-mode-btn ${searchState.mode === mode.id ? 'active' : ''}`,
      dataset: { mode: mode.id },
      onClick: () => {
        searchState.mode = mode.id;
        modeBar.querySelectorAll('.search-mode-btn').forEach(b => {
          b.classList.toggle('active', b.dataset.mode === mode.id);
        });
        // Show/hide hybrid options
        const opts = document.getElementById('hybrid-options');
        if (opts) opts.hidden = mode.id !== 'hybrid';
        if (searchState.query.trim()) executeSearch();
      },
    }, mode.label);
    modeBar.appendChild(btn);
  }
  hero.appendChild(modeBar);

  // Hybrid options
  const hybridOpts = h('div', {
    className: 'search-options',
    id: 'hybrid-options',
    hidden: searchState.mode !== 'hybrid',
  });

  const kwWeight = h('div', { className: 'weight-slider' });
  const kwLabel = h('label', {}, 'Keyword');
  const kwValue = h('span', { className: 'weight-value' }, searchState.keywordWeight.toFixed(1));
  const kwSlider = h('input', {
    type: 'range',
    min: '0',
    max: '1',
    step: '0.1',
    value: String(searchState.keywordWeight),
  });
  kwSlider.addEventListener('input', (e) => {
    searchState.keywordWeight = parseFloat(e.target.value);
    searchState.semanticWeight = parseFloat((1 - searchState.keywordWeight).toFixed(1));
    kwValue.textContent = searchState.keywordWeight.toFixed(1);
    semValue.textContent = searchState.semanticWeight.toFixed(1);
    semSlider.value = String(searchState.semanticWeight);
  });
  kwWeight.append(kwLabel, kwSlider, kwValue);

  const semWeight = h('div', { className: 'weight-slider' });
  const semLabel = h('label', {}, 'Semantic');
  const semValue = h('span', { className: 'weight-value' }, searchState.semanticWeight.toFixed(1));
  const semSlider = h('input', {
    type: 'range',
    min: '0',
    max: '1',
    step: '0.1',
    value: String(searchState.semanticWeight),
  });
  semSlider.addEventListener('input', (e) => {
    searchState.semanticWeight = parseFloat(e.target.value);
    searchState.keywordWeight = parseFloat((1 - searchState.semanticWeight).toFixed(1));
    semValue.textContent = searchState.semanticWeight.toFixed(1);
    kwValue.textContent = searchState.keywordWeight.toFixed(1);
    kwSlider.value = String(searchState.keywordWeight);
  });
  semWeight.append(semLabel, semSlider, semValue);

  hybridOpts.append(kwWeight, semWeight);
  hero.appendChild(hybridOpts);

  container.appendChild(hero);

  // Results container
  const resultsContainer = h('div', { id: 'search-results' });
  container.appendChild(resultsContainer);

  // Auto-focus
  requestAnimationFrame(() => searchInput.focus());

  async function executeSearch() {
    const rc = document.getElementById('search-results');
    if (!rc) return;
    clear(rc);
    rc.appendChild(loading('Searching'));

    try {
      let data;
      const q = searchState.query.trim();
      if (!q) return;

      if (searchState.mode === 'keyword') {
        data = await searchKeyword({
          q,
          pageNumber: searchState.pageNumber,
          pageSize: searchState.pageSize,
        });
      } else if (searchState.mode === 'semantic') {
        data = await searchSemantic({
          query: q,
          pageNumber: searchState.pageNumber,
          pageSize: searchState.pageSize,
        });
      } else {
        data = await searchHybrid({
          query: q,
          keywordWeight: searchState.keywordWeight,
          semanticWeight: searchState.semanticWeight,
          pageNumber: searchState.pageNumber,
          pageSize: searchState.pageSize,
        });
      }

      clear(rc);

      if (data.items.length === 0) {
        rc.appendChild(emptyState('No results', `No papers match "${q}"`));
        return;
      }

      // Results count
      rc.appendChild(
        h('div', { className: 'pagination-info mb-6' },
          `${data.totalCount} result${data.totalCount !== 1 ? 's' : ''}`
        )
      );

      // Result items
      for (const result of data.items) {
        const item = h('div', { className: 'search-result' });

        const titleRow = h('div', { className: 'flex items-center' });
        titleRow.appendChild(
          h('a', {
            className: 'search-result-title',
            href: `#/papers/${result.paperId}`,
            onClick: (e) => { e.preventDefault(); navigate(`/papers/${result.paperId}`); },
          }, result.title)
        );
        titleRow.appendChild(
          h('span', { className: 'search-result-score' },
            `${(result.score * 100).toFixed(0)}%`,
            result.matchType ? ` \u00B7 ${result.matchType}` : '',
          )
        );
        item.appendChild(titleRow);

        // Meta
        const metaParts = [];
        if (result.authors?.length) metaParts.push(result.authors.slice(0, 3).join(', '));
        if (result.year) metaParts.push(String(result.year));
        if (result.venue) metaParts.push(result.venue);
        if (metaParts.length) {
          item.appendChild(h('div', { className: 'search-result-meta' }, metaParts.join(' \u00B7 ')));
        }

        // Abstract
        if (result.abstract) {
          item.appendChild(h('p', { className: 'search-result-abstract' }, result.abstract));
        }

        rc.appendChild(item);
      }

      // Pagination
      rc.appendChild(pagination(data, (page) => {
        searchState.pageNumber = page;
        executeSearch();
      }));

    } catch (err) {
      clear(rc);
      if (err.name === 'AbortError') return;
      rc.appendChild(emptyState('Search error', err.message));
      toast(err.message, 'error');
    }
  }
}
