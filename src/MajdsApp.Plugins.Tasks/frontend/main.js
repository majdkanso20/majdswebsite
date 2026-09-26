// The Tasks plugin's pre-built UI (P5 FR-PLUG-014/015): an ES module that defines a custom element. The shell creates the element, sets
// `hostContext` ({ contract, pluginId, apiBaseUrl, language, getAccessToken }) and puts it on the page inside a shadow root, which keeps this
// plugin's styles from leaking into the host (FR-PLUG-018). The host's theme colours are CSS custom properties, so they pass through.
class TasksOverview extends HTMLElement {
  connectedCallback() {
    if (this.shadowRoot) return;
    const root = this.attachShadow({ mode: 'open' });
    const context = this.hostContext;
    const arabic = context?.language === 'ar';
    root.innerHTML = `<section class="tasks-plugin"><h2>${arabic ? 'نظرة عامة على المهام' : 'Tasks overview'}</h2><ul></ul><p class="status"></p></section>`;
    const list = root.querySelector('ul');
    const status = root.querySelector('.status');

    fetch(`${context.apiBaseUrl}/tasks/list?page=1&pageSize=10`, { headers: { Authorization: `Bearer ${context.getAccessToken()}` } })
      .then((response) => response.json())
      .then((body) => {
        const items = body?.data?.items ?? [];
        status.textContent = items.length === 0 ? (arabic ? 'لا توجد مهام.' : 'No tasks yet.') : '';
        for (const task of items) {
          const item = document.createElement('li');
          item.textContent = `${task.title} — ${task.status}`;
          list.append(item);
        }
      })
      .catch(() => { status.textContent = arabic ? 'تعذّر تحميل المهام.' : 'Could not load the tasks.'; });
  }
}

if (!customElements.get('majds-tasks-overview')) customElements.define('majds-tasks-overview', TasksOverview);
