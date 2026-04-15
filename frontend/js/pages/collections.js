import { getCollections, getCollection, createCollection, updateCollection, deleteCollection, addPaperToCollection, removePaperFromCollection, reorderCollectionPapers, exportCollection, getConfig } from '../api.js';
import { h, clear, loading, toast, emptyState, badge, formatAuthors } from '../components.js';
import { ApiError } from '../api.js';

let collections = [];
let allPapers = [];
let draggedPaperId = null;
let dragSourceCollectionId = null;

export async function render(container, { navigate }) {
  clear(container);
  container.appendChild(h('div', { className: 'page-header' },
    h('h1', { className: 'page-title' }, 'Collections'),
    h('p', { className: 'page-subtitle' }, 'Organize your research papers into collections')
  ));

  const actionsBar = h('div', { className: 'filter-bar' },
    h('button', { className: 'btn btn-primary', id: 'add-collection-btn' }, '+ New Collection')
  );
  container.appendChild(actionsBar);

  document.getElementById('add-collection-btn').addEventListener('click', () => showCollectionModal());

  const board = h('div', { id: 'collections-board', className: 'kanban-board' });
  container.appendChild(board);

  await loadCollections();
}

async function loadCollections() {
  const board = document.getElementById('collections-board');
  if (!board) return;
  clear(board);
  board.appendChild(loading());

  try {
    collections = await getCollections();
    allPapers = await getAllPapers();

    clear(board);

    if (collections.length === 0) {
      board.appendChild(emptyState('No collections yet', 'Create a collection to organize your papers'));
      return;
    }

    const boardInner = h('div', { className: 'kanban-board-inner' });

    for (const coll of collections) {
      const col = buildCollectionColumn(coll);
      boardInner.appendChild(col);
    }

    board.appendChild(boardInner);
  } catch (err) {
    clear(board);
    board.appendChild(emptyState('Error loading collections', err.message));
    toast(err.message, 'error');
  }
}

async function getAllPapers() {
  const papers = [];
  let page = 1;
  while (true) {
    const data = await getPapers({ pageNumber: page, pageSize: 100 });
    papers.push(...data.items);
    if (page >= data.totalPages) break;
    page++;
  }
  return papers;
}

function buildCollectionColumn(collection) {
  const col = h('div', {
    className: 'kanban-column',
    dataset: { collectionId: collection.id }
  });

  const header = h('div', { className: 'kanban-column-header' },
    h('div', { className: 'kanban-column-title' },
      h('span', {}, collection.name),
      collection.isShared ? h('span', { className: 'badge badge-blue', style: 'margin-left:6px' }, 'Shared') : null
    ),
    h('div', { className: 'kanban-column-actions' },
      h('button', { className: 'btn-icon', title: 'Edit', onClick: () => showCollectionModal(collection) }, '\u270E'),
      h('button', { className: 'btn-icon', title: 'Export ZIP', onClick: () => handleExportZip(collection.id, collection.name) }, '\u21E9'),
      h('button', { className: 'btn-icon', title: 'Export BibTeX', onClick: () => handleExportBibtex(collection.id, collection.name) }, '\u21E9'),
      h('button', { className: 'btn-icon btn-icon-danger', title: 'Delete', onClick: () => handleDelete(collection.id) }, '\u2715')
    )
  );
  col.appendChild(header);

  const papersList = h('div', {
    className: 'kanban-papers',
    dataset: { collectionId: collection.id, paperId: 'drop-target' }
  });

  papersList.addEventListener('dragover', handleDragOver);
  papersList.addEventListener('dragleave', handleDragLeave);
  papersList.addEventListener('drop', handleDrop);

  col.appendChild(papersList);

  loadCollectionPapers(collection.id, papersList);

  const footer = h('div', { className: 'kanban-column-footer' },
    h('button', { className: 'btn btn-sm btn-ghost', id: `add-paper-${collection.id}` }, '+ Add Paper')
  );
  col.appendChild(footer);

  document.getElementById(`add-paper-${collection.id}`).addEventListener('click', () => showAddPaperModal(collection));

  return col;
}

