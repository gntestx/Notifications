const shellCache = 'notifications-shell-v1';
const shellAssets = [
    '/',
    '/manifest.webmanifest',
    '/icons/app-icon.svg',
    '/icons/app-icon-192.png',
    '/icons/app-icon-512.png'
];

self.addEventListener('install', event => {
    event.waitUntil(caches.open(shellCache).then(cache => cache.addAll(shellAssets)));
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(key => key !== shellCache).map(key => caches.delete(key))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET' || event.request.mode === 'navigate') return;
    event.respondWith(fetch(event.request).catch(() => caches.match(event.request)));
});

self.addEventListener('push', event => {
    let payload = { title: 'Notifications', body: 'Du har fått en ny notis.', url: '/' };
    if (event.data) {
        try {
            payload = { ...payload, ...event.data.json() };
        } catch {
            payload.body = event.data.text();
        }
    }

    event.waitUntil(self.registration.showNotification(payload.title, {
        body: payload.body,
        icon: '/icons/app-icon-192.png',
        badge: '/icons/app-icon-192.png',
        data: { url: payload.url || '/' },
        tag: 'notifications-message',
        renotify: true
    }));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = new URL(event.notification.data?.url || '/', self.location.origin).href;

    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(clients => {
            const existing = clients.find(client => new URL(client.url).origin === self.location.origin);
            if (existing) {
                existing.navigate(target);
                return existing.focus();
            }
            return self.clients.openWindow(target);
        })
    );
});
