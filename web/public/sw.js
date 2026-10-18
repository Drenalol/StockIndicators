/* Service worker: Web Push delivery for Stock Indicators alerts */

self.addEventListener('activate', () => {
  console.log('[SW] activated');
});

self.addEventListener('push', (event) => {
  console.log('[SW] push received:', event.data ? event.data.text() : '(no payload)');

  let data = { title: 'Stock Indicators', body: '' };
  try {
    if (event.data) data = event.data.json();
  } catch {
    if (event.data) data.body = event.data.text();
  }

  event.waitUntil(
    self.registration
      .showNotification(data.title || 'Stock Indicators', {
        body: data.body || '',
        icon: data.icon || '/icons/icon-192.png',
        badge: data.icon || '/icons/icon-192.png',
        // Signed trigger chart image (absent when the API is not configured with a public URL)
        image: data.image,
        tag: data.tag || 'stock-indicators',
        data: data.data || {},
      })
      .then(() => console.log('[SW] notification shown'))
      .catch((err) => console.error('[SW] showNotification failed:', err))
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  const path = (event.notification.data && event.notification.data.path) || '/';

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
      for (const client of clients) {
        if (client.url.includes(path) && 'focus' in client) {
          return client.focus();
        }
      }
      return self.clients.openWindow(path);
    })
  );
});
