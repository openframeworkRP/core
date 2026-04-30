#!/usr/bin/env node
/**
 * check_fab_studios.mjs
 *
 * Vérifie les nouvelles publications de studios suivis sur Fab.com.
 * Lit les studios depuis la base de données du hub, compare avec les assets
 * déjà vus, et envoie une notification Discord pour chaque nouvel asset.
 *
 * Usage:
 *   node scripts/check_fab_studios.mjs
 *   node scripts/check_fab_studios.mjs --dry-run   (affiche sans envoyer)
 *
 * Variables d'environnement (optionnelles):
 *   DISCORD_WEBHOOK_URL   Webhook Discord pour les notifications
 *   HUB_API_URL           URL de base de l'API (défaut: http://localhost:3001)
 */

import { readFileSync, writeFileSync, existsSync } from "fs";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, "..");

// ── Config ────────────────────────────────────────────────────────────────
const DRY_RUN        = process.argv.includes("--dry-run");
const HUB_API_URL    = process.env.HUB_API_URL    || "http://localhost:3001";
const DISCORD_WEBHOOK= process.env.DISCORD_WEBHOOK_URL || "";
const STATE_FILE     = join(__dirname, "fab_studios_state.json");

// ── Helpers ───────────────────────────────────────────────────────────────
function log(...args) { console.log(`[fab-studios]`, ...args); }
function warn(...args) { console.warn(`[fab-studios] ⚠`, ...args); }

function loadState() {
  if (!existsSync(STATE_FILE)) return {};
  try { return JSON.parse(readFileSync(STATE_FILE, "utf8")); }
  catch { return {}; }
}

function saveState(state) {
  if (!DRY_RUN) writeFileSync(STATE_FILE, JSON.stringify(state, null, 2));
}

// Fetch hub data from local API (requires the backend to be running)
async function fetchHubData() {
  const res = await fetch(`${HUB_API_URL}/api/hub`);
  if (!res.ok) throw new Error(`Hub API error: ${res.status}`);
  return res.json();
}

// Fetch recent listings from Fab.com for a seller username
async function fetchFabListings(fabUsername) {
  const url = `https://www.fab.com/i/listings?seller_username=${encodeURIComponent(fabUsername)}&sort_by=-published_at&first=20`;
  const res = await fetch(url, {
    headers: {
      "Accept": "application/json",
      "User-Agent": "Mozilla/5.0 (compatible; StudioTracker/1.0)",
    },
  });
  if (!res.ok) throw new Error(`Fab API error: ${res.status}`);
  const data = await res.json();
  return (data.results || []).map(a => ({
    id: a.uid || a.id || a.slug,
    title: a.title || a.name || "Asset sans titre",
    url: `https://www.fab.com/listings/${a.slug || a.uid}`,
    price: a.price ? `${(a.price.amount / 100).toFixed(2)} ${a.price.currency}` : "Gratuit",
    thumbnail: a.thumbnailUrl || a.thumbnail_url || null,
    publishedAt: a.publishedAt || a.published_at || null,
  }));
}

// Send a Discord embed for new assets
async function sendDiscordNotif(studio, newAssets) {
  if (!DISCORD_WEBHOOK) {
    log(`  → Discord webhook non configuré, notification ignorée.`);
    return;
  }

  const embeds = newAssets.slice(0, 5).map(asset => ({
    title: asset.title,
    url: asset.url,
    color: parseInt(studio.color?.replace("#", "") || "e07b39", 16),
    thumbnail: asset.thumbnail ? { url: asset.thumbnail } : undefined,
    fields: [
      { name: "Prix", value: asset.price, inline: true },
      { name: "Studio", value: studio.name, inline: true },
      ...(asset.publishedAt ? [{ name: "Publié le", value: new Date(asset.publishedAt).toLocaleDateString("fr-FR"), inline: true }] : []),
    ],
    footer: { text: `fab.com/sellers/${studio.fabUsername}` },
    timestamp: asset.publishedAt || new Date().toISOString(),
  }));

  const body = {
    username: "Fab Studio Tracker",
    avatar_url: "https://www.fab.com/favicon.ico",
    content: `🆕 **${newAssets.length} nouvel${newAssets.length > 1 ? "s asset" : " asset"}** de **${studio.name}**`,
    embeds,
  };

  const res = await fetch(DISCORD_WEBHOOK, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  if (!res.ok) warn(`Discord webhook failed: ${res.status}`);
  else log(`  → Notification Discord envoyée (${newAssets.length} asset${newAssets.length > 1 ? "s" : ""})`);
}

// ── Main ──────────────────────────────────────────────────────────────────
async function main() {
  log(DRY_RUN ? "Mode dry-run activé" : "Démarrage...");

  // Load hub data to get tracked studios
  let hubData;
  try {
    hubData = await fetchHubData();
  } catch (e) {
    warn(`Impossible de contacter l'API hub: ${e.message}`);
    warn(`Assurez-vous que le backend tourne sur ${HUB_API_URL}`);
    process.exit(1);
  }

  const studios = hubData?.fabStudios || [];
  if (studios.length === 0) {
    log("Aucun studio suivi dans le hub. Ajoutez des studios via l'interface admin.");
    return;
  }
  log(`${studios.length} studio${studios.length > 1 ? "s" : ""} trouvé${studios.length > 1 ? "s" : ""}`);

  // Load local state (tracks which asset IDs we've already notified about)
  const state = loadState();
  let totalNew = 0;

  for (const studio of studios) {
    log(`\nVérification: ${studio.name} (@${studio.fabUsername})`);

    let assets;
    try {
      assets = await fetchFabListings(studio.fabUsername);
      log(`  → ${assets.length} asset${assets.length !== 1 ? "s" : ""} trouvé${assets.length !== 1 ? "s" : ""}`);
    } catch (e) {
      warn(`  → Erreur: ${e.message}`);
      continue;
    }

    // Compare with already-seen IDs (from hub seenIds + local state)
    const knownIds = new Set([
      ...(studio.seenIds || []),
      ...(state[studio.id] || []),
    ]);

    const newAssets = assets.filter(a => a.id && !knownIds.has(a.id));
    log(`  → ${newAssets.length} nouveau${newAssets.length !== 1 ? "x" : ""}`);

    if (newAssets.length > 0) {
      totalNew += newAssets.length;

      for (const asset of newAssets) {
        log(`     • ${asset.title} — ${asset.price} — ${asset.url}`);
      }

      // Update local state
      const allIds = [...knownIds, ...newAssets.map(a => a.id)].filter(Boolean);
      state[studio.id] = allIds;

      // Send Discord notification
      if (!DRY_RUN) {
        await sendDiscordNotif(studio, newAssets);
      }
    }
  }

  saveState(state);
  log(`\n✓ Terminé — ${totalNew} nouvel${totalNew !== 1 ? "s asset" : " asset"} au total`);

  // Summary output for cron logging
  if (totalNew > 0 && !DRY_RUN) {
    console.log(`[fab-studios] SUMMARY: ${totalNew} new assets found`);
  }
}

main().catch(e => { console.error(e); process.exit(1); });
