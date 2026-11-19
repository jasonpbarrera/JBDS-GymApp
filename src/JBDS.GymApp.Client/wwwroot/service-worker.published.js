// Published PWA service worker with immediate activation, /api/* bypass, and network-first freshness.
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    self.skipWaiting();
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
    await self.clients.claim();
}

async function onFetch(event) {
    const requestUrl = new URL(event.request.url);

    // Always bypass service worker cache for API calls and non-GET requests
    if (event.request.method !== 'GET' || requestUrl.pathname.startsWith('/api/')) {
        return fetch(event.request);
    }

    // Network-first (with cache fallback) for navigations, CSS, and appsettings.json
    const isNetworkFirst =
        event.request.mode === 'navigate' ||
        requestUrl.pathname.endsWith('.css') ||
        requestUrl.pathname.endsWith('appsettings.json');

    const cache = await caches.open(cacheName);

    if (isNetworkFirst) {
        try {
            const fresh = await fetch(event.request);
            if (fresh && fresh.ok) {
                cache.put(event.request, fresh.clone());
            }
            return fresh;
        } catch {
            const fallbackRequest = event.request.mode === 'navigate' ? 'index.html' : event.request;
            const cached = await cache.match(fallbackRequest);
            if (cached) return cached;
            throw new Error('Offline and no cached asset available.');
        }
    }

    // Cache-first for fingerprinted Blazor WASM framework & static assets
    const shouldServeIndexHtml = event.request.mode === 'navigate' && !manifestUrlList.some(url => url === event.request.url);
    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cachedResponse = await cache.match(request);
    return cachedResponse || fetch(event.request);
}
