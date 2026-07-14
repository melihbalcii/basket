import { getStore } from "@netlify/blobs";

// Vinyl League skor tablosu (serverless). Depolama: Netlify Blobs (ekstra hesap/anahtar yok).
// GET  /api/leaderboard?mode=classic        -> { mode, entries:[{id?,name,score,ts}] }  (ilk 50)
// POST /api/leaderboard  {id,name,score,mode} -> { mode, rank, entries }  (oyuncunun en iyisi tutulur)
// Güvenlik: skor üst sınırı + isim temizleme (basit hile önleme; arkadaş oyunu).

const MAX_ENTRIES = 200;   // modda saklanan azami kayıt
const MAX_SCORE = 2000;    // makul tavan (absürt skorları kes)
const MODES = new Set(["classic", "timed", "survival"]);

const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
  "Content-Type": "application/json; charset=utf-8",
};

const clean = (s, n) => String(s ?? "").replace(/[<>&"'`\\/]/g, "").replace(/\s+/g, " ").trim().slice(0, n);
const json = (obj, status = 200) => new Response(JSON.stringify(obj), { status, headers: CORS });

export default async (req) => {
  if (req.method === "OPTIONS") return new Response("", { headers: CORS });

  const store = getStore("leaderboard");
  const url = new URL(req.url);
  const qMode = url.searchParams.get("mode");
  const mode = MODES.has(qMode) ? qMode : "classic";

  if (req.method === "GET") {
    const list = (await store.get(mode, { type: "json" })) || [];
    return json({ mode, entries: list.slice(0, 50) });
  }

  if (req.method === "POST") {
    let body;
    try { body = await req.json(); } catch { return json({ error: "bad json" }, 400); }

    const m = MODES.has(body.mode) ? body.mode : "classic";
    const name = clean(body.name, 16) || "Anonim";
    const id = clean(body.id, 40) || name;
    let score = Math.floor(Number(body.score));
    if (!Number.isFinite(score) || score < 0) return json({ error: "bad score" }, 400);
    score = Math.min(score, MAX_SCORE);

    const list = (await store.get(m, { type: "json" })) || [];
    const i = list.findIndex((e) => e.id === id);
    if (i >= 0) {
      list[i].name = name;                               // isim güncellenebilir
      if (score > list[i].score) { list[i].score = score; list[i].ts = Date.now(); }
    } else {
      list.push({ id, name, score, ts: Date.now() });
    }
    list.sort((a, b) => b.score - a.score || a.ts - b.ts);
    const trimmed = list.slice(0, MAX_ENTRIES);
    await store.setJSON(m, trimmed);

    const rank = trimmed.findIndex((e) => e.id === id) + 1;
    return json({ mode: m, rank, entries: trimmed.slice(0, 50) });
  }

  return json({ error: "method not allowed" }, 405);
};

export const config = { path: "/api/leaderboard" };
