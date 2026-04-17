const STORAGE_KEY = 'ara_store_v1';

const initialState = {
  auth: {
    user: null,
    token: null,
    isAuthenticated: false,
  },
  papers: {
    items: [],
    currentPaper: null,
    loading: false,
    error: null,
  },
  jobs: {
    items: [],
    currentJob: null,
    loading: false,
  },
  notifications: {
    items: [],
    unreadCount: 0,
  },
  ui: {
    sidebarOpen: true,
    theme: 'light',
    activeModal: null,
  },
};

function createStore() {
  let state = loadState();
  const subscribers = new Map();
  let subscriberIdCounter = 0;

  function loadState() {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved) {
        const parsed = JSON.parse(saved);
        return deepMerge(initialState, parsed);
      }
    } catch (e) {
      console.warn('Failed to load state from storage', e);
    }
    return deepMerge({}, initialState);
  }

  function saveState() {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    } catch (e) {
      console.warn('Failed to save state to storage', e);
    }
  }

  function deepMerge(target, source) {
    for (const key of Object.keys(source)) {
      if (source[key] && typeof source[key] === 'object' && !Array.isArray(source[key])) {
        target[key] = deepMerge(target[key] || {}, source[key]);
      } else {
        target[key] = source[key];
      }
    }
    return target;
  }

  function notify(slice) {
    for (const [id, callback] of subscribers) {
      try {
        callback(state, slice);
      } catch (e) {
        console.error('Subscriber error', id, e);
      }
    }
  }

  return {
    getState() {
      return state;
    },

    setState(updater, slice = 'ui') {
      const updates = typeof updater === 'function' ? updater(state) : updater;
      for (const [key, value] of Object.entries(updates)) {
        if (state[key] !== undefined) {
          state[key] = value;
        }
      }
      saveState();
      notify(slice);
      return state;
    },

    setSlice(sliceName, data, persist = true) {
      if (state[sliceName] !== undefined) {
        state[sliceName] = { ...state[sliceName], ...data };
        if (persist) saveState();
        notify(sliceName);
      }
      return state;
    },

    subscribe(callback) {
      const id = ++subscriberIdCounter;
      subscribers.set(id, callback);
      return () => subscribers.delete(id);
    },

    persist() {
      saveState();
    },

    reset() {
      state = deepMerge({}, initialState);
      saveState();
      notify('*');
    },
  };
}

export const store = createStore();

export function useStore(selector, slice = null) {
  if (typeof selector === 'string') {
    const sliceName = selector;
    return useStore((state) => state[sliceName], sliceName);
  }

  let currentSelector = selector;
  let currentSlice = slice;
  let currentValue = currentSelector(store.getState());

  const subscribe = store.subscribe((state, changedSlice) => {
    if (currentSlice && changedSlice !== '*' && changedSlice !== currentSlice) return;
    const newValue = currentSelector(state);
    if (newValue !== currentValue) {
      currentValue = newValue;
    }
  });

  return { value: currentValue, subscribe };
}

export function getState() {
  return store.getState();
}

export function setState(updater, slice) {
  return store.setState(updater, slice);
}

export function setSlice(sliceName, data, persist) {
  return store.setSlice(sliceName, data, persist);
}

export function subscribe(callback) {
  return store.subscribe(callback);
}

export default store;