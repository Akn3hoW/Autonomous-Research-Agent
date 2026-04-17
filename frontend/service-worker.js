const CACHE_NAME = 'ara-cache-v1';
const STATIC_CACHE_NAME = 'ara-static-v1';
const API_CACHE_NAME = 'ara-api-v1';

const STATIC_ASSETS = [
  '/',
  '/index.html',
  '/style.css',
  '/js/app.js',
  '/manifest.json',
];

const API_CACHE_TTL = 5 * 60 * 1000;

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(STATIC_CACHE_NAME)
      .then((cache) => {
        return cache.addAll(STATIC_ASSETS);
      })
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((cacheNames) => {
        return Promise.all(
          cacheNames
            .filter((name) => name !== STATIC_CACHE_NAME && name !== API_CACHE_NAME)
            .map((name) => caches.delete(name))
        );
      })
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);

  if (url.pathname.startsWith('/api/')) {
    event.respondWith(handleApiRequest(request, url));
    return;
  }

  if (request.mode === 'navigate') {
    event.respondWith(handleNavigationRequest(request));
    return;
  }

  if (request.destination === 'style' || request.destination === 'script' || request.destination === 'font') {
    event.respondWith(handleStaticAsset(request));
    return;
  }

  if (request.destination === 'image') {
    event.respondWith(handleImageRequest(request));
    return;
  }
});

async function handleApiRequest(request, url) {
  const cache = await caches.open(API_CACHE_NAME);

  if (request.method === 'GET') {
    const cachedResponse = await cache.match(request);
    if (cachedResponse) {
      const cachedTime = cachedResponse.headers.get('x-cached-time');
      if (cachedTime && Date.now() - parseInt(cachedTime) < API_CACHE_TTL) {
        return cachedResponse;
      }
    }

    try {
      const response = await fetch(request);
      if (response.ok) {
        const responseToCache = response.clone();
        const headers = new Headers(responseToCache.headers);
        headers.set('x-cached-time', Date.now().toString());
        const body = await responseToCache.arrayBuffer();
        const newResponse = new Response(body, {
          status: responseToCache.status,
          statusText: responseToCache.statusText,
          headers: headers
        });
        cache.put(request, newResponse);
      }
      return response;
    } catch (error) {
      if (cachedResponse) {
        return cachedResponse;
      }
      return new Response(JSON.stringify({ error: 'Offline' }), {
        status: 503,
        headers: { 'Content-Type': 'application/json' }
      });
    }
  }

  if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method)) {
    try {
      return await fetch(request);
    } catch (error) {
      if (!navigator.onLine) {
        await queueBackgroundSync(request);
        return new Response(JSON.stringify({ error: 'Queued for sync', queued: true }), {
          status: 202,
          headers: { 'Content-Type': 'application/json' }
        });
      }
      throw error;
    }
  }
}

async function handleNavigationRequest(request) {
  try {
    const response = await fetch(request);
    return response;
  } catch (error) {
    const cachedResponse = await caches.match('/index.html');
    if (cachedResponse) {
      return cachedResponse;
    }
    throw error;
  }
}

async function handleStaticAsset(request) {
  const cachedResponse = await caches.match(request);
  if (cachedResponse) {
    fetch(request)
      .then((response) => {
        if (response.ok) {
          caches.open(STATIC_CACHE_NAME)
            .then((cache) => cache.put(request, response));
        }
      })
      .catch(() => {});
    return cachedResponse;
  }

  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(STATIC_CACHE_NAME);
      cache.put(request, response.clone());
    }
    return response;
  } catch (error) {
    return new Response('Offline', { status: 503 });
  }
}

async function handleImageRequest(request) {
  const cachedResponse = await caches.match(request);
  if (cachedResponse) {
    return cachedResponse;
  }

  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(CACHE_NAME);
      cache.put(request, response.clone());
    }
    return response;
  } catch (error) {
    return new Response('', { status: 503 });
  }
}

async function queueBackgroundSync(request) {
  const data = await request.json();
  const queuedRequests = await getQueuedRequests();
  queuedRequests.push({
    url: request.url,
    method: request.method,
    data,
    timestamp: Date.now()
  });
  await saveQueuedRequests(queuedRequests);

  self.registration.sync.register('ara-api-sync');
}

async function getQueuedRequests() {
  const db = await openDB();
  const tx = db.transaction('queued-requests', 'readonly');
  const store = tx.objectStore('queued-requests');
  return new Promise((resolve, reject) => {
    const request = store.getAll();
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function saveQueuedRequests(requests) {
  const db = await openDB();
  const tx = db.transaction('queued-requests', 'readwrite');
  const store = tx.objectStore('queued-requests');
  await store.clear();
  for (const req of requests) {
    store.add(req);
  }
}

async function openDB() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open('ara-offline', 1);
    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result);
    request.onupgradeneeded = (event) => {
      const db = event.target.result;
      if (!db.objectStoreNames.contains('queued-requests')) {
        db.createObjectStore('queued-requests', { keyPath: 'timestamp' });
      }
    };
  });
}

self.addEventListener('sync', (event) => {
  if (event.tag === 'ara-api-sync') {
    event.waitUntil(processQueuedRequests());
  }
});

async function processQueuedRequests() {
  const queuedRequests = await getQueuedRequests();
  if (queuedRequests.length === 0) return;

  const successfulRequests = [];

  for (const req of queuedRequests) {
    try {
      const response = await fetch(req.url, {
        method: req.method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req.data)
      });

      if (response.ok) {
        successfulRequests.push(req);
      }
    } catch (error) {
      console.warn('Failed to sync request:', req, error);
    }
  }

  const remaining = queuedRequests.filter(
    (req) => !successfulRequests.find((s) => s.timestamp === req.timestamp)
  );
  await saveQueuedRequests(remaining);

  if (remaining.length === 0 && successfulRequests.length > 0) {
    self.clients.matchAll()
      .then((clients) => {
        clients.forEach((client) => {
          client.postMessage({
            type: 'SYNC_COMPLETE',
            syncedCount: successfulRequests.length
          });
        });
      });
  }
}

self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});

let deferredInstallPrompt = null;

self.addEventListener('beforeinstallprompt', (event) => {
  event.preventDefault();
  deferredInstallPrompt = event;
  self.clients.matchAll()
    .then((clients) => {
      clients.forEach((client) => {
        client.postMessage({ type: 'INSTALL_PROMPT_AVAILABLE' });
      });
    });
});

self.addEventListener('install', () => {
  deferredInstallPrompt = null;
});

export {};