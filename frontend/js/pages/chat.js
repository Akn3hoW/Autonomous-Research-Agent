import { streamChat, postChat, escapeHtml } from '../api.js';
import {
  h, clear, loading, toast, emptyState
} from '../components.js';

let chatState = {
  messages: [],
  sources: [],
};

export async function render(container, { navigate, signal }) {
  clear(container);

  const wrapper = h('div', { className: 'chat-wrapper' });

  const header = h('div', { className: 'chat-header' });
  header.appendChild(h('h1', { className: 'page-title' }, 'Research Chat'));
  header.appendChild(h('p', { className: 'chat-subtitle' }, 'Ask questions about your research papers'));
  wrapper.appendChild(header);

  const messagesContainer = h('div', { className: 'chat-messages', id: 'chat-messages' });
  wrapper.appendChild(messagesContainer);

  const inputArea = h('div', { className: 'chat-input-area' });
  const inputRow = h('div', { className: 'chat-input-row' });

  const input = h('textarea', {
    className: 'input chat-input',
    placeholder: 'Ask a research question\u2026',
    rows: 1,
  });

  input.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      submitQuestion();
    }
  });

  input.addEventListener('input', () => {
    input.style.height = 'auto';
    input.style.height = Math.min(input.scrollHeight, 150) + 'px';
  });

  const submitBtn = h('button', {
    className: 'btn btn-primary chat-submit',
    type: 'button',
  }, 'Send');

  submitBtn.addEventListener('click', submitQuestion);

  inputRow.appendChild(input);
  inputRow.appendChild(submitBtn);
  inputArea.appendChild(inputRow);

  const sourcesToggle = h('div', { className: 'chat-sources-toggle' });
  sourcesToggle.innerHTML = `
    <label class="toggle-label">
      <input type="checkbox" id="show-sources" checked>
      <span>Show sources</span>
    </label>
  `;
  inputArea.appendChild(sourcesToggle);

  wrapper.appendChild(inputArea);
  container.appendChild(wrapper);

  requestAnimationFrame(() => input.focus());

  if (chatState.messages.length > 0) {
    renderMessages(messagesContainer);
  }

  async function submitQuestion() {
    const question = input.value.trim();
    if (!question) return;

    input.value = '';
    input.style.height = 'auto';

    chatState.messages.push({ role: 'user', content: question });
    renderMessages(messagesContainer);

    const userMsg = h('div', { className: 'chat-message user' });
    userMsg.appendChild(h('div', { className: 'chat-bubble' }, question));
    messagesContainer.appendChild(userMsg);
    scrollToBottom();

    const assistantMsg = h('div', { className: 'chat-message assistant' });
    const assistantBubble = h('div', { className: 'chat-bubble' });
    assistantBubble.appendChild(loading('Thinking'));
    assistantMsg.appendChild(assistantBubble);
    messagesContainer.appendChild(assistantMsg);
    scrollToBottom();

    try {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 120000);

      let fullResponse = '';
      const showSources = document.getElementById('show-sources')?.checked ?? true;

      if (showSources) {
        const result = await postChat({ question, topK: 10 }, { signal: controller.signal });
        clearTimeout(timeout);

        clear(assistantBubble);
        assistantBubble.appendChild(h('div', { className: 'chat-answer' }, escapeHtml(result.content) || ''));

        if (result.sources?.length > 0) {
          chatState.sources = result.sources;
          const sourcesSection = renderSources(result.sources);
          assistantBubble.appendChild(sourcesSection);
        }
      } else {
        const stream = await streamChat({ question, topK: 10 }, { signal: controller.signal });
        clearTimeout(timeout);

        clear(assistantBubble);

        for await (const chunk of stream) {
          fullResponse += chunk;
          clear(assistantBubble);
          assistantBubble.appendChild(h('div', { className: 'chat-answer' }, fullResponse));
          scrollToBottom();
        }
      }

      chatState.messages.push({ role: 'assistant', content: fullResponse || assistantBubble.textContent });
      scrollToBottom();

    } catch (err) {
      clear(assistantBubble);
      if (err.name === 'AbortError') {
        assistantBubble.appendChild(h('div', { className: 'chat-error' }, 'Request timed out'));
      } else {
        assistantBubble.appendChild(h('div', { className: 'chat-error' }, err.message));
      }
      toast(err.message, 'error');
    }
  }

  function renderMessages(container) {
    clear(container);
    for (const msg of chatState.messages) {
      const msgEl = h('div', { className: `chat-message ${msg.role}` });
      msgEl.appendChild(h('div', { className: 'chat-bubble' }, msg.content));
      container.appendChild(msgEl);
    }
  }

  function renderSources(sources) {
    const section = h('div', { className: 'chat-sources' });
    section.appendChild(h('h4', { className: 'chat-sources-title' }, 'Sources'));

    for (const source of sources) {
      const card = h('div', { className: 'source-card' });

      const titleRow = h('div', { className: 'source-card-header' });
      titleRow.appendChild(h('a', {
        className: 'source-card-title',
        href: `#/papers/${source.paperId}`,
        onClick: (e) => { e.preventDefault(); navigate(`/papers/${source.paperId}`); },
      }, source.paperTitle || 'Unknown Paper'));
      titleRow.appendChild(h('span', { className: 'source-card-score' },
        `${(source.relevanceScore * 100).toFixed(0)}%`));
      card.appendChild(titleRow);

      card.appendChild(h('p', { className: 'source-card-text' }, source.chunkText || ''));

      section.appendChild(card);
    }

    return section;
  }

  function scrollToBottom() {
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
  }
}