async function loadCollectionPapers(collectionId, container) {
  clear(container);
  try {
    const detail = await getCollection(collectionId);
    if (detail.papers.length === 0) {
      container.appendChild(h('div', { className: 'kanban-empty' }, 'Drop papers here'));
      return;
    }

    for (const paper of detail.papers) {
      const card = buildPaperCard(paper);
      container.appendChild(card);
    }
  } catch (err) {
    toast('Error loading papers: ' + err.message, 'error');
  }
}

function buildPaperCard(paper) {
  const card = h('div', {
    className: 'kanban-paper-card',
    draggable: true,
    dataset: { paperId: paper.paperId }
  });

  card.addEventListener('dragstart', handleDragStart);
  card.addEventListener('dragend', handleDragEnd);

  card.innerHTML = `
    <div class="kanban-paper-title">${escapeHtml(paper.title)}</div>
    <div class="kanban-paper-meta">${formatAuthors(paper.authors, 2)} · ${paper.year || '—'}</div>
  `;

  const removeBtn = h('button', {
    className: 'kanban-paper-remove',
    title: 'Remove from collection',
    onClick: (e) => {
      e.stopPropagation();
      handleRemovePaper(paper.collectionId, paper.paperId);
    }
  }, '\u2715');
  card.appendChild(removeBtn);

  return card;
}

function handleDragStart(e) {
  draggedPaperId = e.currentTarget.dataset.paperId;
  dragSourceCollectionId = e.currentTarget.closest('.kanban-column').dataset.collectionId;
  e.currentTarget.classList.add('dragging');
  e.dataTransfer.effectAllowed = 'move';
}

function handleDragEnd(e) {
  e.currentTarget.classList.remove('dragging');
  draggedPaperId = null;
  dragSourceCollectionId = null;
  document.querySelectorAll('.kanban-papers').forEach(el => el.classList.remove('drag-over'));
}

function handleDragOver(e) {
  e.preventDefault();
  e.dataTransfer.dropEffect = 'move';
  e.currentTarget.classList.add('drag-over');
}

function handleDragLeave(e) {
  e.currentTarget.classList.remove('drag-over');
}

