// Vinyl League service worker: ikinci açılış şimşek hızı + çevrimdışı destek.
// __BUILDVER__ her build'de BuildWebGL.cs tarafından değiştirilir -> yeni sürüm
// yayınlanınca eski önbellek otomatik silinir, oyuncu asla bayat oyun görmez.
const CACHE = "vinyl-league-__BUILDVER__";

self.addEventListener("install", (e) => self.skipWaiting());

self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", (e) => {
  const url = new URL(e.request.url);
  if (e.request.method !== "GET") return;
  if (url.pathname.startsWith("/api/")) return; // skor tablosu HER ZAMAN canlı

  // Oyun dosyaları (Build/, TemplateData/, ikonlar): önce önbellek, yoksa indir + sakla.
  // index.html: önce ağ (yeni sürümü hemen al), çevrimdışıysa önbellekten aç.
  const isAsset = url.pathname.includes("/Build/") || url.pathname.includes("/TemplateData/")
    || url.pathname.includes("/icons/") || url.pathname.endsWith(".webmanifest");

  if (isAsset) {
    e.respondWith(
      caches.open(CACHE).then((c) =>
        c.match(e.request).then((hit) =>
          hit || fetch(e.request).then((res) => {
            if (res.ok) c.put(e.request, res.clone());
            return res;
          })
        )
      )
    );
  } else {
    e.respondWith(
      fetch(e.request)
        .then((res) => {
          if (res.ok) caches.open(CACHE).then((c) => c.put(e.request, res.clone()));
          return res;
        })
        .catch(() => caches.match(e.request))
    );
  }
});
