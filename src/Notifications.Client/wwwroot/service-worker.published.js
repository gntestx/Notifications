self.importScripts('./service-worker-assets.js');

const cacheName = `notifications-cache-${self.assetsManifest.version}`;
const offlineAssets = self.assetsManifest.assets
    .filter(asset => !asset.url.endsWith('.map'))
    .map(asset => new Request(new URL(asset.url, self.registration.scope), { integrity: asset.hash, cache: 'no-cache' }));

self.addEventListener('install', event => event.waitUntil(
    caches.open(cacheName).then(cache => cache.addAll(offlineAssets))));

self.addEventListener('activate', event => event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.filter(key => key !== cacheName).map(key => caches.delete(key)));
    await self.clients.claim();
})()));

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') {
        return;
    }

    event.respondWith((async () => {
        if (event.request.mode === 'navigate') {
            try {
                return await fetch(event.request);
            } catch {
                return (await caches.match(new URL('index.html', self.registration.scope)))
                    ?? Response.error();
            }
        }

        return (await caches.match(event.request)) ?? fetch(event.request);
    })());
});

self.addEventListener('push', event => event.waitUntil(showPushNotification(event)));
self.addEventListener('notificationclick', event => event.waitUntil(openNotification(event)));

async function showPushNotification(event) {
    const payload = event.data?.json() ?? {};
    const base = self.registration.scope;
    await self.registration.showNotification(payload.title ?? 'Notifications', {
        body: payload.body ?? '',
        icon: `${base}icon-192.png`,
        badge: `${base}icon-192.png`,
        data: { url: payload.url ?? base },
        tag: 'notifications-message'
    });
}

async function openNotification(event) {
    event.notification.close();
    const target = new URL(event.notification.data?.url ?? '.', self.registration.scope).href;
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const existing = windows.find(client => client.url === target);
    if (existing) {
        await existing.focus();
        return;
    }
    await self.clients.openWindow(target);
}
