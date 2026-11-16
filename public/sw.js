/*
 * Asset360 service worker: desktop notifications for IoT alerts (Web Push from the Tracking service).
 * Shows a system notification with Acknowledge / Remind me even when no Asset360 tab is open, forwards the alert to
 * open tabs (in-app list + sound), and sends the button the user clicked back to the Tracking service
 * (POST {api}/api/push/actions with the alert's action token; no sign-in token is needed or available here).
 */
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

self.addEventListener('push', (event) => {
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch {
    data = { title: 'Asset360 alert', body: event.data ? event.data.text() : '' };
  }
  if (data.type !== 'iot-alert') return;

  const repeat = data.number > 1 ? ` (reminder ${data.number - 1})` : '';
  event.waitUntil(
    Promise.all([
      self.registration.showNotification(data.title + repeat, {
        body: data.body,
        icon: '/PurpleIQLOGOPurpleBright.png',
        badge: '/PurpleIQLOGOPurpleBright.png',
        tag: `iot-alert-${data.alertId}`,
        renotify: true,
        requireInteraction: true,
        silent: data.sound === false,
        timestamp: data.raisedAt ? Date.parse(data.raisedAt) : Date.now(),
        actions: [
          { action: 'acknowledge', title: 'Acknowledge' },
          { action: 'remind', title: 'Remind me' },
        ],
        data,
      }),
      // open tabs show it in the app and play the sound
      self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((tabs) => tabs.forEach((tab) => tab.postMessage(data))),
    ])
  );
});

self.addEventListener('notificationclick', (event) => {
  const data = event.notification.data || {};
  event.notification.close();

  if (event.action === 'acknowledge' || event.action === 'remind') {
    event.waitUntil(
      self.registration.pushManager.getSubscription().then((sub) =>
        fetch(data.actionUrl, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ alertId: data.alertId, token: data.actionToken, action: event.action, endpoint: sub ? sub.endpoint : null }),
        }).catch(() => undefined)
      )
    );
    return;
  }

  // clicking the notification itself opens (or focuses) Geofence & Alerts
  const url = data.url || '/tracking-iot/geofence-alerts';
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((tabs) => {
      const open = tabs.find((t) => t.url.startsWith(self.location.origin));
      if (open) {
        open.focus();
        return open.navigate ? open.navigate(url).catch(() => undefined) : undefined;
      }
      return self.clients.openWindow(url);
    })
  );
});
