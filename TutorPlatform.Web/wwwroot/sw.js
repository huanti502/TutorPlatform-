// Service Worker - Gia Sư Việt PWA
// Chiến lược: network-first cho điều hướng (web động), cache-first cho tài nguyên tĩnh.
const CACHE = 'giasuviet-v1';
const OFFLINE_URL = '/offline.html';
const PRECACHE = [OFFLINE_URL, '/icons/icon-192.png', '/css/site.css', '/css/ui-polish.css'];

self.addEventListener('install', e => {
    e.waitUntil(caches.open(CACHE).then(c => c.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', e => {
    const req = e.request;
    if (req.method !== 'GET') return;

    // Điều hướng trang: mạng trước, mất mạng -> trang offline
    if (req.mode === 'navigate') {
        e.respondWith(fetch(req).catch(() => caches.match(OFFLINE_URL)));
        return;
    }

    // Tĩnh (css/js/ảnh/font): cache trước, thiếu thì tải và cất vào cache
    const dest = req.destination;
    if (['style', 'script', 'image', 'font'].includes(dest)) {
        e.respondWith(
            caches.match(req).then(hit => hit || fetch(req).then(res => {
                if (res.ok && new URL(req.url).origin === location.origin) {
                    const clone = res.clone();
                    caches.open(CACHE).then(c => c.put(req, clone));
                }
                return res;
            }).catch(() => hit))
        );
    }
});
