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
