import { getAuditLog } from '../api.js';
import { h, clear, loading, toast, emptyState, jsonBlock } from '../components.js';

export async function render(container, { signal }) {
  clear(container);

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Audit Log'),
      h('p', { className: 'page-subtitle' }, 'Track all changes and actions across the system'),
    )
  );

  const filters = h('div', { className: 'filters-bar' });

  const userIdInput = h('input', {
    className: 'input',
    type: 'text',
    id: 'al-user-id',
    placeholder: 'User ID',
  });

  const entityTypeInput = h('input', {
    className: 'input',
    type: 'text',
    id: 'al-entity-type',
    placeholder: 'Entity Type (e.g., Paper)',
  });

  const actionInput = h('input', {
    className: 'input',
    type: 'text',
    id: 'al-action',
    placeholder: 'Action (e.g., Created)',
  });

  const startDateInput = h('input', {
    className: 'input',
    type: 'date',
    id: 'al-start-date',
  });

  const endDateInput = h('input', {
    className: 'input',
    type: 'date',
    id: 'al-end-date',
  });

  const searchBtn = h('button', { className: 'btn btn-primary' }, 'Search');
  const prevBtn = h('button', { className: 'btn' }, 'Previous');
  const nextBtn = h('button', { className: 'btn' }, 'Next');
  const pageInfo = h('span', { className: 'page-info' }, 'Page 1');

  searchBtn.onclick = () => loadPage(1);
  prevBtn.onclick = () => loadPage(currentPage - 1);
  nextBtn.onclick = () => loadPage(currentPage + 1);

  container.appendChild(filters);
  filters.appendChild(h('label', { className: 'field-label' }, 'User ID'));
  filters.appendChild(userIdInput);
  filters.appendChild(h('label', { className: 'field-label' }, 'Entity Type'));
  filters.appendChild(entityTypeInput);
  filters.appendChild(h('label', { className: 'field-label' }, 'Action'));
  filters.appendChild(actionInput);
  filters.appendChild(h('label', { className: 'field-label' }, 'Start Date'));
  filters.appendChild(startDateInput);
  filters.appendChild(h('label', { className: 'field-label' }, 'End Date'));
  filters.appendChild(endDateInput);
  filters.appendChild(searchBtn);

  const pagination = h('div', { className: 'pagination' });
  pagination.appendChild(prevBtn);
  pagination.appendChild(pageInfo);
  pagination.appendChild(nextBtn);
  container.appendChild(pagination);

  const resultsSection = h('div', { id: 'audit-results' });
  container.appendChild(resultsSection);

  let currentPage = 1;
  let totalCount = 0;
  const pageSize = 50;

  async function loadPage(page) {
    if (page < 1) return;

    const params = {
      pageNumber: page,
      pageSize,
      userId: userIdInput.value.trim() || undefined,
      entityType: entityTypeInput.value.trim() || undefined,
      action: actionInput.value.trim() || undefined,
      startDate: startDateInput.value ? new Date(startDateInput.value).toISOString() : undefined,
      endDate: endDateInput.value ? new Date(endDateInput.value).toISOString() : undefined,
    };

    clear(resultsSection);
    resultsSection.appendChild(loading('Loading audit events...'));

    try {
      const result = await getAuditLog(params, signal);
      currentPage = result.pageNumber;
      totalCount = result.totalCount;
      renderResults(result);
    } catch (err) {
      if (err.name === 'AbortError') return;
      clear(resultsSection);
      resultsSection.appendChild(emptyState('Error', err.message));
      toast(err.message, 'error');
    }
  }

  function renderResults(result) {
    clear(resultsSection);

    if (result.items.length === 0) {
      resultsSection.appendChild(emptyState('No audit events', 'No events match your filters'));
      return;
    }

    const table = h('table', { className: 'table' });
    const thead = h('thead');
    thead.appendChild(h('tr', {},
      h('th', {}, 'Timestamp'),
      h('th', {}, 'User'),
      h('th', {}, 'Entity Type'),
      h('th', {}, 'Entity ID'),
      h('th', {}, 'Action'),
      h('th', {}, 'Details'),
    ));
    table.appendChild(thead);

    const tbody = h('tbody');
    for (const event of result.items) {
      const row = h('tr');
      row.appendChild(h('td', {}, new Date(event.timestamp).toLocaleString()));
      row.appendChild(h('td', {}, event.userName || event.userId || 'System'));
      row.appendChild(h('td', {}, event.entityType));
      row.appendChild(h('td', { className: 'cell-mono' }, event.entityId ? String(event.entityId).substring(0, 8) + '...' : '-'));
      row.appendChild(h('td', {}, h('span', { className: `badge badge-${getActionBadge(event.action)}` }, event.action)));
      row.appendChild(h('td', {}, event.diff ? h('button', {
        className: 'btn-text',
        onClick: () => showDiffModal(event),
      }, 'View') : '-'));
      tbody.appendChild(row);
    }
    table.appendChild(tbody);
    resultsSection.appendChild(table);

    const totalPages = Math.ceil(totalCount / pageSize);
    pageInfo.textContent = `Page ${currentPage} of ${totalPages} (${totalCount} total)`;
    prevBtn.disabled = currentPage <= 1;
    nextBtn.disabled = currentPage >= totalPages;
  }

  function getActionBadge(action) {
    switch (action) {
      case 'Created': return 'green';
      case 'Updated': return 'blue';
      case 'Deleted': return 'red';
      default: return 'gray';
    }
  }

  function showDiffModal(event) {
    const modal = h('div', { className: 'modal-backdrop' });
    const content = h('div', { className: 'modal-content' });
    content.appendChild(h('h2', { className: 'section-title' }, `Audit Event Details`));
    content.appendChild(h('div', { className: 'detail-meta' },
      h('div', { className: 'meta-item' }, h('span', { className: 'meta-label' }, 'Timestamp'), h('span', { className: 'meta-value' }, new Date(event.timestamp).toLocaleString())),
      h('div', { className: 'meta-item' }, h('span', { className: 'meta-label' }, 'User'), h('span', { className: 'meta-value' }, event.userName || event.userId || 'System')),
      h('div', { className: 'meta-item' }, h('span', { className: 'meta-label' }, 'Entity Type'), h('span', { className: 'meta-value' }, event.entityType)),
      h('div', { className: 'meta-item' }, h('span', { className: 'meta-label' }, 'Entity ID'), h('span', { className: 'meta-value cell-mono' }, event.entityId || '-')),
      h('div', { className: 'meta-item' }, h('span', { className: 'meta-label' }, 'Action'), h('span', { className: 'meta-value' }, event.action)),
    ));
    if (event.diff) {
      content.appendChild(h('h3', { className: 'section-title mt-4' }, 'Changes'));
      content.appendChild(jsonBlock(event.diff));
    }
    const closeBtn = h('button', { className: 'btn btn-primary mt-4' }, 'Close');
    closeBtn.onclick = () => modal.remove();
    content.appendChild(closeBtn);
    modal.appendChild(content);
    modal.onclick = (e) => { if (e.target === modal) modal.remove(); };
    document.body.appendChild(modal);
  }

  await loadPage(1);
}