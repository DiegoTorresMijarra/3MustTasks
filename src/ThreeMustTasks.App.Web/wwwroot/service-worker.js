// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Toque en una notificación (issue 21): enfoca la app abierta o la abre; con ruta, navega a ella.
self.addEventListener('notificationclick', event => {
    event.notification.close();
    const route = (event.notification.data && event.notification.data.route) || '';
    event.waitUntil((async () => {
        const url = new URL(route, self.registration.scope).href;
        const all = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        const open = all[0];
        if (open) { await open.focus(); if (route && open.navigate) await open.navigate(url); }
        else await self.clients.openWindow(url);
    })());
});
