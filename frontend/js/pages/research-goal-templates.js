import { h, clear, loading, toast, emptyState } from '../components.js';

let selectedTemplate = null;

export async function render(container, { navigate, params, signal }) {
  clear(container);

  container.appendChild(
    h('div', { className: 'page-header' },
      h('h1', { className: 'page-title' }, 'Research Goal Templates'),
      h('p', { className: 'page-subtitle' }, 'Select a template to create a new research goal'),
    )
  );

  const grid = h('div', { className: 'card-grid', id: 'templates-grid' });
  container.appendChild(grid);

  const formContainer = h('div', { id: 'template-form-container' });
  container.appendChild(formContainer);

  loadTemplates();

  async function loadTemplates() {
    clear(grid);
    grid.appendChild(loading());

    try {
      const res = await fetch('/api/v1/research-goals/templates', {
        headers: { 'Accept': 'application/json' },
        signal,
      });

      if (!res.ok) throw new Error(`Failed to load templates: ${res.status}`);

      const templates = await res.json();
      clear(grid);

      if (templates.length === 0) {
        grid.appendChild(emptyState('No templates available', 'Templates will appear here once configured'));
        return;
      }

      for (const tmpl of templates) {
        const card = h('div', { className: 'card clickable', onClick: () => selectTemplate(tmpl) },
          h('div', { className: 'card-header' },
            h('h3', { className: 'card-title' }, tmpl.name),
            h('span', { className: 'badge' }, tmpl.goalType)
          ),
          h('p', { className: 'card-description' }, tmpl.description || 'No description')
        );
        grid.appendChild(card);
      }

    } catch (err) {
      if (err.name === 'AbortError') return;
      clear(grid);
      grid.appendChild(emptyState('Error loading templates', err.message));
      toast(err.message, 'error');
    }
  }

  function selectTemplate(template) {
    selectedTemplate = template;
    clear(formContainer);

    const params = template.parameters ? JSON.parse(template.parameters) : [];
    const form = h('div', { className: 'modal-backdrop', onClick: (e) => { if (e.target.classList.contains('modal-backdrop')) closeForm(); } },
      h('div', { className: 'modal' },
        h('div', { className: 'modal-header' },
          h('h2', { className: 'modal-title' }, `Create: ${template.name}`),
          h('button', { className: 'modal-close', onClick: closeForm }, '\u00D7')
        ),
        h('div', { className: 'modal-body' },
          h('p', { className: 'text-secondary mb-4' }, 'Fill in the parameters below to generate your research goal.'),
          h('div', { id: 'param-fields' }),
        ),
        h('div', { className: 'modal-footer' },
          h('button', { className: 'btn btn-secondary', onClick: closeForm }, 'CANCEL'),
          h('button', { className: 'btn btn-primary', onClick: () => submitForm(template) }, 'CREATE GOAL')
        )
      )
    );

    formContainer.appendChild(form);

    const paramFields = document.getElementById('param-fields');
    if (params.length === 0) {
      paramFields.appendChild(h('p', { className: 'text-secondary' }, 'This template has no parameters. The goal will be created with default values.'));
    } else {
      for (const p of params) {
        paramFields.appendChild(
          h('div', { className: 'form-group' },
            h('label', { className: 'form-label', for: `param-${p}` }, formatParamName(p)),
            h('input', { type: 'text', id: `param-${p}`, className: 'input', placeholder: `Enter ${formatParamName(p)}...` })
          )
        );
      }
    }

    const firstInput = paramFields.querySelector('input');
    if (firstInput) setTimeout(() => firstInput.focus(), 100);
  }

  function closeForm() {
    selectedTemplate = null;
    clear(formContainer);
  }

  function formatParamName(param) {
    return param.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
  }

  async function submitForm(template) {
    const params = template.parameters ? JSON.parse(template.parameters) : [];
    const paramValues = {};

    for (const p of params) {
      const input = document.getElementById(`param-${p}`);
      if (input) {
        paramValues[p] = input.value.trim();
      }
    }

    const missing = params.filter(p => !paramValues[p]);
    if (missing.length > 0) {
      toast(`Missing required parameters: ${missing.join(', ')}`, 'error');
      return;
    }

    try {
      const res = await fetch('/api/v1/research-goals/from-template', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
        body: JSON.stringify({
          templateId: template.id,
          name: template.name,
          parameters: paramValues,
        }),
      });

      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.detail || 'Failed to create goal');
      }

      const result = await res.json();
      toast('Research goal created', 'success');
      closeForm();
      navigate(`/jobs/${result.jobId}`);

    } catch (err) {
      if (err.name === 'AbortError') return;
      toast(err.message, 'error');
    }
  }
}

export function cleanup() {
  selectedTemplate = null;
}