async function handleDrop(e) {
  e.preventDefault();
  e.currentTarget.classList.remove('drag-over');

  const targetCollectionId = e.currentTarget.dataset.collectionId;
  if (!draggedPaperId) return;

  const sourceColId = dragSourceCollectionId;
  const targetColId = targetCollectionId;

  if (sourceColId === targetColId) return;

  try {
    await addPaperToCollection(targetColId, draggedPaperId);
    await loadCollections();
    toast('Paper added to collection', 'success');
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function handleRemovePaper(collectionId, paperId) {
  if (!confirm('Remove this paper from the collection?')) return;
  try {
    await removePaperFromCollection(collectionId, paperId);
    await loadCollections();
    toast('Paper removed', 'success');
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function handleDelete(collectionId) {
  if (!confirm('Delete this collection? Papers will not be deleted.')) return;
  try {
    await deleteCollection(collectionId);
    await loadCollections();
    toast('Collection deleted', 'success');
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function handleExportZip(collectionId, collectionName) {
  try {
    await exportCollection(collectionId, `collection_${collectionName.replace(/\s+/g, '_')}.zip`);
    toast('Collection exported', 'success');
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function handleExportBibtex(collectionId, collectionName) {
  try {
    await exportPapersByCollection(collectionId, `collection_${collectionName.replace(/\s+/g, '_')}.bib`);
    toast('Collection exported as BibTeX', 'success');
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function exportPapersByCollection(collectionId, filename) {
  const { baseUrl, token } = getConfig();
  const h = { Accept: '*/*' };
  if (token) h['Authorization'] = token.startsWith('Bearer ') ? token : `Bearer ${token}`;
  const res = await fetch(`${baseUrl}/api/v1/collections/${collectionId}/export?format=bibtex`, { headers: h });
  if (!res.ok) {
    let detail = '';
    try {
      const err = await res.json();
      detail = err.detail || err.title || err.message || JSON.stringify(err);
    } catch {
      detail = res.statusText;
    }
    throw new ApiError(res.status, detail);
  }
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

function showCollectionModal(collection = null) {
  const modal = h('div', { className: 'modal-backdrop', id: 'collection-modal' });
  modal.innerHTML = `
    <div class="modal">
      <div class="modal-header">
        <h3>${collection ? 'Edit Collection' : 'New Collection'}</h3>
        <button class="modal-close">&times;</button>
      </div>
      <div class="modal-body">
        <div class="form-group">
          <label>Name</label>
          <input type="text" id="coll-name" class="input" value="${escapeHtml(collection?.name || '')}" />
        </div>
        <div class="form-group">
          <label>Description</label>
          <textarea id="coll-desc" class="input" rows="3">${escapeHtml(collection?.description || '')}</textarea>
        </div>
        <div class="form-group">
          <label>
            <input type="checkbox" id="coll-shared" ${collection?.isShared ? 'checked' : ''} />
            Shared with others
          </label>
        </div>
      </div>
      <div class="modal-footer">
        <button class="btn btn-ghost modal-close">Cancel</button>
        <button class="btn btn-primary" id="save-coll">Save</button>
      </div>
    </div>
  `;

  modal.querySelectorAll('.modal-close').forEach(btn => btn.addEventListener('click', () => modal.remove()));
  modal.addEventListener('click', (e) => { if (e.target === modal) modal.remove(); });

  document.body.appendChild(modal);

  document.getElementById('save-coll').addEventListener('click', async () => {
    const name = document.getElementById('coll-name').value.trim();
    if (!name) { toast('Name is required', 'error'); return; }
    const description = document.getElementById('coll-desc').value.trim();
    const isShared = document.getElementById('coll-shared').checked;

    try {
      if (collection) {
        await updateCollection(collection.id, { name, description, isShared });
      } else {
        await createCollection({ name, description, isShared });
      }
      modal.remove();
      await loadCollections();
      toast('Collection saved', 'success');
    } catch (err) {
      toast(err.message, 'error');
    }
  });
}

function showAddPaperModal(collection) {
  const availablePapers = allPapers.filter(p => !collections.some(c => c.id === collection.id && c.paperCount > 0));

  const modal = h('div', { className: 'modal-backdrop', id: 'add-paper-modal' });
  modal.innerHTML = `
    <div class="modal">
      <div class="modal-header">
        <h3>Add Paper to ${escapeHtml(collection.name)}</h3>
        <button class="modal-close">&times;</button>
      </div>
      <div class="modal-body">
        <div id="paper-list" class="paper-select-list"></div>
      </div>
      <div class="modal-footer">
        <button class="btn btn-ghost modal-close">Cancel</button>
      </div>
    </div>
  `;

  modal.querySelectorAll('.modal-close').forEach(btn => btn.addEventListener('click', () => modal.remove()));
  modal.addEventListener('click', (e) => { if (e.target === modal) modal.remove(); });

  document.body.appendChild(modal);

  const listEl = document.getElementById('paper-list');

  if (allPapers.length === 0) {
    listEl.appendChild(h('p', { className: 'text-secondary' }, 'No papers available'));
    return;
  }

  for (const paper of allPapers) {
    const item = h('div', {
      className: 'paper-select-item',
      onClick: async () => {
        try {
          await addPaperToCollection(collection.id, paper.id);
          modal.remove();
          await loadCollections();
          toast('Paper added', 'success');
        } catch (err) {
          toast(err.message, 'error');
        }
      }
    },
      h('div', { className: 'paper-select-title' }, paper.title),
      h('div', { className: 'paper-select-meta' }, formatAuthors(paper.authors, 2))
    );
    listEl.appendChild(item);
  }
}

function escapeHtml(str) {
  if (!str) return '';
  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}