import { useState, useEffect, useCallback, useRef, useMemo } from "react";
import { api } from "./api.js";
import { useAdminSocket } from "./useAdminSocket.js";
import {
  BarChart3, Columns3, List, Route, Lightbulb, Plus, RotateCw,
  ArrowUp, Minus, ArrowDown, Check, CheckCircle, X, Trash2, Save,
  Pen, Image, SquareCheck, Square, ThumbsUp, ThumbsDown,
  ArrowRightToLine, Reply, SendHorizontal, MessageCircle,
  Clock, Folder, Layers, Users, Eye, EyeOff, Inbox,
  ChevronUp, ChevronDown, Gamepad2, Rocket, User, Activity,
  MapPin, Circle, RectangleHorizontal, Pencil, ZoomIn, ZoomOut, Move, MousePointer, Undo2,
  Search, ExternalLink,
  RefreshCw, Loader2,
  ArrowUpNarrowWide, ArrowDownNarrowWide,
  Link2, Tag, FileUp,
  HardDrive, Download, FolderOpen, FolderSearch, Globe, ChevronRight, Package, Database,
  Copy, LayoutGrid, Rows3, Building2, Archive, Lock, AlertCircle, CheckCircle2, XCircle
} from "lucide-react";

// Icon helper — consistent sizing
const IC = ({ icon: Icon, size, style }) => <Icon size={size || 14} style={{ display: "inline-block", verticalAlign: "middle", ...style }} />;

// ============================================================
// SMALLBOX STUDIO — PROJECT HUB
// ============================================================

const STORAGE_KEY = "openframework-hub-data";

// --- DEFAULT DATA ---
// Les membres du hub sont les users admin (/api/users)
// Palette de couleurs attribuée par index
const MEMBER_COLORS = [
  "#5865f2", "#ed9121", "var(--brand-primary, #e07b39)", "#3e9041", "#9b59b6",
  "#e74c3c", "#e67e22", "#2ecc71", "#ae2121", "#00b5d8",
  "#f39c12", "#1abc9c", "#8e44ad", "#d35400", "#27ae60",
];

// Convertit un user DB en membre Hub (id = display_name en lowercase)
function dbUserToHub(u, idx) {
  return {
    id: u.display_name.toLowerCase().replace(/\s+/g, "_"),
    name: u.display_name,
    role: u.role,
    color: MEMBER_COLORS[idx % MEMBER_COLORS.length],
    steam_id64: u.steam_id || null,
    avatar: u.avatar || null,
  };
}

export const DEFAULT_MEMBERS = []; // plus utilisé, conservé pour compatibilité import

export const DEFAULT_PROJECTS = [
  { id: "sl-v1", name: "OpenFramework", icon: Gamepad2, color: "var(--brand-primary, #e07b39)", deadline: null },
  { id: "sw-rp", name: "Star Wars RP", icon: Rocket, color: "#2ecc71", deadline: null },
  { id: "ph", name: "PropHunt", icon: Gamepad2, color: "#9b59b6", deadline: null },
];

const STATUS_CONFIG = {
  todo: { label: "À faire", color: "#ed9121", bg: "rgba(237,145,33,0.12)" },
  in_progress: { label: "En cours", color: "#5865f2", bg: "rgba(88,101,242,0.12)" },
  to_test: { label: "À tester", color: "#06b6d4", bg: "rgba(6,182,212,0.12)" },
  done: { label: "Fait", color: "#3e9041", bg: "rgba(62,144,65,0.12)" },
  bug: { label: "Bug/Fix", color: "#d13b1a", bg: "rgba(209,59,26,0.12)" },
  v2: { label: "Reporté V2", color: "#525066", bg: "rgba(82,80,102,0.15)" },
  archived: { label: "Archivé", color: "#666666", bg: "rgba(102,102,102,0.12)" },
};

// Notation 1..5. NULL = non priorisée (force un choix volontaire).
// 1 = Backlog, 2 = Plus tard, 3 = Bientôt, 4 = Important, 5 = Urgent.
const PRIO_CONFIG = {
  1: { label: "1 — Backlog",     color: "#7a7a7a", bg: "rgba(122,122,122,0.18)" },
  2: { label: "2 — Plus tard",   color: "#c9b03a", bg: "rgba(201,176,58,0.18)" },
  3: { label: "3 — Bientôt",     color: "#ed9121", bg: "rgba(237,145,33,0.18)" },
  4: { label: "4 — Important",   color: "#e0531a", bg: "rgba(224,83,26,0.18)" },
  5: { label: "5 — Urgent",      color: "#d13b1a", bg: "rgba(209,59,26,0.22)" },
};
const PRIO_LEVELS = [1, 2, 3, 4, 5];

let _idCounter = Date.now();
const genId = () => `t_${_idCounter++}`;

const DEFAULT_TASKS = [];
const DEFAULT_IDEAS = [];

// --- STORAGE ---
async function loadData() {
  const dbData = await api.getHub(); // laisse remonter les erreurs réseau
  return dbData ?? null; // null = DB vide, exception = erreur réseau
}


export async function loadHubData() {
  return loadData();
}

// --- DISCORD WEBHOOK NOTIFICATIONS ---
const DISCORD_WEBHOOK = "https://discord.com/api/webhooks/1488196286757863436/Gjbwk1WC_lCm7ILmIDfo_kuxf8ZuMuBCuVgUL-ZhV2c7awBHXynW0os4hIhXGRpkF2UP";

const NOTIF_COLORS = {
  status_change: 0xe07b39,
  new_task:      0x3e9041,
  task_deleted:  0xd13b1a,
  new_idea:      0x5865f2,
  idea_converted:0xed9121,
  comment:       0x525066,
  bug_reported:  0xd13b1a,
};

const STATUS_COLORS = {
  todo:        0xed9121,
  in_progress: 0x5865f2,
  to_test:     0x06b6d4,
  done:        0x3e9041,
  bug:         0xd13b1a,
  v2:          0x525066,
};

const STATUS_EMOJI = {
  todo: "🟡", in_progress: "🔵", to_test: "🧪", done: "✅", bug: "🔴", v2: "⏳",
};

const PRIO_EMOJI = { high: "🔴", med: "🟠", low: "🟢" };

const HUB_ICON = "https://sbox.game/favicon.ico";

async function sendDiscordNotif(type, data) {
  const embed = buildEmbed(type, data);
  if (!embed) return;
  try {
    await fetch(DISCORD_WEBHOOK, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        username: "OpenFramework Hub",
        avatar_url: "https://sbox.game/favicon.ico",
        embeds: [embed],
      }),
    });
  } catch (e) {
    console.log("[Discord] Webhook blocked (sandbox) — will work when hosted:", e.message);
  }
}

// ── Debounce queue — regroupe les notifs envoyées en moins de 4s ──────────
let _discordQueue = [];
let _discordTimer = null;

function queueDiscordNotif(type, data) {
  _discordQueue.push({ type, data });
  clearTimeout(_discordTimer);
  _discordTimer = setTimeout(async () => {
    const batch = _discordQueue.splice(0);
    if (batch.length === 0) return;
    if (batch.length === 1) {
      await sendDiscordNotif(batch[0].type, batch[0].data);
      return;
    }
    // Résumé groupé
    const lines = batch.map(({ type: t, data: d }) => {
      const proj = d.projects ? (d.projects.find(p => p.id === (d.task || d.idea)?.projectId)?.name || "") : "";
      const text = (d.task?.text || d.idea?.text || "").substring(0, 55);
      const typeLabel = {
        new_task:      "＋ Tâche créée",
        task_edited:   "✎  Tâche modifiée",
        status_change: `⇒  ${STATUS_CONFIG[d.newStatus]?.label || d.newStatus}`,
        task_deleted:  "✕  Tâche supprimée",
        new_idea:      "◈  Idée ajoutée",
        idea_converted:"◈→ Idée convertie",
      }[t] || t;
      return `\`${typeLabel}\`  **${text}**${proj ? `  ·  ${proj}` : ""}`;
    });
    const embed = {
      color: 0x5865f2,
      author: { name: "OpenFramework Hub  ·  Activité groupée", icon_url: HUB_ICON },
      description: lines.join("\n"),
      footer: { text: `${batch.length} modifications  ·  OpenFramework Hub` },
      timestamp: new Date().toISOString(),
    };
    try {
      await fetch(DISCORD_WEBHOOK, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: "OpenFramework Hub", avatar_url: HUB_ICON, embeds: [embed] }),
      });
    } catch (e) {
      console.log("[Discord] Batch webhook blocked:", e.message);
    }
  }, 4000);
}

function buildEmbed(type, data) {
  const { task, oldStatus, newStatus, idea, members, projects } = data;

  const memberNames = (ids) => {
    const names = (ids || []).map(id => {
      const m = (members || DEFAULT_MEMBERS).find(x => x.id === id);
      return m ? m.name : id;
    });
    return names.length ? names.join(", ") : "Non assigné";
  };

  const projectName = (id) => {
    const p = (projects || DEFAULT_PROJECTS).find(x => x.id === id);
    return p ? p.name : id;
  };

  const ts = new Date().toISOString();

  switch (type) {
    case "status_change": {
      const oldCfg = STATUS_CONFIG[oldStatus];
      const newCfg = STATUS_CONFIG[newStatus];
      return {
        color: STATUS_COLORS[newStatus] || NOTIF_COLORS.status_change,
        author: { name: projectName(task.projectId), icon_url: HUB_ICON },
        title: task.text,
        description: `\`${oldCfg?.label}\`  →  \`${newCfg?.label}\``,
        fields: [
          { name: "Assigné", value: memberNames(task.assignees), inline: true },
          ...(task.priority ? [{ name: "Priorité", value: PRIO_CONFIG[task.priority]?.label || task.priority, inline: true }] : []),
          ...(task.category ? [{ name: "Catégorie", value: task.category, inline: true }] : []),
        ],
        footer: { text: "OpenFramework Hub", icon_url: HUB_ICON },
        timestamp: ts,
      };
    }
    case "new_task":
      return {
        color: NOTIF_COLORS.new_task,
        author: { name: `${projectName(task.projectId)}  ·  Nouvelle tâche`, icon_url: HUB_ICON },
        title: task.text,
        fields: [
          { name: "Priorité", value: PRIO_CONFIG[task.priority]?.label || "—", inline: true },
          { name: "Assigné", value: memberNames(task.assignees), inline: true },
          ...(task.category ? [{ name: "Catégorie", value: task.category, inline: true }] : []),
          ...(task.notes ? [{ name: "Notes", value: task.notes.substring(0, 300) }] : []),
        ],
        footer: { text: "OpenFramework Hub", icon_url: HUB_ICON },
        timestamp: ts,
      };
    case "task_deleted":
      return {
        color: NOTIF_COLORS.task_deleted,
        author: { name: `${projectName(task.projectId)}  ·  Tâche supprimée`, icon_url: HUB_ICON },
        title: task.text,
        fields: [
          { name: "Assigné", value: memberNames(task.assignees), inline: true },
        ],
        footer: { text: "OpenFramework Hub", icon_url: HUB_ICON },
        timestamp: ts,
      };
    case "new_idea":
      return {
        color: NOTIF_COLORS.new_idea,
        author: { name: `${projectName(idea.projectId)}  ·  Nouvelle idée`, icon_url: HUB_ICON },
        title: idea.text,
        footer: { text: "OpenFramework Hub", icon_url: HUB_ICON },
        timestamp: ts,
      };
    case "idea_converted":
      return {
        color: NOTIF_COLORS.idea_converted,
        author: { name: `${projectName(idea.projectId)}  ·  Idée convertie en tâche`, icon_url: HUB_ICON },
        title: idea.text,
        footer: { text: "OpenFramework Hub", icon_url: HUB_ICON },
        timestamp: ts,
      };
    case "task_edited": {
      const changes = data.changes || [];
      // Sépare "Notes" (texte long) des autres (inline)
      const notesChange = changes.find(c => c.field === "Notes");
      const inlineChanges = changes.filter(c => c.field !== "Notes");
      return {
        color: STATUS_COLORS[task.status] || NOTIF_COLORS.comment,
        author: { name: `${projectName(task.projectId)}  ·  Tâche modifiée`, icon_url: HUB_ICON },
        title: task.text,
        fields: [
          ...inlineChanges.map(c => ({
            name: c.field,
            value: c.from ? `\`${c.from}\`  →  \`${c.to}\`` : `\`${c.to}\``,
            inline: true,
          })),
          ...(notesChange ? [{ name: "Notes", value: notesChange.to.substring(0, 300) || "_(vide)_" }] : []),
        ],
        footer: { text: "OpenFramework Hub", icon_url: HUB_ICON },
        timestamp: ts,
      };
    }
    default: return null;
  }
}

// --- COMPONENTS ---

function Badge({ member, small }) {
  if (!member) return null;
  return (
    <span style={{
      fontSize: small ? "0.62rem" : "0.7rem", fontWeight: 600,
      padding: small ? "1px 6px" : "2px 8px", borderRadius: 5,
      background: member.color + "1a", color: member.color,
      border: `1px solid ${member.color}40`, whiteSpace: "nowrap",
      textTransform: "uppercase", letterSpacing: "0.03em",
    }}>{member.name}</span>
  );
}

// Avatar circulaire — photo Steam si dispo, sinon initiale colorée
function MemberAvatar({ member, size = 22, style = {} }) {
  if (!member) return null;
  const s = {
    width: size, height: size, borderRadius: "50%", flexShrink: 0,
    border: `2px solid ${member.color}60`, overflow: "hidden",
    display: "flex", alignItems: "center", justifyContent: "center",
    background: member.avatar ? "transparent" : member.color,
    ...style,
  };
  if (member.avatar) {
    return (
      <span title={member.name} style={s}>
        <img src={member.avatar} alt={member.name}
          style={{ width: "100%", height: "100%", objectFit: "cover", display: "block" }}
          onError={e => {
            e.target.style.display = "none";
            e.target.parentElement.style.background = member.color;
            e.target.insertAdjacentHTML("afterend", `<span style="font-size:${Math.round(size*0.45)}px;font-weight:700;color:#11151f">${member.name[0]}</span>`);
          }}
        />
      </span>
    );
  }
  return (
    <span title={member.name} style={s}>
      <span style={{ fontSize: Math.round(size * 0.45), fontWeight: 700, color: "#11151f", lineHeight: 1 }}>{member.name[0]}</span>
    </span>
  );
}

function StatusBadge({ status }) {
  const cfg = STATUS_CONFIG[status];
  if (!cfg) return null;
  return (
    <span style={{
      fontSize: "0.68rem", fontWeight: 600, padding: "3px 10px",
      borderRadius: 5, background: cfg.bg, color: cfg.color, whiteSpace: "nowrap",
    }}>{cfg.label}</span>
  );
}

function PrioBadge({ priority }) {
  if (priority == null) {
    return (
      <span title="Priorité non définie — à reprioriser" style={{
        display: "inline-flex", alignItems: "center", justifyContent: "center",
        width: 18, height: 18, borderRadius: "50%",
        fontSize: "0.65rem", fontWeight: 700,
        color: "#888", background: "transparent",
        border: "1px dashed rgba(255,255,255,0.25)",
      }}>?</span>
    );
  }
  const cfg = PRIO_CONFIG[priority];
  if (!cfg) return null;
  return (
    <span title={cfg.label} style={{
      display: "inline-flex", alignItems: "center", justifyContent: "center",
      width: 18, height: 18, borderRadius: "50%",
      fontSize: "0.7rem", fontWeight: 700,
      color: cfg.color, background: cfg.bg,
      border: `1px solid ${cfg.color}`,
    }}>{priority}</span>
  );
}

function Countdown({ deadline }) {
  const [now, setNow] = useState(Date.now());
  useEffect(() => { const i = setInterval(() => setNow(Date.now()), 60000); return () => clearInterval(i); }, []);
  if (!deadline) return null;
  const diff = new Date(deadline) - now;
  if (diff <= 0) return <span style={{ color: "#d13b1a", fontWeight: 700 }}>C'est l'heure !</span>;
  const days = Math.floor(diff / 86400000);
  const hours = Math.floor((diff % 86400000) / 3600000);
  return <span style={{ fontFamily: "monospace", fontWeight: 700, color: "#d13b1a" }}>J-{days}j {hours}h</span>;
}

// --- MD IMPORT PARSER ---
function parseMdImport(text) {
  // Map vers la notation 1..5. Ancien vocabulaire conservé pour compat imports.
  const PRIO_MAP = {
    "1": 1, "2": 2, "3": 3, "4": 4, "5": 5,
    basse: 2, low: 2,
    moyenne: 3, med: 3,
    haute: 4, high: 4,
    urgente: 5, urgent: 5,
  };
  const STATUS_MAP = {
    "à faire": "todo", "a faire": "todo", todo: "todo",
    "en cours": "in_progress", in_progress: "in_progress",
    "à tester": "to_test", "a tester": "to_test", tester: "to_test", to_test: "to_test",
    fait: "done", done: "done",
    bug: "bug",
    "reporté": "v2", reporte: "v2", v2: "v2",
  };
  const VALID_PROJECTS = ["sl-v1", "sl-v2", "sw-rp", "ph"];

  const sections = text.split(/\n---+\n?/).map(s => s.trim()).filter(Boolean);
  const tasks = [], ideas = [], errors = [];

  sections.forEach((section, si) => {
    const lines = section.split("\n");
    const titleLine = lines.find(l => /^#\s+/.test(l));
    if (!titleLine) { errors.push(`Section ${si + 1} : pas de titre (#)`); return; }
    const title = titleLine.replace(/^#\s+/, "").trim();

    // Metadata : lignes "Clé: valeur" jusqu'à la première ligne vide après le titre
    const meta = {};
    let bodyStart = lines.length;
    let pastTitle = false;
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      if (/^#\s+/.test(line)) { pastTitle = true; continue; }
      if (!pastTitle) continue;
      if (line.trim() === "") { bodyStart = i + 1; break; }
      const m = line.match(/^([^:]+):\s*(.*)$/);
      if (m) meta[m[1].trim().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "")] = m[2].trim();
    }

    // Corps = tout après la ligne vide
    const bodyLines = lines.slice(bodyStart);
    // Séparer Notes: du reste
    const notesIdx = bodyLines.findIndex(l => /^Notes:\s*/i.test(l));
    let description = "", notes = "";
    if (notesIdx !== -1) {
      description = bodyLines.slice(0, notesIdx).join("\n").trim();
      notes = bodyLines.slice(notesIdx).join("\n").replace(/^Notes:\s*/i, "").trim();
    } else {
      description = bodyLines.join("\n").trim();
    }

    const rawType = (meta["type"] || "tache").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();
    const isIdea = rawType === "idee";
    const projectId = VALID_PROJECTS.includes(meta["projet"]) ? meta["projet"] : "sl-v1";

    if (isIdea) {
      ideas.push({
        id: `i_${Date.now()}_${si}`,
        text: title,
        description: description || '',
        projectId,
        createdAt: Date.now(),
      });
    } else {
      const rawPrio = (meta["priorite"] || "").toLowerCase();
      const rawStatus = (meta["status"] || "a faire").toLowerCase();
      const rawAssignees = meta["assignes"] || "";
      tasks.push({
        id: genId(),
        text: title,
        description,
        projectId,
        category: meta["categorie"] || "",
        status: STATUS_MAP[rawStatus] || "todo",
        priority: PRIO_MAP[rawPrio] ?? null,
        assignees: rawAssignees ? rawAssignees.split(",").map(a => a.trim().toLowerCase().replace(/\s+/g, "_")).filter(Boolean) : [],
        deadline: /^\d{4}-\d{2}-\d{2}$/.test(meta["deadline"] || "") ? meta["deadline"] : null,
        notes,
        images: [],
        createdAt: Date.now(),
        updatedAt: Date.now(),
      });
    }
  });

  return { tasks, ideas, errors };
}

// --- MD IMPORT MODAL ---
function ImportModal({ onImport, onClose, projects }) {
  const [result, setResult] = useState(null);
  const [dragging, setDragging] = useState(false);
  const fileRef = useRef(null);

  const parse = (text) => setResult(parseMdImport(text));

  const onFile = (file) => {
    if (!file) return;
    const reader = new FileReader();
    reader.onload = e => parse(e.target.result);
    reader.readAsText(file, "utf-8");
  };

  const onDrop = (e) => {
    e.preventDefault(); setDragging(false);
    onFile(e.dataTransfer.files[0]);
  };

  const projectName = (id) => projects.find(p => p.id === id)?.name || id;

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.75)", zIndex: 1100, display: "flex", alignItems: "center", justifyContent: "center" }}>
      <div style={{ background: "#1a1f2c", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 16, padding: 28, width: "100%", maxWidth: 560, maxHeight: "85vh", overflow: "auto" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
          <h3 style={{ fontWeight: 700, fontSize: "1rem", color: "#fff", margin: 0 }}><IC icon={FileUp} style={{ marginRight: 8, color: "var(--brand-primary, #e07b39)" }} />Importer un fichier .md</h3>
          <button onClick={onClose} style={{ background: "none", border: "none", color: "#888", cursor: "pointer", padding: 4 }}><IC icon={X} size={16} /></button>
        </div>

        {!result ? (
          <div
            onDragOver={e => { e.preventDefault(); setDragging(true); }}
            onDragLeave={() => setDragging(false)}
            onDrop={onDrop}
            onClick={() => fileRef.current?.click()}
            style={{ border: `2px dashed ${dragging ? "var(--brand-primary, #e07b39)" : "rgba(255,255,255,0.15)"}`, borderRadius: 12, padding: "40px 20px", textAlign: "center", cursor: "pointer", transition: "border-color 0.15s", background: dragging ? "rgba(60, 173, 217,0.05)" : "transparent" }}
          >
            <IC icon={FileUp} size={28} style={{ color: "var(--brand-primary, #e07b39)", marginBottom: 10, display: "block", margin: "0 auto 10px" }} />
            <div style={{ color: "#e8eaed", fontWeight: 600, marginBottom: 6 }}>Glisse un fichier .md ici</div>
            <div style={{ color: "#666", fontSize: "0.78rem" }}>ou clique pour sélectionner</div>
            <input ref={fileRef} type="file" accept=".md,text/markdown,text/plain" style={{ display: "none" }} onChange={e => onFile(e.target.files[0])} />
          </div>
        ) : (
          <>
            {result.errors.length > 0 && (
              <div style={{ background: "rgba(209,59,26,0.12)", border: "1px solid rgba(209,59,26,0.3)", borderRadius: 8, padding: "10px 14px", marginBottom: 14 }}>
                {result.errors.map((e, i) => <div key={i} style={{ fontSize: "0.78rem", color: "#d13b1a" }}>⚠ {e}</div>)}
              </div>
            )}

            <div style={{ marginBottom: 14 }}>
              <div style={{ fontSize: "0.72rem", fontWeight: 700, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: 8 }}>
                {result.tasks.length} tâche{result.tasks.length !== 1 ? "s" : ""} · {result.ideas.length} idée{result.ideas.length !== 1 ? "s" : ""}
              </div>
              {result.tasks.map((t, i) => (
                <div key={i} style={{ display: "flex", gap: 10, alignItems: "flex-start", padding: "8px 10px", borderRadius: 8, background: "rgba(255,255,255,0.03)", marginBottom: 4 }}>
                  <IC icon={SquareCheck} size={13} style={{ color: "var(--brand-primary, #e07b39)", flexShrink: 0, marginTop: 2 }} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: "0.83rem", color: "#e8eaed", fontWeight: 600, marginBottom: 2 }}>{t.text}</div>
                    <div style={{ fontSize: "0.7rem", color: "#666" }}>{projectName(t.projectId)}{t.category ? ` · ${t.category}` : ""} · {PRIO_CONFIG[t.priority]?.label || "Non priorisée"}</div>
                  </div>
                </div>
              ))}
              {result.ideas.map((id, i) => (
                <div key={i} style={{ display: "flex", gap: 10, alignItems: "flex-start", padding: "8px 10px", borderRadius: 8, background: "rgba(255,255,255,0.03)", marginBottom: 4 }}>
                  <IC icon={Lightbulb} size={13} style={{ color: "#5865f2", flexShrink: 0, marginTop: 2 }} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: "0.83rem", color: "#e8eaed", fontWeight: 600, marginBottom: 2 }}>{id.text}</div>
                    {id.description && (
                      <div style={{ fontSize: "0.75rem", color: "#999", lineHeight: 1.4, marginBottom: 2, whiteSpace: "pre-wrap", maxHeight: 60, overflow: "hidden" }}>{id.description}</div>
                    )}
                    <div style={{ fontSize: "0.7rem", color: "#666" }}>{projectName(id.projectId)}</div>
                  </div>
                </div>
              ))}
              {result.tasks.length === 0 && result.ideas.length === 0 && (
                <div style={{ color: "#666", fontSize: "0.82rem", textAlign: "center", padding: 20 }}>Aucun élément parsé — vérifie le format du fichier.</div>
              )}
            </div>

            <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
              <button onClick={() => setResult(null)} style={{ padding: "8px 16px", background: "transparent", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8, color: "#888", cursor: "pointer", fontFamily: "inherit", fontSize: "0.82rem" }}>Changer de fichier</button>
              <button
                onClick={() => { onImport(result.tasks, result.ideas); onClose(); }}
                disabled={result.tasks.length === 0 && result.ideas.length === 0}
                style={{ padding: "8px 18px", background: result.tasks.length + result.ideas.length ? "var(--brand-primary, #e07b39)" : "#555", color: "#161a26", border: "none", borderRadius: 8, cursor: result.tasks.length + result.ideas.length ? "pointer" : "default", fontWeight: 700, fontFamily: "inherit", fontSize: "0.82rem" }}
              >
                Importer {result.tasks.length + result.ideas.length} élément{result.tasks.length + result.ideas.length !== 1 ? "s" : ""}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

// --- IMAGE LIGHTBOX ---
function ImageLightbox({ url, onClose }) {
  useEffect(() => {
    const onKey = (e) => { if (e.key === "Escape") onClose(); };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);
  return (
    <div onClick={onClose} style={{ position: "fixed", inset: 0, zIndex: 9999, background: "rgba(0,0,0,0.88)", display: "flex", alignItems: "center", justifyContent: "center", cursor: "zoom-out" }}>
      <img src={url} alt="" onClick={e => e.stopPropagation()} style={{ maxWidth: "90vw", maxHeight: "90vh", objectFit: "contain", borderRadius: 8, boxShadow: "0 8px 40px rgba(0,0,0,0.7)", cursor: "default" }} />
    </div>
  );
}

// --- TASK MODAL ---
function TaskModal({ task, members, tasks, milestones, onSave, onClose, onDelete, onMilestoneChange }) {
  const [form, setForm] = useState({ ...task });
  const update = (k, v) => setForm(f => ({ ...f, [k]: v }));
  const [history, setHistory] = useState([]);
  const [historyOpen, setHistoryOpen] = useState(false);

  useEffect(() => {
    if (!task.id) return;
    api.getTaskActivity(task.id, 50).then(setHistory).catch(() => setHistory([]));
  }, [task.id]);

  // Find which milestone this task is in
  const currentMsId = (milestones || []).find(m => (m.taskIds || []).includes(task.id))?.id || "";

  // Milestone par défaut pour une nouvelle tâche : le plus proche dans le futur
  // (date >= aujourd'hui), à défaut le plus récent passé. Ignore les archivés.
  const [selectedMs, setSelectedMs] = useState(() => {
    if (currentMsId) return currentMsId;
    if (task.id) return ""; // tâche existante non rattachée → on ne suggère rien
    const active = (milestones || []).filter(m => !m.archived);
    if (active.length === 0) return "";
    const now = Date.now();
    const future = active
      .filter(m => new Date(m.date).getTime() >= now)
      .sort((a, b) => new Date(a.date) - new Date(b.date));
    if (future.length > 0) return future[0].id;
    const past = [...active].sort((a, b) => new Date(b.date) - new Date(a.date));
    return past[0].id;
  });
  const [showAssigneeDropdown, setShowAssigneeDropdown] = useState(false);
  const [lightbox, setLightbox] = useState(null);
  const [imageInput, setImageInput] = useState("");
  const [relType, setRelType] = useState("blocks");
  const [relTarget, setRelTarget] = useState("");
  const [attrKey, setAttrKey] = useState("");
  const [attrValue, setAttrValue] = useState("");
  const [showCatDropdown, setShowCatDropdown] = useState(false);
  const [videoInput, setVideoInput] = useState("");

  const addImageFromInput = (formSnapshot) => {
    const url = imageInput.trim();
    if (!url) return formSnapshot;
    setImageInput("");
    const updated = { ...formSnapshot, images: [...(formSnapshot.images || []), url] };
    setForm(updated);
    return updated;
  };

  return (
    <div style={{
      position: "fixed", inset: 0, background: "rgba(0,0,0,0.7)", zIndex: 1000,
      display: "flex", alignItems: "center", justifyContent: "center", padding: 20,
    }} onClick={onClose}>
      <div onClick={e => e.stopPropagation()} style={{
        background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 14,
        padding: 28, width: "100%", maxWidth: 600, maxHeight: "90vh", overflow: "auto",
      }}>
        <h3 style={{ fontWeight: 700, fontSize: "1.1rem", marginBottom: task.id ? 6 : 20, color: "#ffffff" }}>
          {task.id ? "Modifier tâche" : "Nouvelle tâche"}
        </h3>

        {task.id && (task.createdAt || task.createdBy || task.updatedAt || task.updatedBy) && (
          <div style={{ fontSize: "0.7rem", color: "#888", marginBottom: 12, display: "flex", flexDirection: "column", gap: 3 }}>
            {(task.createdAt || task.createdBy) && (
              <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                <span style={{ color: "#666" }}>Créée le</span>
                {task.createdAt && <span style={{ color: "#bbb" }}>{new Date(task.createdAt).toLocaleString("fr-FR", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" })}</span>}
                {task.createdBy && <><span style={{ color: "#666" }}>par</span><span style={{ color: "var(--brand-primary, #e07b39)", fontWeight: 600 }}>{task.createdBy}</span></>}
              </div>
            )}
            {(task.updatedAt && task.updatedAt !== task.createdAt) && (
              <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                <span style={{ color: "#666" }}>Modifiée le</span>
                <span style={{ color: "#bbb" }}>{new Date(task.updatedAt).toLocaleString("fr-FR", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" })}</span>
                {task.updatedBy && <><span style={{ color: "#666" }}>par</span><span style={{ color: "#06b6d4", fontWeight: 600 }}>{task.updatedBy}</span></>}
              </div>
            )}
          </div>
        )}

        {task.id && history.length > 0 && (
          <div style={{ marginBottom: 16, border: "1px solid rgba(255,255,255,0.06)", borderRadius: 8, background: "rgba(255,255,255,0.02)" }}>
            <button onClick={() => setHistoryOpen(o => !o)} style={{
              width: "100%", display: "flex", alignItems: "center", justifyContent: "space-between",
              background: "transparent", border: "none", padding: "8px 12px", cursor: "pointer",
              color: "#888", fontSize: "0.72rem", fontWeight: 600, fontFamily: "inherit",
            }}>
              <span>Historique ({history.length})</span>
              <span style={{ fontSize: "0.65rem" }}>{historyOpen ? "▴" : "▾"}</span>
            </button>
            {historyOpen && (
              <div style={{ maxHeight: 200, overflowY: "auto", padding: "0 12px 10px", display: "flex", flexDirection: "column", gap: 6 }}>
                {history.map(h => {
                  const member = members.find(m => m.id === h.author);
                  const when = h.created_at ? new Date(h.created_at).toLocaleString("fr-FR", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" }) : "";
                  const actionColor = { create: "#3e9041", edit: "#5865f2", status: "var(--brand-primary, #e07b39)", delete: "#d13b1a" }[h.action] || "#888";
                  return (
                    <div key={h.id} style={{ fontSize: "0.7rem", color: "#bbb", display: "flex", alignItems: "flex-start", gap: 8, padding: "6px 8px", background: "rgba(0,0,0,0.2)", borderRadius: 5, borderLeft: `2px solid ${actionColor}` }}>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 2 }}>
                          <span style={{ color: member?.color || "var(--brand-primary, #e07b39)", fontWeight: 700, fontSize: "0.68rem" }}>{member?.name || h.author || "system"}</span>
                          <span style={{ color: "#555", fontSize: "0.62rem" }}>{when}</span>
                        </div>
                        <div style={{ color: "#ccc", fontSize: "0.72rem", lineHeight: 1.3, wordBreak: "break-word" }}>{h.detail}</div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}

        <label style={labelStyle}>Nom</label>
        <input value={form.text} onChange={e => update("text", e.target.value)} style={inputStyle} placeholder="Nom de la tâche..." autoFocus />

        <label style={labelStyle}>Description</label>
        <textarea value={form.description || ""} onChange={e => update("description", e.target.value)}
          style={{ ...inputStyle, minHeight: 70, resize: "vertical" }} placeholder="Détails, contexte..." />

        <label style={labelStyle}>Catégorie</label>
        {(() => {
          const allCats = [...new Set((tasks || []).map(t => t.category).filter(Boolean))].sort();
          const q = (form.category || "").trim().toLowerCase();
          const filtered = q ? allCats.filter(c => c.toLowerCase().includes(q)) : allCats;
          const exactMatch = allCats.some(c => c.toLowerCase() === q);
          return (
            <div style={{ position: "relative", marginBottom: 12 }}>
              <div style={{ position: "relative" }}>
                <input
                  value={form.category || ""}
                  onChange={e => { update("category", e.target.value); setShowCatDropdown(true); }}
                  onFocus={() => setShowCatDropdown(true)}
                  onBlur={() => setTimeout(() => setShowCatDropdown(false), 150)}
                  style={{ ...inputStyle, marginBottom: 0, paddingRight: 32 }}
                  placeholder="Choisir ou créer une catégorie…"
                />
                <span style={{ position: "absolute", right: 10, top: "50%", transform: "translateY(-50%)", color: "#555", pointerEvents: "none", fontSize: "0.7rem" }}>▾</span>
              </div>
              {showCatDropdown && (filtered.length > 0 || (q && !exactMatch)) && (
                <div style={{ position: "absolute", top: "calc(100% + 2px)", left: 0, right: 0, background: "#1f2330", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 8, zIndex: 200, maxHeight: 220, overflowY: "auto", boxShadow: "0 8px 24px rgba(0,0,0,0.5)" }}>
                  {filtered.map(cat => (
                    <div key={cat} onMouseDown={() => { update("category", cat); setShowCatDropdown(false); }}
                      style={{ padding: "8px 14px", fontSize: "0.84rem", color: cat === form.category ? "var(--brand-primary, #e07b39)" : "#e8eaed", cursor: "pointer", background: cat === form.category ? "rgba(60, 173, 217,0.1)" : "transparent", borderLeft: cat === form.category ? "2px solid var(--brand-primary, #e07b39)" : "2px solid transparent" }}
                      onMouseEnter={e => { if (cat !== form.category) e.currentTarget.style.background = "rgba(255,255,255,0.05)"; }}
                      onMouseLeave={e => { if (cat !== form.category) e.currentTarget.style.background = "transparent"; }}>
                      {cat}
                    </div>
                  ))}
                  {q && !exactMatch && (
                    <div onMouseDown={() => { update("category", form.category.trim()); setShowCatDropdown(false); }}
                      style={{ padding: "8px 14px", fontSize: "0.84rem", color: "#3e9041", cursor: "pointer", borderTop: filtered.length > 0 ? "1px solid rgba(255,255,255,0.07)" : "none", display: "flex", alignItems: "center", gap: 6 }}
                      onMouseEnter={e => e.currentTarget.style.background = "rgba(62,144,65,0.1)"}
                      onMouseLeave={e => e.currentTarget.style.background = "transparent"}>
                      <span style={{ fontSize: "0.9rem", fontWeight: 700 }}>+</span> Créer <strong style={{ color: "#fff" }}>"{form.category.trim()}"</strong>
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })()}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
          <div>
            <label style={labelStyle}>Statut</label>
            <select value={form.status} onChange={e => update("status", e.target.value)} style={inputStyle}>
              {Object.entries(STATUS_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
            </select>
          </div>
          <div>
            <label style={labelStyle}>Priorité</label>
            <select
              value={form.priority ?? ""}
              onChange={e => update("priority", e.target.value === "" ? null : Number(e.target.value))}
              style={inputStyle}
            >
              <option value="">— Non priorisée —</option>
              {PRIO_LEVELS.map(n => <option key={n} value={n}>{PRIO_CONFIG[n].label}</option>)}
            </select>
          </div>
        </div>

        <label style={labelStyle}>Deadline</label>
        <input type="date" value={form.deadline || ""} onChange={e => update("deadline", e.target.value || null)} style={inputStyle} />

        <label style={labelStyle}>Milestone (Roadmap)</label>
        <select value={selectedMs} onChange={e => setSelectedMs(e.target.value)} style={inputStyle}>
          <option value="">— Aucun milestone —</option>
          {(milestones || [])
            .filter(m => !m.archived || m.id === currentMsId) // cache les archivés, sauf si la tâche y est déjà rattachée
            .sort((a, b) => new Date(a.date) - new Date(b.date))
            .map(m => (
              <option key={m.id} value={m.id}>
                {m.name} ({new Date(m.date).toLocaleDateString("fr-FR", { day: "numeric", month: "short" })})
                {m.archived ? " — archivé" : ""}
              </option>
            ))}
        </select>

        <label style={labelStyle}>Assignés</label>
        <div style={{ position: "relative", marginBottom: 12 }}>
          <div onClick={() => setShowAssigneeDropdown(v => !v)} style={{ ...inputStyle, cursor: "pointer", display: "flex", flexWrap: "wrap", gap: 4, minHeight: 36, alignItems: "center", paddingTop: 6, paddingBottom: 6, userSelect: "none" }}>
            {(form.assignees || []).length === 0
              ? <span style={{ color: "#555", fontSize: "0.82rem" }}>Sélectionner des membres…</span>
              : (form.assignees || []).map(id => {
                  const m = members.find(mb => mb.id === id);
                  return m ? <span key={id} style={{ background: m.color + "28", color: m.color, border: `1px solid ${m.color}55`, borderRadius: 4, padding: "2px 7px", fontSize: "0.72rem", fontWeight: 700 }}>{m.name}</span> : null;
                })
            }
            <IC icon={ChevronDown} size={13} style={{ marginLeft: "auto", color: "#555", flexShrink: 0 }} />
          </div>
          {showAssigneeDropdown && (
            <>
            <div onClick={() => setShowAssigneeDropdown(false)} style={{ position: "fixed", inset: 0, zIndex: 199 }} />
            <div style={{ position: "absolute", top: "calc(100% + 2px)", left: 0, right: 0, zIndex: 200, background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 8, overflow: "hidden", boxShadow: "0 8px 24px rgba(0,0,0,0.4)" }}>
              {members.length === 0 && <div style={{ padding: "12px 14px", fontSize: "0.78rem", color: "#555" }}>Aucun membre configuré</div>}
              {members.map(m => {
                const sel = (form.assignees || []).includes(m.id);
                return (
                  <div key={m.id} onClick={() => { const a = form.assignees || []; update("assignees", sel ? a.filter(x => x !== m.id) : [...a, m.id]); }} style={{ display: "flex", alignItems: "center", gap: 10, padding: "9px 14px", cursor: "pointer", background: sel ? m.color + "14" : "transparent", transition: "background 0.1s" }}
                    onMouseEnter={e => { if (!sel) e.currentTarget.style.background = "rgba(255,255,255,0.04)"; }}
                    onMouseLeave={e => { e.currentTarget.style.background = sel ? m.color + "14" : "transparent"; }}>
                    <div style={{ width: 15, height: 15, borderRadius: 3, border: `2px solid ${sel ? m.color : "rgba(255,255,255,0.2)"}`, background: sel ? m.color : "transparent", flexShrink: 0, display: "flex", alignItems: "center", justifyContent: "center" }}>
                      {sel && <IC icon={Check} size={9} style={{ color: "#fff" }} />}
                    </div>
                    <MemberAvatar member={m} size={22} />
                    <span style={{ fontSize: "0.83rem", color: sel ? m.color : "#e8eaed", fontWeight: sel ? 700 : 400 }}>{m.name}</span>
                  </div>
                );
              })}
              <div onClick={() => setShowAssigneeDropdown(false)} style={{ padding: "8px 14px", fontSize: "0.72rem", color: "#666", cursor: "pointer", borderTop: "1px solid rgba(255,255,255,0.06)", textAlign: "center" }}>Fermer</div>
            </div>
            </>
          )}
        </div>

        <label style={labelStyle}>Notes</label>
        <textarea value={form.notes || ""} onChange={e => update("notes", e.target.value)}
          style={{ ...inputStyle, minHeight: 50, resize: "vertical" }} placeholder="Notes, contexte, d\u00e9tails..." />

        <label style={labelStyle}><IC icon={Check} size={12} style={{ marginRight: 4 }} />{"Sous-t\u00e2ches"}</label>
        <div style={{ marginBottom: 8 }}>
          {(form.subtasks || []).map((st, i) => (
            <div key={i} style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
              <div onClick={() => {
                const updated = [...(form.subtasks || [])];
                updated[i] = { ...updated[i], done: !updated[i].done };
                update("subtasks", updated);
              }} style={{
                width: 18, height: 18, borderRadius: 4, cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
                border: st.done ? "2px solid #3e9041" : "2px solid rgba(255,255,255,0.2)",
                background: st.done ? "#3e9041" : "transparent", flexShrink: 0,
              }}>
                {st.done && <IC icon={Check} size={11} style={{ color: "#fff" }} />}
              </div>
              <input value={st.text} onChange={e => {
                const updated = [...(form.subtasks || [])];
                updated[i] = { ...updated[i], text: e.target.value };
                update("subtasks", updated);
              }} style={{ ...inputStyle, flex: 1, marginBottom: 0, textDecoration: st.done ? "line-through" : "none", opacity: st.done ? 0.5 : 1 }} />
              <button onClick={() => update("subtasks", (form.subtasks || []).filter((_, j) => j !== i))}
                style={{ background: "none", border: "none", color: "#888888", cursor: "pointer", padding: 2 }}><IC icon={X} size={14} /></button>
            </div>
          ))}
          <div style={{ display: "flex", gap: 6 }}>
            <input placeholder="Ajouter une sous-t\u00e2che..." style={{ ...inputStyle, flex: 1, marginBottom: 0, fontSize: "0.78rem" }}
              onKeyDown={e => {
                if (e.key === "Enter" && e.target.value.trim()) {
                  update("subtasks", [...(form.subtasks || []), { text: e.target.value.trim(), done: false }]);
                  e.target.value = "";
                }
              }} />
          </div>
          {(form.subtasks || []).length > 0 && (
            <div style={{ fontSize: "0.7rem", color: "#888888", marginTop: 4 }}>
              {(form.subtasks || []).filter(s => s.done).length}/{(form.subtasks || []).length}{" compl\u00e9t\u00e9es"}
            </div>
          )}
        </div>

        <label style={labelStyle}>Images (URLs)</label>
        <div style={{ display: "flex", gap: 6, marginBottom: 8 }}>
          <input
            value={imageInput}
            onChange={e => setImageInput(e.target.value)}
            placeholder="Coller une URL d'image…"
            style={{ ...inputStyle, flex: 1, marginBottom: 0 }}
            onKeyDown={e => {
              if (e.key === "Enter" && imageInput.trim()) {
                update("images", [...(form.images || []), imageInput.trim()]);
                setImageInput("");
              }
            }}
          />
          <button
            onClick={() => { if (imageInput.trim()) { update("images", [...(form.images || []), imageInput.trim()]); setImageInput(""); } }}
            style={{ padding: "0 12px", background: "var(--brand-primary, #e07b39)", border: "none", borderRadius: 8, cursor: "pointer", color: "#161a26", fontWeight: 700, fontSize: "1rem", flexShrink: 0 }}
            title="Ajouter l'image"
          >+</button>
        </div>
        {(form.images || []).length > 0 && (
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 8 }}>
            {(form.images || []).map((url, i) => (
              <div key={i} style={{ position: "relative", borderRadius: 8, overflow: "hidden", border: "1px solid rgba(255,255,255,0.1)", background: "#161a26", flexShrink: 0 }}>
                <img src={url} alt="" style={{ display: "block", width: 120, height: 80, objectFit: "cover", cursor: "zoom-in" }}
                  onClick={() => setLightbox(url)}
                  onError={e => { e.target.style.opacity = "0.3"; e.target.alt = "⚠"; }} />
                <button onClick={() => update("images", (form.images || []).filter((_, j) => j !== i))}
                  style={{
                    position: "absolute", top: 3, right: 3, background: "rgba(0,0,0,0.75)", border: "none",
                    color: "#fff", width: 18, height: 18, borderRadius: "50%", cursor: "pointer",
                    display: "flex", alignItems: "center", justifyContent: "center", padding: 0,
                  }}><IC icon={X} size={10} /></button>
              </div>
            ))}
          </div>
        )}

        <label style={labelStyle}><IC icon={Link2} size={11} style={{ marginRight: 4 }} />Vidéos (URLs)</label>
        <div style={{ display: "flex", gap: 6, marginBottom: 8 }}>
          <input
            value={videoInput}
            onChange={e => setVideoInput(e.target.value)}
            placeholder="Coller une URL de vidéo…"
            style={{ ...inputStyle, flex: 1, marginBottom: 0 }}
            onKeyDown={e => {
              if (e.key === "Enter" && videoInput.trim()) {
                update("videos", [...(form.videos || []), videoInput.trim()]);
                setVideoInput("");
              }
            }}
          />
          <button
            onClick={() => { if (videoInput.trim()) { update("videos", [...(form.videos || []), videoInput.trim()]); setVideoInput(""); } }}
            style={{ padding: "0 12px", background: "var(--brand-primary, #e07b39)", border: "none", borderRadius: 8, cursor: "pointer", color: "#161a26", fontWeight: 700, fontSize: "1rem", flexShrink: 0 }}
            title="Ajouter la vidéo"
          >+</button>
        </div>
        {(form.videos || []).length > 0 && (
          <div style={{ display: "flex", flexDirection: "column", gap: 6, marginBottom: 8 }}>
            {(form.videos || []).map((url, i) => (
              <div key={i} style={{ display: "flex", alignItems: "center", gap: 6, background: "#161a26", borderRadius: 8, padding: "6px 10px", border: "1px solid rgba(255,255,255,0.08)" }}>
                <IC icon={ExternalLink} size={13} style={{ color: "var(--brand-primary, #e07b39)", flexShrink: 0 }} />
                <a href={url} target="_blank" rel="noopener noreferrer" style={{ flex: 1, fontSize: "0.78rem", color: "#06b6d4", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", textDecoration: "none" }}>{url}</a>
                <button onClick={() => update("videos", (form.videos || []).filter((_, j) => j !== i))}
                  style={{ background: "transparent", border: "none", color: "#555", cursor: "pointer", padding: 2 }}><IC icon={X} size={12} /></button>
              </div>
            ))}
          </div>
        )}

        {/* ── Relations ── */}
        <label style={labelStyle}><IC icon={Link2} size={11} style={{ marginRight: 4 }} />Relations</label>
        <div style={{ display: "flex", gap: 6, marginBottom: 8 }}>
          <select value={relType} onChange={e => setRelType(e.target.value)} style={{ ...inputStyle, width: 130, marginBottom: 0, flexShrink: 0 }}>
            <option value="blocks">Bloque</option>
            <option value="depends-on">Dépend de</option>
            <option value="related">Liée à</option>
            <option value="duplicate">Doublon de</option>
          </select>
          <select value={relTarget} onChange={e => setRelTarget(e.target.value)} style={{ ...inputStyle, flex: 1, marginBottom: 0 }}>
            <option value="">— Choisir une tâche —</option>
            {(tasks || []).filter(t => t.id !== task.id).map(t => (
              <option key={t.id} value={t.id}>{t.text.substring(0, 50)}</option>
            ))}
          </select>
          <button onClick={() => {
            if (!relTarget) return;
            const existing = (form.relations || []);
            if (existing.find(r => r.type === relType && r.targetId === relTarget)) return;
            update("relations", [...existing, { type: relType, targetId: relTarget }]);
            setRelTarget("");
          }} style={{ padding: "0 12px", background: "var(--brand-primary, #e07b39)", border: "none", borderRadius: 8, cursor: "pointer", color: "#161a26", fontWeight: 700, fontSize: "1rem", flexShrink: 0 }}>+</button>
        </div>
        {(form.relations || []).length > 0 && (
          <div style={{ display: "flex", flexDirection: "column", gap: 4, marginBottom: 10 }}>
            {(form.relations || []).map((r, i) => {
              const target = (tasks || []).find(t => t.id === r.targetId);
              const LABELS = { blocks: "Bloque", "depends-on": "Dépend de", related: "Liée à", duplicate: "Doublon de" };
              const COLORS = { blocks: "#d13b1a", "depends-on": "var(--brand-primary, #e07b39)", related: "#5865f2", duplicate: "#888" };
              return (
                <div key={i} style={{ display: "flex", alignItems: "center", gap: 6, background: "#2a2f3e", borderRadius: 6, padding: "5px 8px" }}>
                  <span style={{ fontSize: "0.68rem", fontWeight: 700, color: COLORS[r.type], background: COLORS[r.type] + "22", borderRadius: 4, padding: "2px 6px", flexShrink: 0 }}>{LABELS[r.type]}</span>
                  <span style={{ fontSize: "0.78rem", color: "#e8eaed", flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{target?.text || r.targetId}</span>
                  <button onClick={() => update("relations", (form.relations || []).filter((_, j) => j !== i))} style={{ background: "transparent", border: "none", color: "#555", cursor: "pointer", padding: 2 }}><IC icon={X} size={12} /></button>
                </div>
              );
            })}
          </div>
        )}

        {/* ── Attributs custom ── */}
        <label style={labelStyle}><IC icon={Tag} size={11} style={{ marginRight: 4 }} />Attributs</label>
        <div style={{ display: "flex", gap: 6, marginBottom: 8 }}>
          <input value={attrKey} onChange={e => setAttrKey(e.target.value)} placeholder="Clé" style={{ ...inputStyle, width: 110, marginBottom: 0, flexShrink: 0 }} />
          <input value={attrValue} onChange={e => setAttrValue(e.target.value)} placeholder="Valeur" style={{ ...inputStyle, flex: 1, marginBottom: 0 }}
            onKeyDown={e => { if (e.key === "Enter" && attrKey.trim() && attrValue.trim()) { update("attrs", [...(form.attrs || []), { key: attrKey.trim(), value: attrValue.trim() }]); setAttrKey(""); setAttrValue(""); } }} />
          <button onClick={() => {
            if (!attrKey.trim() || !attrValue.trim()) return;
            update("attrs", [...(form.attrs || []), { key: attrKey.trim(), value: attrValue.trim() }]);
            setAttrKey(""); setAttrValue("");
          }} style={{ padding: "0 12px", background: "var(--brand-primary, #e07b39)", border: "none", borderRadius: 8, cursor: "pointer", color: "#161a26", fontWeight: 700, fontSize: "1rem", flexShrink: 0 }}>+</button>
        </div>
        {(form.attrs || []).length > 0 && (
          <div style={{ display: "flex", flexDirection: "column", gap: 4, marginBottom: 10 }}>
            {(form.attrs || []).map((a, i) => (
              <div key={i} style={{ display: "flex", alignItems: "center", gap: 6, background: "#2a2f3e", borderRadius: 6, padding: "5px 8px" }}>
                <span style={{ fontSize: "0.72rem", fontWeight: 700, color: "#888", flexShrink: 0 }}>{a.key}</span>
                <span style={{ fontSize: "0.72rem", color: "#555", flexShrink: 0 }}>→</span>
                <span style={{ fontSize: "0.78rem", color: "#e8eaed", flex: 1 }}>{a.value}</span>
                <button onClick={() => update("attrs", (form.attrs || []).filter((_, j) => j !== i))} style={{ background: "transparent", border: "none", color: "#555", cursor: "pointer", padding: 2 }}><IC icon={X} size={12} /></button>
              </div>
            ))}
          </div>
        )}

        <div style={{ display: "flex", gap: 10, marginTop: 20, justifyContent: "flex-end" }}>
          {task.id && <button onClick={() => { onDelete(task.id); onClose(); }} style={{ ...btnStyle, background: "rgba(209,59,26,0.15)", color: "#d13b1a", border: "1px solid rgba(209,59,26,0.3)" }}><IC icon={Trash2} style={{ marginRight: 6 }} />Supprimer</button>}
          <button onClick={onClose} style={{ ...btnStyle, background: "transparent", color: "#888888", border: "1px solid rgba(255,255,255,0.1)" }}><IC icon={X} style={{ marginRight: 6 }} />Annuler</button>
          <button onClick={() => {
            const withImage = addImageFromInput(form);
            const finalForm = (!task.id && !withImage.id) ? { ...withImage, id: genId() } : withImage;
            // On passe le milestone choisi dans le payload pour que le parent traite
            // l'enregistrement de la tâche ET son rattachement au milestone de
            // manière atomique (évite la race entre POST tasks et PUT misc).
            onSave({ ...finalForm, _milestoneId: selectedMs, _previousMilestoneId: currentMsId });
            onClose();
          }} style={{ ...btnStyle, background: "var(--brand-primary, #e07b39)", color: "#161a26", border: "none", fontWeight: 700 }}><IC icon={Save} style={{ marginRight: 6 }} />Sauvegarder</button>
        </div>
      </div>
      {lightbox && <ImageLightbox url={lightbox} onClose={() => setLightbox(null)} />}
    </div>
  );
}

// --- DASHBOARD ---
function DashboardView({ tasks, projects, members, projectFilter }) {
  const currentProject = projects.find(p => p.id === projectFilter) || projects[0];

  const v1s = {
    total: tasks.length,
    done: tasks.filter(t => t.status === "done" || t.status === "archived").length,
    todo: tasks.filter(t => t.status === "todo").length,
    in_progress: tasks.filter(t => t.status === "in_progress").length,
    to_test: tasks.filter(t => t.status === "to_test").length,
    bug: tasks.filter(t => t.status === "bug").length,
    v2: tasks.filter(t => t.status === "v2").length,
    archived: tasks.filter(t => t.status === "archived").length,
  };
  const pct = v1s.total ? Math.round((v1s.done / v1s.total) * 100) : 0;

  // Categories with counts
  const cats = {};
  tasks.forEach(t => {
    if (!cats[t.category]) cats[t.category] = { total: 0, done: 0 };
    cats[t.category].total++;
    if (t.status === "done" || t.status === "archived") cats[t.category].done++;
  });
  const topCats = Object.entries(cats).sort((a, b) => b[1].total - a[1].total).slice(0, 8);

  // Status breakdown data
  const statusBreakdown = [
    { key: "done", label: "Fait", value: v1s.done, color: "#3e9041" },
    { key: "in_progress", label: "En cours", value: v1s.in_progress, color: "#5865f2" },
    { key: "to_test", label: "À tester", value: v1s.to_test, color: "#06b6d4" },
    { key: "todo", label: "A faire", value: v1s.todo, color: "#ed9121" },
    { key: "bug", label: "Bugs", value: v1s.bug, color: "#d13b1a" },
    { key: "v2", label: "V2+", value: v1s.v2, color: "#525066" },
  ];

  return (
    <div>
      {/* Header row */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-end", marginBottom: 32, flexWrap: "wrap", gap: 16 }}>
        <div>
          <div style={{ fontSize: "0.68rem", fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.12em", color: currentProject?.color || "var(--brand-primary, #e07b39)", marginBottom: 6 }}>OpenFramework</div>
          <h2 style={{ fontSize: "1.8rem", fontWeight: 800, color: "#ffffff", letterSpacing: "-0.02em" }}>
            <span style={{ color: currentProject?.color || "var(--brand-primary, #e07b39)" }}>{currentProject?.name || "Projet"}</span> {"— Dashboard"}
          </h2>
        </div>
        <div style={{ background: "rgba(209,59,26,0.1)", border: "1px solid rgba(209,59,26,0.25)", borderRadius: 14, padding: "14px 24px", textAlign: "right" }}>
          <div style={{ fontSize: "0.68rem", color: "#d13b1a", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.06em" }}><IC icon={Clock} style={{ marginRight: 6 }} />{"Prochaine sortie"}</div>
          <div style={{ fontWeight: 700, fontSize: "1.15rem", color: "#ffffff", marginTop: 3 }}>{"15 \u2013 22 Avril 2026"}</div>
          <Countdown deadline={currentProject?.deadline} />
        </div>
      </div>

      {/* Hero: Total + progress */}
      <div style={{ background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 16, padding: "28px 32px", marginBottom: 24 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 24, marginBottom: 20, flexWrap: "wrap" }}>
          <div>
            <div style={{ fontFamily: "monospace", fontSize: "3.2rem", fontWeight: 800, color: "#ffffff", lineHeight: 1 }}>{v1s.total}</div>
            <div style={{ fontSize: "0.78rem", color: "#888888", marginTop: 4 }}>{"t\u00e2ches au total"}</div>
          </div>
          <div style={{ width: 1, height: 50, background: "rgba(255,255,255,0.08)" }} />
          <div style={{ flex: 1, minWidth: 200 }}>
            <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
              <span style={{ fontSize: "0.82rem", fontWeight: 600, color: "#3e9041" }}>{v1s.done} {"termin\u00e9es"}</span>
              <span style={{ fontFamily: "monospace", fontSize: "0.82rem", fontWeight: 700, color: "#ffffff" }}>{pct}%</span>
            </div>
            <div style={{ background: "rgba(255,255,255,0.06)", borderRadius: 8, height: 12, overflow: "hidden" }}>
              <div style={{ height: "100%", width: `${pct}%`, background: "linear-gradient(90deg, #3e9041, #4caf50)", borderRadius: 8, transition: "width 0.6s ease" }} />
            </div>
            <div style={{ fontSize: "0.72rem", color: "#888888", marginTop: 6 }}>{v1s.total - v1s.done - v1s.v2} {"restantes"}</div>
          </div>
        </div>

        {/* Status breakdown bars */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(5, 1fr)", gap: 16 }}>
          {statusBreakdown.map(s => (
            <div key={s.key}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", marginBottom: 6 }}>
                <span style={{ fontSize: "0.72rem", color: "#888888", fontWeight: 500 }}>{s.label}</span>
                <span style={{ fontFamily: "monospace", fontSize: "1.3rem", fontWeight: 700, color: s.color }}>{s.value}</span>
              </div>
              <div style={{ background: "rgba(255,255,255,0.04)", borderRadius: 4, height: 4, overflow: "hidden" }}>
                <div style={{ height: "100%", width: `${v1s.total ? (s.value / v1s.total) * 100 : 0}%`, background: s.color, borderRadius: 4, transition: "width 0.5s" }} />
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Two columns: Categories + V2 */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16, marginBottom: 32 }}>
        {/* Categories overview */}
        <div style={{ background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 14, padding: "22px 24px" }}>
          <div style={{ fontSize: "0.78rem", fontWeight: 700, color: "#ffffff", marginBottom: 16, textTransform: "uppercase", letterSpacing: "0.04em" }}><IC icon={Folder} style={{ marginRight: 8, color: "var(--brand-primary, #e07b39)" }} />{"Cat\u00e9gories"}</div>
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {topCats.map(([cat, data]) => {
              const catPct = data.total ? Math.round((data.done / data.total) * 100) : 0;
              return (
                <div key={cat}>
                  <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 3 }}>
                    <span style={{ fontSize: "0.78rem", color: "#e8eaed" }}>{cat}</span>
                    <span style={{ fontFamily: "monospace", fontSize: "0.72rem", color: "#888888" }}>{data.done}/{data.total}</span>
                  </div>
                  <div style={{ background: "rgba(255,255,255,0.04)", borderRadius: 3, height: 3, overflow: "hidden" }}>
                    <div style={{ height: "100%", width: `${catPct}%`, background: catPct === 100 ? "#3e9041" : "var(--brand-primary, #e07b39)", borderRadius: 3, transition: "width 0.4s" }} />
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Backlog reporté */}
        <div style={{ background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 14, padding: "22px 24px" }}>
          <div style={{ fontSize: "0.78rem", fontWeight: 700, color: "#ffffff", marginBottom: 16, textTransform: "uppercase", letterSpacing: "0.04em" }}><IC icon={Layers} style={{ marginRight: 8, color: "#525066" }} />{"Backlog report\u00e9"}</div>
          <div style={{ display: "flex", alignItems: "center", gap: 16, marginBottom: 16 }}>
            <div style={{ fontFamily: "monospace", fontSize: "2.2rem", fontWeight: 700, color: "#525066" }}>{v1s.v2}</div>
            <div style={{ fontSize: "0.78rem", color: "#888888" }}>{"t\u00e2ches report\u00e9es"}</div>
          </div>
          {(() => {
            const backlogTasks = tasks.filter(t => t.status === "v2");
            const backlogCats = {};
            backlogTasks.forEach(t => { backlogCats[t.category] = (backlogCats[t.category] || 0) + 1; });
            return Object.entries(backlogCats).sort((a, b) => b[1] - a[1]).slice(0, 6).map(([cat, cnt]) => (
              <div key={cat} style={{ display: "flex", justifyContent: "space-between", padding: "4px 0", borderBottom: "1px solid rgba(255,255,255,0.04)" }}>
                <span style={{ fontSize: "0.75rem", color: "#888888" }}>{cat}</span>
                <span style={{ fontFamily: "monospace", fontSize: "0.72rem", color: "#525066" }}>{cnt}</span>
              </div>
            ));
          })()}
        </div>
      </div>

      {/* Charge par personne */}
      <div style={{ fontSize: "0.78rem", fontWeight: 700, color: "#ffffff", marginBottom: 16, textTransform: "uppercase", letterSpacing: "0.04em" }}><IC icon={Users} style={{ marginRight: 8, color: "var(--brand-primary, #e07b39)" }} />{"Charge par personne"}</div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: 12 }}>
        {members.filter(m => m.id !== "equipe" && m.id !== "map").map(m => {
          const myTasks = tasks.filter(t => (t.assignees || []).includes(m.id));
          const myDone = myTasks.filter(t => t.status === "done" || t.status === "archived").length;
          const myBugs = myTasks.filter(t => t.status === "bug").length;
          const myTodo = myTasks.filter(t => t.status === "todo" || t.status === "in_progress").length;
          const myPct = myTasks.length ? Math.round((myDone / myTasks.length) * 100) : 0;
          return (
            <div key={m.id} style={{ background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 14, padding: "18px 20px" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                  <div style={{ width: 10, height: 10, borderRadius: "50%", background: m.color }} />
                  <span style={{ fontWeight: 700, fontSize: "0.9rem", color: "#ffffff" }}>{m.name}</span>
                </div>
                <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "#888888" }}>{myPct}%</span>
              </div>

              {/* Mini progress */}
              <div style={{ background: "rgba(255,255,255,0.04)", borderRadius: 4, height: 4, marginBottom: 12, overflow: "hidden" }}>
                <div style={{ height: "100%", width: `${myPct}%`, background: m.color, borderRadius: 4, transition: "width 0.4s" }} />
              </div>

              {/* Stats row */}
              <div style={{ display: "flex", gap: 16, marginBottom: 10 }}>
                <div style={{ textAlign: "center" }}>
                  <div style={{ fontFamily: "monospace", fontSize: "1.1rem", fontWeight: 700, color: "#ed9121" }}>{myTodo}</div>
                  <div style={{ fontSize: "0.65rem", color: "#888888" }}>{"restantes"}</div>
                </div>
                <div style={{ textAlign: "center" }}>
                  <div style={{ fontFamily: "monospace", fontSize: "1.1rem", fontWeight: 700, color: "#3e9041" }}>{myDone}</div>
                  <div style={{ fontSize: "0.65rem", color: "#888888" }}>{"faites"}</div>
                </div>
                {myBugs > 0 && (
                  <div style={{ textAlign: "center" }}>
                    <div style={{ fontFamily: "monospace", fontSize: "1.1rem", fontWeight: 700, color: "#d13b1a" }}>{myBugs}</div>
                    <div style={{ fontSize: "0.65rem", color: "#888888" }}>{"bugs"}</div>
                  </div>
                )}
              </div>

              {/* Top tasks */}
              {myTasks.filter(t => t.status !== "done" && t.status !== "v2" && t.status !== "archived").slice(0, 4).map(t => (
                <div key={t.id} style={{ fontSize: "0.75rem", color: "#888888", padding: "4px 8px", background: "rgba(255,255,255,0.02)", borderRadius: 5, marginBottom: 2, display: "flex", gap: 8, alignItems: "flex-start" }}>
                  <span style={{ width: 5, height: 5, borderRadius: "50%", background: STATUS_CONFIG[t.status]?.color, marginTop: 5, flexShrink: 0 }} />
                  <span>{t.text}</span>
                </div>
              ))}
              {myTasks.filter(t => t.status !== "done" && t.status !== "v2" && t.status !== "archived").length > 4 && (
                <div style={{ fontSize: "0.68rem", color: "#888888", marginTop: 3, fontStyle: "italic" }}>
                  +{myTasks.filter(t => t.status !== "done" && t.status !== "v2" && t.status !== "archived").length - 4} {"autres..."}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

// --- BOARD (KANBAN) with Drag & Drop ---
function BoardView({ tasks, members, projectFilter, onEditTask, onUpdateStatus }) {
  const filtered = projectFilter === "all" ? tasks : tasks.filter(t => t.projectId === projectFilter);
  const columns = ["todo", "in_progress", "to_test", "bug", "done"];
  const [dragId, setDragId] = useState(null);
  const [overCol, setOverCol] = useState(null);

  const handleDragStart = (e, taskId) => {
    setDragId(taskId);
    e.dataTransfer.effectAllowed = "move";
    // Make ghost semi-transparent
    requestAnimationFrame(() => {
      if (e.target) e.target.style.opacity = "0.4";
    });
  };

  const handleDragEnd = (e) => {
    e.target.style.opacity = "1";
    setDragId(null);
    setOverCol(null);
  };

  const handleDragOver = (e, status) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
    if (overCol !== status) setOverCol(status);
  };

  const handleDragLeave = (e, status) => {
    // Only clear if actually leaving the column (not entering a child)
    if (!e.currentTarget.contains(e.relatedTarget)) {
      if (overCol === status) setOverCol(null);
    }
  };

  const handleDrop = (e, status) => {
    e.preventDefault();
    if (dragId) {
      const task = tasks.find(t => t.id === dragId);
      if (task && task.status !== status) {
        onUpdateStatus(dragId, status);
      }
    }
    setDragId(null);
    setOverCol(null);
  };

  return (
    <div style={{ display: "grid", gridTemplateColumns: `repeat(${columns.length}, 1fr)`, gap: 12, minHeight: 400 }}>
      {columns.map(status => {
        const col = filtered.filter(t => t.status === status);
        const cfg = STATUS_CONFIG[status];
        const isOver = overCol === status;
        return (
          <div key={status}
            onDragOver={e => handleDragOver(e, status)}
            onDragLeave={e => handleDragLeave(e, status)}
            onDrop={e => handleDrop(e, status)}
            style={{
              background: isOver ? `${cfg.color}12` : "rgba(255,255,255,0.02)",
              borderRadius: 12, padding: 12, transition: "background 0.2s, box-shadow 0.2s",
              border: isOver ? `2px dashed ${cfg.color}60` : "2px dashed transparent",
              minHeight: 200,
            }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 14, padding: "0 4px" }}>
              <span style={{ width: 8, height: 8, borderRadius: "50%", background: cfg.color }} />
              <span style={{ fontWeight: 700, fontSize: "0.85rem", color: "#ffffff" }}>{cfg.label}</span>
              <span style={{ fontFamily: "monospace", fontSize: "0.72rem", color: "#888888", marginLeft: "auto" }}>{col.length}</span>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {col.map(t => (
                <div key={t.id}
                  draggable
                  onDragStart={e => handleDragStart(e, t.id)}
                  onDragEnd={handleDragEnd}
                  onClick={() => onEditTask(t)}
                  style={{
                    background: dragId === t.id ? "#333333" : "#2a2f3e",
                    border: "1px solid rgba(255,255,255,0.08)", borderRadius: 10,
                    padding: "10px 12px", cursor: "grab", transition: "all 0.15s",
                    userSelect: "none",
                  }}
                  onMouseEnter={e => { if (!dragId) e.currentTarget.style.borderColor = "rgba(60, 173, 217,0.35)"; }}
                  onMouseLeave={e => e.currentTarget.style.borderColor = "rgba(255,255,255,0.08)"}>
                  <div style={{ fontSize: "0.8rem", color: "#e8eaed", lineHeight: 1.45, marginBottom: 6 }}>{t.text}</div>
                  {(t.images || []).length > 0 && (
                    <div style={{ display: "flex", gap: 4, marginBottom: 6 }}>
                      {(t.images || []).slice(0, 3).map((url, i) => (
                        <img key={i} src={url} alt="" style={{ width: 40, height: 28, objectFit: "cover", borderRadius: 4, border: "1px solid rgba(255,255,255,0.1)" }} onError={e => e.target.style.display = "none"} />
                      ))}
                      {(t.images || []).length > 3 && <span style={{ fontSize: "0.65rem", color: "#888888", alignSelf: "center" }}>+{t.images.length - 3}</span>}
                    </div>
                  )}
                  <div style={{ display: "flex", gap: 4, flexWrap: "wrap", alignItems: "center" }}>
                    {(t.assignees || []).map(a => <Badge key={a} member={members.find(m => m.id === a)} small />)}
                    <PrioBadge priority={t.priority} />
                    {(t.subtasks || []).length > 0 && (
                      <span style={{ fontSize: "0.62rem", color: "#888888", display: "flex", alignItems: "center", gap: 3 }}>
                        <IC icon={Check} size={10} />{(t.subtasks || []).filter(s => s.done).length}/{(t.subtasks || []).length}
                      </span>
                    )}
                  </div>
                </div>
              ))}
              {col.length === 0 && (
                <div style={{
                  padding: "24px 12px", textAlign: "center", fontSize: "0.75rem",
                  color: "#888888", fontStyle: "italic", borderRadius: 8,
                  border: "1px dashed rgba(255,255,255,0.06)",
                }}>
                  {isOver ? "Déposer ici" : "Vide"}
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

// --- LIST VIEW ---
function ListView({ tasks, members, projectFilter, personFilter, milestones, onEditTask, onAddInCategory, onBulkUpdate }) {
  const [selected, setSelected] = useState(new Set());
  const [showBulkEdit, setShowBulkEdit] = useState(false);
  const [bulkStatus, setBulkStatus] = useState("");
  const [bulkPriority, setBulkPriority] = useState("");
  const [bulkAssignee, setBulkAssignee] = useState("");
  const [bulkMilestone, setBulkMilestone] = useState("");
  const [bulkCategory, setBulkCategory] = useState("");
  const [expandedId, setExpandedId] = useState(null);
  const [sortBy, setSortBy] = useState(null); // null | "status" | "priority"
  const [statusView, setStatusView] = useState("active"); // "active" | "done" | "bug" | "all"

  const STATUS_VIEWS = {
    active:   { label: "À faire",  statuses: ["todo", "in_progress"] },
    to_test:  { label: "À tester", statuses: ["to_test"] },
    done:     { label: "Fait",     statuses: ["done"] },
    bug:      { label: "Bugs",     statuses: ["bug"] },
    archived: { label: "Archives", statuses: ["archived"] },
    all:      { label: "Tout",     statuses: ["todo", "in_progress", "to_test", "done", "bug", "v2"] },
  };

  let filtered = tasks;
  if (projectFilter !== "all") filtered = filtered.filter(t => t.projectId === projectFilter);
  if (personFilter !== "all") filtered = filtered.filter(t => (t.assignees || []).includes(personFilter));
  const statusDef = STATUS_VIEWS[statusView];
  if (statusDef.statuses) filtered = filtered.filter(t => statusDef.statuses.includes(t.status));

  const cats = {};
  filtered.forEach(t => { (cats[t.category] = cats[t.category] || []).push(t); });

  const toggleSelect = (id, e) => {
    e.stopPropagation();
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const selectAll = () => {
    if (selected.size === filtered.length) {
      setSelected(new Set());
    } else {
      setSelected(new Set(filtered.map(t => t.id)));
    }
  };

  const applyBulk = () => {
    if (selected.size === 0) return;
    const updates = {};
    if (bulkStatus) updates.status = bulkStatus;
    if (bulkPriority === "__clear__") updates.priority = null;
    else if (bulkPriority) updates.priority = Number(bulkPriority);
    if (bulkAssignee === "__clear__") updates.assignees = [];
    else if (bulkAssignee) updates.addAssignee = bulkAssignee;
    if (bulkMilestone) updates.milestone = bulkMilestone;
    if (bulkMilestone === "__clear__") updates.milestone = "__clear__";
    if (bulkCategory) updates.category = bulkCategory;
    onBulkUpdate(Array.from(selected), updates);
    setSelected(new Set());
    setShowBulkEdit(false);
    setBulkStatus(""); setBulkPriority(""); setBulkAssignee(""); setBulkMilestone(""); setBulkCategory("");
  };

  return (
    <div>
      {/* Status view tabs */}
      <div style={{ display: "flex", gap: 4, marginBottom: 14, alignItems: "center", flexWrap: "wrap" }}>
        <div style={{ display: "flex", gap: 4, background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 10, padding: 4, width: "fit-content" }}>
          {Object.entries(STATUS_VIEWS).map(([key, { label }]) => {
            const active = statusView === key;
            const count = key === "archived" ? tasks.filter(t => t.status === "archived").length : 0;
            return (
              <button key={key} onClick={() => { setStatusView(key); setSelected(new Set()); }} style={{
                ...btnStyle, padding: "5px 14px", fontSize: "0.78rem",
                background: active ? "var(--brand-primary, #e07b39)" : "transparent",
                color: active ? "#161a26" : "#888",
                border: "none", fontWeight: active ? 700 : 400,
                borderRadius: 7, display: "flex", alignItems: "center", gap: 5,
              }}>
                {key === "archived" && <IC icon={Archive} size={12} />}
                {label}
                {key === "archived" && count > 0 && <span style={{ fontSize: "0.65rem", background: active ? "rgba(0,0,0,0.2)" : "rgba(255,255,255,0.08)", borderRadius: 8, padding: "1px 6px" }}>{count}</span>}
              </button>
            );
          })}
        </div>
        {statusView === "done" && filtered.length > 0 && (
          <button onClick={() => { onBulkUpdate(filtered.map(t => t.id), { status: "archived" }); setStatusView("archived"); }} style={{
            ...btnStyle, background: "transparent", border: "1px solid rgba(102,102,102,0.4)",
            color: "#888", fontSize: "0.75rem", padding: "5px 12px", display: "flex", alignItems: "center", gap: 5,
          }}>
            <IC icon={Archive} size={12} />Tout archiver
          </button>
        )}
      </div>

      {/* Bulk toolbar */}
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14, flexWrap: "wrap" }}>
        <button onClick={selectAll} style={{
          ...btnStyle, background: "transparent", border: "1px solid rgba(255,255,255,0.1)",
          color: selected.size === filtered.length && filtered.length > 0 ? "var(--brand-primary, #e07b39)" : "#888888",
          fontSize: "0.78rem", padding: "6px 12px",
        }}>
          {selected.size === filtered.length && filtered.length > 0
            ? <span><IC icon={SquareCheck} style={{ marginRight: 6 }} />{"Tout d\u00e9s\u00e9lectionner"}</span>
            : <span><IC icon={Square} style={{ marginRight: 6 }} />{"Tout s\u00e9lectionner"}</span>}
        </button>

        {/* Sort button */}
        {selected.size === 0 && (
          <div style={{ marginLeft: "auto", display: "flex", gap: 6 }}>
            {[
              { key: "status", label: "Statut" },
              { key: "priority", label: "Priorité" },
            ].map(({ key, label }) => (
              <button key={key} onClick={() => setSortBy(s => s === key ? null : key)} style={{
                ...btnStyle, background: sortBy === key ? "rgba(60, 173, 217,0.15)" : "transparent",
                border: `1px solid ${sortBy === key ? "rgba(60, 173, 217,0.6)" : "rgba(255,255,255,0.1)"}`,
                color: sortBy === key ? "var(--brand-primary, #e07b39)" : "#888888", fontSize: "0.75rem", padding: "5px 11px",
              }}>
                <IC icon={sortBy === key ? ArrowUp : Minus} size={11} style={{ marginRight: 5 }} />{label}
              </button>
            ))}
          </div>
        )}

        {selected.size > 0 && (
          <>
            <span style={{ fontSize: "0.78rem", color: "var(--brand-primary, #e07b39)", fontWeight: 600 }}>{selected.size} sélectionnée{selected.size > 1 ? "s" : ""}</span>
            <button onClick={() => setShowBulkEdit(!showBulkEdit)} style={{
              ...btnStyle, background: "var(--brand-primary, #e07b39)", color: "#161a26", border: "none", fontWeight: 700,
              fontSize: "0.78rem", padding: "6px 14px",
            }}><IC icon={Pen} style={{ marginRight: 6 }} />{"Modifier la s\u00e9lection"}</button>
            <button onClick={() => setSelected(new Set())} style={{
              ...btnStyle, background: "transparent", color: "#888888", border: "1px solid rgba(255,255,255,0.1)",
              fontSize: "0.78rem", padding: "6px 12px",
            }}>{"Annuler"}</button>
          </>
        )}
      </div>

      {/* Bulk edit panel */}
      {showBulkEdit && selected.size > 0 && (
        <div style={{
          background: "#2a2f3e", border: "1px solid rgba(60, 173, 217,0.3)", borderRadius: 12,
          padding: 18, marginBottom: 18, display: "flex", gap: 12, alignItems: "flex-end", flexWrap: "wrap",
        }}>
          <div>
            <label style={labelStyle}>Statut</label>
            <select value={bulkStatus} onChange={e => setBulkStatus(e.target.value)} style={{ ...inputStyle, width: 130, marginBottom: 0 }}>
              <option value="">— Ne pas changer —</option>
              {Object.entries(STATUS_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
            </select>
          </div>
          <div>
            <label style={labelStyle}>Priorité</label>
            <select value={bulkPriority} onChange={e => setBulkPriority(e.target.value)} style={{ ...inputStyle, width: 130, marginBottom: 0 }}>
              <option value="">— Ne pas changer —</option>
              <option value="__clear__">— Non priorisée —</option>
              {PRIO_LEVELS.map(n => <option key={n} value={String(n)}>{PRIO_CONFIG[n].label}</option>)}
            </select>
          </div>
          <div>
            <label style={labelStyle}>Ajouter assigné</label>
            <select value={bulkAssignee} onChange={e => setBulkAssignee(e.target.value)} style={{ ...inputStyle, width: 140, marginBottom: 0 }}>
              <option value="">— Ne pas changer —</option>
              {members.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}
              <option value="__clear__">Retirer tous</option>
            </select>
          </div>
          <div>
            <label style={labelStyle}>Milestone</label>
            <select value={bulkMilestone} onChange={e => setBulkMilestone(e.target.value)} style={{ ...inputStyle, width: 180, marginBottom: 0 }}>
              <option value="">— Ne pas changer —</option>
              {(milestones || [])
                .filter(m => !m.archived) // bulk : on n'autorise pas l'ajout dans un milestone archivé
                .sort((a, b) => new Date(a.date) - new Date(b.date))
                .map(m => (
                  <option key={m.id} value={m.id}>{m.name}</option>
                ))}
              <option value="__clear__">Retirer du milestone</option>
            </select>
          </div>
          <div>
            <label style={labelStyle}>Catégorie</label>
            <select value={bulkCategory} onChange={e => setBulkCategory(e.target.value)} style={{ ...inputStyle, width: 160, marginBottom: 0 }}>
              <option value="">— Ne pas changer —</option>
              {[...new Set(tasks.map(t => t.category).filter(Boolean))].sort().map(cat => (
                <option key={cat} value={cat}>{cat}</option>
              ))}
            </select>
          </div>
          <button onClick={applyBulk} style={{ ...btnStyle, background: "#3e9041", color: "#fff", border: "none", fontWeight: 700, padding: "8px 20px" }}>
            <IC icon={Check} style={{ marginRight: 6 }} />{"Appliquer \u00e0 "}{selected.size}{" t\u00e2che"}{selected.size > 1 ? "s" : ""}
          </button>
        </div>
      )}

      {/* Task list */}
      {Object.entries(cats).map(([cat, catTasks]) => (
        <div key={cat}>
          <div style={{ fontWeight: 700, fontSize: "0.95rem", margin: "24px 0 10px", display: "flex", alignItems: "center", gap: 10, color: "#ffffff" }}>
            {cat}
            <span style={{ fontFamily: "monospace", fontSize: "0.7rem", background: "#2a2f3e", padding: "2px 8px", borderRadius: 12, color: "#888888", border: "1px solid rgba(255,255,255,0.08)" }}>{catTasks.length}</span>
            {onAddInCategory && (
              <button
                onClick={() => onAddInCategory(cat)}
                title={`Ajouter une tâche dans "${cat}"`}
                style={{ background: "none", border: "1px solid rgba(60, 173, 217,0.4)", borderRadius: 6, color: "var(--brand-primary, #e07b39)", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center", width: 20, height: 20, padding: 0, fontSize: "1rem", lineHeight: 1, flexShrink: 0 }}
                onMouseEnter={e => { e.currentTarget.style.background = "rgba(60, 173, 217,0.15)"; e.currentTarget.style.borderColor = "var(--brand-primary, #e07b39)"; }}
                onMouseLeave={e => { e.currentTarget.style.background = "none"; e.currentTarget.style.borderColor = "rgba(60, 173, 217,0.4)"; }}
              >+</button>
            )}
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
            {(sortBy === "status"
              ? [...catTasks].sort((a, b) => {
                  const order = ["bug", "in_progress", "to_test", "todo", "done", "v2", "archived"];
                  return order.indexOf(a.status) - order.indexOf(b.status);
                })
              : sortBy === "priority"
              ? [...catTasks].sort((a, b) => {
                  const pa = a.priority == null ? -1 : a.priority;
                  const pb = b.priority == null ? -1 : b.priority;
                  return pb - pa;
                })
              : catTasks
            ).map(t => {
              const isSelected = selected.has(t.id);
              const isExpanded = expandedId === t.id;
              const assignedMembers = (t.assignees || []).map(a => members.find(m => m.id === a)).filter(Boolean);
              const milestoneName = milestones?.find(m => m.id === t.milestoneId)?.name;
              return (
                <div key={t.id}>
                  {/* Row */}
                  <div style={{
                    display: "grid", gridTemplateColumns: "28px 10px 1fr 150px 90px 80px 28px",
                    gap: 12, alignItems: "center",
                    background: isExpanded ? "rgba(60, 173, 217,0.06)" : isSelected ? "rgba(60, 173, 217,0.08)" : "#2a2f3e",
                    border: isExpanded ? "1px solid rgba(60, 173, 217,0.4)" : isSelected ? "1px solid rgba(60, 173, 217,0.4)" : "1px solid rgba(255,255,255,0.08)",
                    borderRadius: isExpanded ? "8px 8px 0 0" : 8,
                    borderBottom: isExpanded ? "none" : undefined,
                    padding: "10px 14px", cursor: "pointer", transition: "all 0.15s",
                  }}
                    onClick={() => setExpandedId(isExpanded ? null : t.id)}
                    onMouseEnter={e => { if (!isSelected && !isExpanded) e.currentTarget.style.borderColor = "rgba(60, 173, 217,0.35)"; }}
                    onMouseLeave={e => { if (!isSelected && !isExpanded) e.currentTarget.style.borderColor = "rgba(255,255,255,0.08)"; }}>
                    <div onClick={e => toggleSelect(t.id, e)} style={{
                      width: 18, height: 18, borderRadius: 4, border: isSelected ? "2px solid var(--brand-primary, #e07b39)" : "2px solid rgba(255,255,255,0.2)",
                      background: isSelected ? "var(--brand-primary, #e07b39)" : "transparent", display: "flex", alignItems: "center", justifyContent: "center",
                      cursor: "pointer", flexShrink: 0,
                    }}>
                      {isSelected && <IC icon={Check} style={{ color: "#161a26", fontSize: "0.6rem" }} />}
                    </div>
                    <span style={{ width: 10, height: 10, borderRadius: "50%", background: STATUS_CONFIG[t.status]?.color, flexShrink: 0 }} />
                    <span style={{ fontSize: "0.84rem", color: "#e8eaed", display: "flex", alignItems: "center", gap: 6, minWidth: 0 }}>
                      <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{t.text}</span>
                      {(t.images || []).length > 0 && <span style={{ fontSize: "0.65rem", color: "#888888", flexShrink: 0 }}><IC icon={Image} style={{ marginRight: 3 }} />{t.images.length}</span>}
                      {(t.subtasks || []).length > 0 && <span style={{ fontSize: "0.65rem", color: (t.subtasks || []).every(s => s.done) ? "#3e9041" : "#888888", flexShrink: 0 }}><IC icon={Check} size={11} style={{ marginRight: 2 }} />{(t.subtasks || []).filter(s => s.done).length}/{(t.subtasks || []).length}</span>}
                    </span>
                    <div style={{ display: "flex", gap: 3, flexWrap: "wrap" }}>
                      {assignedMembers.map(m => <Badge key={m.id} member={m} small />)}
                    </div>
                    <StatusBadge status={t.status} />
                    <PrioBadge priority={t.priority} />
                    <IC icon={isExpanded ? ChevronUp : ChevronDown} size={14} style={{ color: isExpanded ? "var(--brand-primary, #e07b39)" : "#555", flexShrink: 0 }} />
                  </div>

                  {/* Expanded detail panel */}
                  {isExpanded && (
                    <div style={{
                      background: "#1f2330", border: "1px solid rgba(60, 173, 217,0.4)", borderTop: "none",
                      borderRadius: "0 0 8px 8px", padding: "16px 18px",
                    }}>
                      <div style={{ display: "flex", gap: 24, flexWrap: "wrap", marginBottom: t.description || t.notes || (t.subtasks || []).length > 0 ? 14 : 0 }}>
                        {/* Assignés */}
                        {assignedMembers.length > 0 && (
                          <div>
                            <div style={{ fontSize: "0.65rem", color: "#666", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6 }}>Assigné(s)</div>
                            <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                              {assignedMembers.map(m => <Badge key={m.id} member={m} />)}
                            </div>
                          </div>
                        )}
                        {/* Statut + Priorité */}
                        <div>
                          <div style={{ fontSize: "0.65rem", color: "#666", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6 }}>Statut</div>
                          <StatusBadge status={t.status} />
                        </div>
                        <div>
                          <div style={{ fontSize: "0.65rem", color: "#666", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6 }}>Priorité</div>
                          <PrioBadge priority={t.priority} />
                        </div>
                        {/* Deadline */}
                        {t.deadline && (
                          <div>
                            <div style={{ fontSize: "0.65rem", color: "#666", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6 }}>Deadline</div>
                            <span style={{ fontSize: "0.78rem", color: "#e8eaed" }}>{t.deadline}</span>
                          </div>
                        )}
                        {/* Milestone */}
                        {milestoneName && (
                          <div>
                            <div style={{ fontSize: "0.65rem", color: "#666", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6 }}>Milestone</div>
                            <span style={{ fontSize: "0.78rem", color: "var(--brand-primary, #e07b39)" }}>{milestoneName}</span>
                          </div>
                        )}
                      </div>

                      {/* Description */}
                      {t.description && (
                        <div style={{ marginBottom: 14 }}>
                          <div style={{ fontSize: "0.65rem", color: "#666", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6 }}>Description</div>
                          <div style={{ fontSize: "0.82rem", color: "#ccc", lineHeight: 1.6, whiteSpace: "pre-wrap" }}>{t.description}</div>
                        </div>
                      )}

                      {/* Notes */}
                      {t.notes && (
                        <div style={{ marginBottom: (t.subtasks || []).length > 0 ? 14 : 0 }}>
                          <div style={{ fontSize: "0.65rem", color: "#666", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6 }}>Notes</div>
                          <div style={{ fontSize: "0.82rem", color: "#aaa", lineHeight: 1.6, whiteSpace: "pre-wrap" }}>{t.notes}</div>
                        </div>
                      )}

                      {/* Sous-tâches */}
                      {(t.subtasks || []).length > 0 && (
                        <div style={{ marginBottom: 14 }}>
                          <div style={{ fontSize: "0.65rem", color: "#666", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 8 }}>Sous-tâches</div>
                          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                            {t.subtasks.map(s => (
                              <div key={s.id} style={{ display: "flex", alignItems: "center", gap: 8 }}>
                                <span style={{
                                  width: 14, height: 14, borderRadius: 3, flexShrink: 0,
                                  background: s.done ? "#3e9041" : "transparent",
                                  border: s.done ? "2px solid #3e9041" : "2px solid rgba(255,255,255,0.2)",
                                  display: "flex", alignItems: "center", justifyContent: "center",
                                }}>
                                  {s.done && <IC icon={Check} size={9} style={{ color: "#fff" }} />}
                                </span>
                                <span style={{ fontSize: "0.8rem", color: s.done ? "#555" : "#ccc", textDecoration: s.done ? "line-through" : "none" }}>{s.text}</span>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Boutons actions */}
                      <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 4 }}>
                        {t.status === "done" && (
                          <button onClick={e => { e.stopPropagation(); onBulkUpdate([t.id], { status: "archived" }); }} style={{
                            ...btnStyle, background: "transparent", border: "1px solid rgba(102,102,102,0.4)",
                            color: "#888", fontSize: "0.78rem", padding: "6px 14px",
                          }}>
                            <IC icon={Archive} size={13} style={{ marginRight: 6 }} />Archiver
                          </button>
                        )}
                        {t.status === "archived" && (
                          <button onClick={e => { e.stopPropagation(); onBulkUpdate([t.id], { status: "done" }); }} style={{
                            ...btnStyle, background: "transparent", border: "1px solid rgba(62,144,65,0.4)",
                            color: "#3e9041", fontSize: "0.78rem", padding: "6px 14px",
                          }}>
                            <IC icon={Undo2} size={13} style={{ marginRight: 6 }} />Désarchiver
                          </button>
                        )}
                        <button onClick={e => { e.stopPropagation(); onEditTask(t); }} style={{
                          ...btnStyle, background: "transparent", border: "1px solid rgba(255,255,255,0.15)",
                          color: "#aaa", fontSize: "0.78rem", padding: "6px 14px",
                        }}>
                          <IC icon={Pencil} size={13} style={{ marginRight: 6 }} />Éditer
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      ))}
      {Object.keys(cats).length === 0 && <div style={{ color: "#888888", padding: 40, textAlign: "center" }}>Aucune tâche trouvée</div>}
    </div>
  );
}

// --- WHITEBOARD ---
function WhiteboardView({ ideas, projects, members, onAddIdea, onDeleteIdea, onConvertIdea, onUpdateIdea, onShowImport, defaultAuthor }) {
  const [newText, setNewText] = useState("");
  const [newDesc, setNewDesc] = useState("");
  const [showNewForm, setShowNewForm] = useState(false);
  const [editingIdea, setEditingIdea] = useState(null); // id of idea being edited
  const [editText, setEditText] = useState("");
  const [editDesc, setEditDesc] = useState("");
  const [openThread, setOpenThread] = useState(null);
  const [commentText, setCommentText] = useState("");
  const [commentAuthor, setCommentAuthor] = useState(defaultAuthor || null);
  useEffect(() => { if (defaultAuthor) setCommentAuthor(defaultAuthor); }, [defaultAuthor]);
  const [replyTo, setReplyTo] = useState(null);
  const [downvoteTarget, setDownvoteTarget] = useState(null);
  const [downvoteText, setDownvoteText] = useState("");
  const [promptCopied, setPromptCopied] = useState(false);

  const addComment = (ideaId) => {
    if (!commentText.trim()) return;
    const idea = ideas.find(i => i.id === ideaId);
    if (!idea) return;
    const comment = {
      id: `c_${Date.now()}`,
      author: commentAuthor,
      text: commentText.trim(),
      replyTo: replyTo,
      createdAt: Date.now(),
    };
    onUpdateIdea(ideaId, { comments: [...(idea.comments || []), comment] });
    setCommentText("");
    setReplyTo(null);
  };

  const deleteComment = (ideaId, commentId) => {
    const idea = ideas.find(i => i.id === ideaId);
    if (!idea) return;
    onUpdateIdea(ideaId, { comments: (idea.comments || []).filter(c => c.id !== commentId) });
  };

  const renderThread = (ideaId, comments, parentId) => {
    const children = (comments || []).filter(c => (c.replyTo || null) === parentId);
    if (children.length === 0) return null;
    return children.map(c => {
      const author = members.find(m => m.id === c.author);
      const isReply = parentId !== null;
      return (
        <div key={c.id} style={{ marginLeft: isReply ? 20 : 0, marginTop: 6 }}>
          <div style={{
            background: isReply ? "rgba(255,255,255,0.02)" : "rgba(255,255,255,0.04)",
            borderRadius: 8, padding: "8px 12px",
            borderLeft: `3px solid ${author?.color || "#888888"}`,
          }}>
            <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 4 }}>
              <span style={{
                fontSize: "0.68rem", fontWeight: 700, color: author?.color || "#888888",
              }}>{author?.name || c.author}</span>
              <span style={{ fontSize: "0.6rem", color: "#888888" }}>
                {new Date(c.createdAt).toLocaleDateString("fr-FR", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
              </span>
              <span style={{ flex: 1 }} />
              <button onClick={() => { setReplyTo(c.id); setCommentText(""); }}
                style={{ background: "none", border: "none", color: "#888888", cursor: "pointer", fontSize: "0.62rem", padding: "0 4px" }}>
                <IC icon={Reply} style={{ marginRight: 3 }} />{"r\u00e9pondre"}
              </button>
              <button onClick={() => deleteComment(ideaId, c.id)}
                style={{ background: "none", border: "none", color: "#888888", cursor: "pointer", fontSize: "0.62rem", padding: "0 4px" }}>
                <IC icon={Trash2} />
              </button>
            </div>
            <div style={{ fontSize: "0.78rem", color: "#e8eaed", lineHeight: 1.45 }}>{c.text}</div>
          </div>
          {renderThread(ideaId, comments, c.id)}
        </div>
      );
    });
  };

  return (
    <div>
      <h3 style={{ fontWeight: 700, fontSize: "1rem", marginBottom: 6, color: "#ffffff" }}><IC icon={Lightbulb} style={{ marginRight: 8, color: "var(--brand-primary, #e07b39)" }} />{"Tableau d\u2019id\u00e9es"}</h3>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 18 }}>
        <p style={{ fontSize: "0.8rem", color: "#888888", margin: 0 }}>{"Notez vos id\u00e9es ici. Cliquez sur une id\u00e9e pour ouvrir le fil de discussion."}</p>
        <button onClick={() => {
          const prompt = `Tu es un assistant créatif pour un studio de jeux vidéo. Génère des idées sous forme de fichier Markdown (.md) importable dans notre outil de gestion.

FORMAT REQUIS — chaque idée est une section séparée par une ligne "---" :

\`\`\`
# Titre de l'idée
Type: idee
Projet: sl-v1

Description détaillée de l'idée ici.
Tu peux écrire sur plusieurs lignes pour expliquer le contexte,
donner des références, des liens, des exemples, etc.
---
# Deuxième idée
Type: idee
Projet: sl-v1

Description de la deuxième idée...
---
\`\`\`

RÈGLES :
- Chaque section DOIT commencer par "# Titre" (titre court et clair)
- La ligne "Type: idee" est OBLIGATOIRE (pas "tâche", pas "task")
- La ligne "Projet:" accepte : sl-v1, sl-v2, sw-rp, ph
- Après une ligne vide, tout le texte est la description (multi-lignes OK)
- Sépare chaque idée par "---" sur sa propre ligne
- Pas de sous-titres (##), pas de listes à puces dans le titre
- Le fichier doit être encodé en UTF-8

Génère maintenant des idées sur le thème suivant : [DÉCRIS TON THÈME ICI]`;
          navigator.clipboard.writeText(prompt);
          setPromptCopied(true);
          setTimeout(() => setPromptCopied(false), 2000);
        }}
          style={{
            ...btnStyle, background: promptCopied ? "rgba(62,144,65,0.15)" : "transparent",
            color: promptCopied ? "#3e9041" : "#888",
            border: `1px solid ${promptCopied ? "rgba(62,144,65,0.3)" : "rgba(255,255,255,0.1)"}`,
            padding: "5px 12px",
            display: "flex", alignItems: "center", gap: 5, fontSize: "0.72rem",
            whiteSpace: "nowrap", flexShrink: 0, transition: "all 0.2s",
          }}
          title="Copier un prompt pour générer des idées au format .md avec une IA">
          <IC icon={promptCopied ? Check : Copy} size={12} />{promptCopied ? "Copié !" : "Copier prompt IA"}
        </button>
        <button onClick={onShowImport}
          style={{
            ...btnStyle, background: "transparent", color: "#888",
            border: "1px solid rgba(255,255,255,0.1)", padding: "5px 12px",
            display: "flex", alignItems: "center", gap: 5, fontSize: "0.72rem",
            whiteSpace: "nowrap", flexShrink: 0,
          }}
          title="Importer un fichier .md">
          <IC icon={FileUp} size={12} />{"Import .md"}
        </button>
      </div>

      {/* Bouton pour ouvrir le formulaire */}
      {!showNewForm ? (
        <div style={{ display: "flex", gap: 8, marginBottom: 24 }}>
          <input value={newText} onChange={e => setNewText(e.target.value)} placeholder="Nouvelle idée..."
            style={{ ...inputStyle, flex: 1, marginBottom: 0 }}
            onFocus={() => setShowNewForm(true)}
            onKeyDown={e => {
              if (e.key === "Enter" && newText.trim()) { setShowNewForm(true); }
            }} />
          <button onClick={() => setShowNewForm(true)}
            style={{ ...btnStyle, background: "var(--brand-primary, #e07b39)", color: "#161a26", border: "none", fontWeight: 700, whiteSpace: "nowrap" }}><IC icon={Plus} style={{ marginRight: 6 }} />{"Nouvelle id\u00e9e"}</button>
        </div>
      ) : (
        <div style={{
          background: "#2a2f3e", border: "1px solid rgba(60, 173, 217,0.3)",
          borderRadius: 12, padding: 16, marginBottom: 24, display: "flex", flexDirection: "column", gap: 10,
        }}>
          <div style={{ fontSize: "0.82rem", fontWeight: 700, color: "var(--brand-primary, #e07b39)" }}>
            <IC icon={Lightbulb} style={{ marginRight: 6 }} />{"Proposer une id\u00e9e"}
          </div>
          <input value={newText} onChange={e => setNewText(e.target.value)} placeholder="Titre de l'idée…"
            autoFocus
            style={{ ...inputStyle, marginBottom: 0, fontWeight: 700, fontSize: "0.9rem" }} />
          <textarea value={newDesc} onChange={e => setNewDesc(e.target.value)}
            placeholder={"Description d\u00e9taill\u00e9e, r\u00e9f\u00e9rences, liens, contexte\u2026\nExpliquez votre id\u00e9e en d\u00e9tail pour que l'\u00e9quipe comprenne bien."}
            rows={5}
            style={{
              ...inputStyle, marginBottom: 0, resize: "vertical", minHeight: 100,
              lineHeight: 1.6, fontSize: "0.83rem",
            }} />
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <button onClick={() => { setShowNewForm(false); setNewText(""); setNewDesc(""); }}
              style={{ ...btnStyle, background: "transparent", color: "#888888", border: "1px solid rgba(255,255,255,0.1)" }}>
              {"Annuler"}
            </button>
            <button onClick={() => {
              if (newText.trim()) {
                onAddIdea(newText.trim(), projects[0]?.id, newDesc.trim());
                setNewText(""); setNewDesc(""); setShowNewForm(false);
              }
            }}
              disabled={!newText.trim()}
              style={{ ...btnStyle, background: newText.trim() ? "var(--brand-primary, #e07b39)" : "#555", color: "#161a26", border: "none", fontWeight: 700, opacity: newText.trim() ? 1 : 0.5 }}>
              <IC icon={Plus} style={{ marginRight: 6 }} />{"Poster l\u2019id\u00e9e"}
            </button>
          </div>
        </div>
      )}

      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {ideas.map(idea => {
          const isOpen = openThread === idea.id;
          const commentCount = (idea.comments || []).length;
          const votes = idea.votes || {};
          const upvoters = Object.entries(votes).filter(([_, v]) => v === 1).map(([k]) => k);
          const downvoters = Object.entries(votes).filter(([_, v]) => v === -1).map(([k]) => k);
          const score = upvoters.length - downvoters.length;
          const myVote = votes[commentAuthor] || 0;
          const isDownvoting = downvoteTarget === idea.id;

          const handleVote = (e, val) => {
            e.stopPropagation();
            const newVotes = { ...votes };
            if (newVotes[commentAuthor] === val) {
              delete newVotes[commentAuthor];
              onUpdateIdea(idea.id, { votes: newVotes });
            } else if (val === -1 && newVotes[commentAuthor] !== -1) {
              // Downvote: open justification box
              setDownvoteTarget(idea.id);
              setDownvoteText("");
            } else {
              newVotes[commentAuthor] = val;
              onUpdateIdea(idea.id, { votes: newVotes });
            }
          };

          const confirmDownvote = (e) => {
            e.stopPropagation();
            const newVotes = { ...votes, [commentAuthor]: -1 };
            const newComments = [...(idea.comments || [])];
            if (downvoteText.trim()) {
              newComments.push({
                id: `c_${Date.now()}`,
                author: commentAuthor,
                text: `\u{1F44E} Contre : ${downvoteText.trim()}`,
                replyTo: null,
                createdAt: Date.now(),
              });
            }
            onUpdateIdea(idea.id, { votes: newVotes, comments: newComments });
            setDownvoteTarget(null);
            setDownvoteText("");
          };

          const cancelDownvote = (e) => {
            e.stopPropagation();
            setDownvoteTarget(null);
            setDownvoteText("");
          };

          return (
            <div key={idea.id} style={{
              background: "#2a2f3e",
              border: isOpen ? "1px solid rgba(60, 173, 217,0.3)" : isDownvoting ? "1px solid rgba(209,59,26,0.3)" : "1px solid rgba(255,255,255,0.08)",
              borderRadius: 12, overflow: "hidden", display: "flex",
            }}>
              {/* Vote column */}
              <div style={{
                display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
                padding: "12px 6px", gap: 2, minWidth: 44, borderRight: "1px solid rgba(255,255,255,0.06)",
              }}>
                <button onClick={e => handleVote(e, 1)} style={{
                  background: "none", border: "none", cursor: "pointer", fontSize: "0.85rem", padding: 2,
                  color: myVote === 1 ? "#3e9041" : "#888888", transition: "color 0.15s",
                }}><IC icon={ThumbsUp} /></button>
                <span style={{
                  fontFamily: "monospace", fontWeight: 700, fontSize: "0.95rem",
                  color: score > 0 ? "#3e9041" : score < 0 ? "#d13b1a" : "#888888",
                }}>{score}</span>
                <button onClick={e => handleVote(e, -1)} style={{
                  background: "none", border: "none", cursor: "pointer", fontSize: "0.85rem", padding: 2,
                  color: myVote === -1 ? "#d13b1a" : "#888888", transition: "color 0.15s",
                }}><IC icon={ThumbsDown} /></button>
              </div>

              {/* Content */}
              <div style={{ flex: 1 }}>
              {/* Idea header */}
              <div style={{ padding: "16px 20px", cursor: "pointer" }}
                onClick={() => { if (!isDownvoting) { setOpenThread(isOpen ? null : idea.id); setReplyTo(null); setCommentText(""); } }}>
                <div style={{ fontSize: "0.88rem", color: "#e8eaed", lineHeight: 1.5, fontWeight: 600, marginBottom: 4 }}>{idea.text}</div>
                {/* Preview description (tronquée quand fermé) */}
                {!isOpen && idea.description && (
                  <div style={{
                    fontSize: "0.78rem", color: "#888888", lineHeight: 1.4, marginBottom: 8,
                    overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: "100%",
                  }}>{idea.description}</div>
                )}
                <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
                  <span style={{ fontSize: "0.68rem", color: "#888888" }}>{projects.find(p => p.id === idea.projectId)?.name}</span>
                  <span style={{ fontSize: "0.68rem", color: "#888888" }}>
                    <IC icon={MessageCircle} style={{ marginRight: 4 }} />
                    {commentCount > 0 ? `${commentCount} commentaire${commentCount > 1 ? "s" : ""}` : "Pas de commentaire"}
                  </span>
                  {/* Voter names */}
                  {(upvoters.length > 0 || downvoters.length > 0) && (
                    <span style={{ fontSize: "0.62rem", color: "#888888" }}>
                      {upvoters.length > 0 && (
                        <span style={{ color: "#3e9041" }}>
                          {"+"}{upvoters.map(id => members.find(m => m.id === id)?.name || id).join(", ")}
                        </span>
                      )}
                      {upvoters.length > 0 && downvoters.length > 0 && " / "}
                      {downvoters.length > 0 && (
                        <span style={{ color: "#d13b1a" }}>
                          {"-"}{downvoters.map(id => members.find(m => m.id === id)?.name || id).join(", ")}
                        </span>
                      )}
                    </span>
                  )}
                  <span style={{ flex: 1 }} />
                  <button onClick={e => { e.stopPropagation(); setEditingIdea(idea.id); setEditText(idea.text); setEditDesc(idea.description || ""); }}
                    style={{ fontSize: "0.68rem", padding: "3px 8px", borderRadius: 5, border: "1px solid rgba(60, 173, 217,0.3)", background: "rgba(60, 173, 217,0.12)", color: "var(--brand-primary, #e07b39)", cursor: "pointer" }}>
                    <IC icon={Pen} style={{ marginRight: 4 }} />{"Modifier"}
                  </button>
                  <button onClick={e => { e.stopPropagation(); onConvertIdea(idea); }}
                    style={{ fontSize: "0.68rem", padding: "3px 8px", borderRadius: 5, border: "1px solid rgba(62,144,65,0.3)", background: "rgba(62,144,65,0.12)", color: "#3e9041", cursor: "pointer" }}>
                    <IC icon={ArrowRightToLine} style={{ marginRight: 4 }} />{"T\u00e2che"}
                  </button>
                  <button onClick={e => { e.stopPropagation(); onDeleteIdea(idea.id); }}
                    style={{ fontSize: "0.68rem", padding: "3px 8px", borderRadius: 5, border: "1px solid rgba(209,59,26,0.3)", background: "rgba(209,59,26,0.12)", color: "#d13b1a", cursor: "pointer" }}>
                    <IC icon={Trash2} />
                  </button>
                </div>
              </div>

              {/* Downvote justification box */}
              {isDownvoting && (
                <div onClick={e => e.stopPropagation()} style={{
                  padding: "0 20px 14px", borderTop: "1px solid rgba(209,59,26,0.15)",
                }}>
                  <div style={{ fontSize: "0.75rem", color: "#d13b1a", fontWeight: 600, marginBottom: 8, marginTop: 10 }}>
                    {"Pourquoi voter contre ?"}
                  </div>
                  <div style={{ display: "flex", gap: 8 }}>
                    <input value={downvoteText} onChange={e => setDownvoteText(e.target.value)}
                      placeholder="Justification (facultatif)..."
                      autoFocus
                      style={{ ...inputStyle, flex: 1, marginBottom: 0, fontSize: "0.78rem" }}
                      onKeyDown={e => { if (e.key === "Enter") confirmDownvote(e); if (e.key === "Escape") cancelDownvote(e); }} />
                    <button onClick={confirmDownvote}
                      style={{ ...btnStyle, background: "#d13b1a", color: "#fff", border: "none", fontWeight: 700, padding: "7px 14px", fontSize: "0.75rem" }}>
                      <IC icon={ThumbsDown} style={{ marginRight: 6 }} />{"Voter"}
                    </button>
                    <button onClick={cancelDownvote}
                      style={{ ...btnStyle, background: "transparent", color: "#888888", border: "1px solid rgba(255,255,255,0.1)", padding: "7px 10px", fontSize: "0.75rem" }}>
                      <IC icon={X} style={{ marginRight: 4 }} />{"Annuler"}
                    </button>
                  </div>
                </div>
              )}

              {/* Edit form (inline) */}
              {editingIdea === idea.id && (
                <div onClick={e => e.stopPropagation()} style={{
                  borderTop: "1px solid rgba(60, 173, 217,0.2)", padding: "14px 20px",
                  background: "rgba(60, 173, 217,0.05)", display: "flex", flexDirection: "column", gap: 10,
                }}>
                  <div style={{ fontSize: "0.78rem", fontWeight: 700, color: "var(--brand-primary, #e07b39)" }}>
                    <IC icon={Pen} style={{ marginRight: 6 }} />{"Modifier l\u2019id\u00e9e"}
                  </div>
                  <input value={editText} onChange={e => setEditText(e.target.value)}
                    style={{ ...inputStyle, marginBottom: 0, fontWeight: 700, fontSize: "0.88rem" }} />
                  <textarea value={editDesc} onChange={e => setEditDesc(e.target.value)}
                    placeholder={"Description d\u00e9taill\u00e9e, r\u00e9f\u00e9rences, liens\u2026"}
                    rows={5}
                    style={{ ...inputStyle, marginBottom: 0, resize: "vertical", minHeight: 80, lineHeight: 1.6, fontSize: "0.82rem" }} />
                  <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
                    <button onClick={() => setEditingIdea(null)}
                      style={{ ...btnStyle, background: "transparent", color: "#888888", border: "1px solid rgba(255,255,255,0.1)", fontSize: "0.75rem" }}>
                      {"Annuler"}
                    </button>
                    <button onClick={() => {
                      if (editText.trim()) {
                        onUpdateIdea(idea.id, { text: editText.trim(), description: editDesc.trim() });
                        setEditingIdea(null);
                      }
                    }}
                      style={{ ...btnStyle, background: "var(--brand-primary, #e07b39)", color: "#161a26", border: "none", fontWeight: 700, fontSize: "0.75rem" }}>
                      {"Enregistrer"}
                    </button>
                  </div>
                </div>
              )}

              {/* Thread panel */}
              {isOpen && (
                <div style={{ borderTop: "1px solid rgba(255,255,255,0.06)", padding: "14px 20px", background: "rgba(0,0,0,0.15)" }}>
                  {/* Description complète */}
                  {idea.description && (
                    <div style={{
                      background: "rgba(255,255,255,0.03)", borderRadius: 8,
                      padding: "12px 14px", marginBottom: 14,
                      borderLeft: "3px solid var(--brand-primary, #e07b39)",
                    }}>
                      <div style={{
                        fontSize: "0.68rem", fontWeight: 700, color: "#888888",
                        textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6,
                      }}>{"Description"}</div>
                      <div style={{
                        fontSize: "0.82rem", color: "#e8eaed", lineHeight: 1.7,
                        whiteSpace: "pre-wrap", wordBreak: "break-word",
                      }}>{idea.description}</div>
                    </div>
                  )}

                  {/* Existing comments */}
                  {commentCount > 0 ? (
                    <div style={{ marginBottom: 14 }}>
                      {renderThread(idea.id, idea.comments, null)}
                    </div>
                  ) : (
                    <div style={{ fontSize: "0.75rem", color: "#888888", marginBottom: 14, fontStyle: "italic" }}>
                      <IC icon={MessageCircle} style={{ marginRight: 6 }} />{"Aucun commentaire. Lancez la discussion !"}
                    </div>
                  )}

                  {/* Reply indicator */}
                  {replyTo && (
                    <div style={{ fontSize: "0.7rem", color: "var(--brand-primary, #e07b39)", marginBottom: 6, display: "flex", alignItems: "center", gap: 6 }}>
                      <IC icon={Reply} style={{ marginRight: 4 }} />{"En r\u00e9ponse \u00e0"} {members.find(m => m.id === (idea.comments || []).find(c => c.id === replyTo)?.author)?.name || "..."}
                      <button onClick={() => setReplyTo(null)} style={{ background: "none", border: "none", color: "#888888", cursor: "pointer", fontSize: "0.65rem" }}><IC icon={X} style={{ marginRight: 3 }} />{"annuler"}</button>
                    </div>
                  )}

                  {/* Comment input */}
                  <div style={{ display: "flex", gap: 8, alignItems: "flex-end" }}>
                    <select value={commentAuthor} onChange={e => setCommentAuthor(e.target.value)}
                      style={{ ...inputStyle, width: 110, marginBottom: 0, fontSize: "0.75rem", padding: "7px 8px" }}>
                      {members.filter(m => m.id !== "equipe" && m.id !== "map").map(m => (
                        <option key={m.id} value={m.id}>{m.name}</option>
                      ))}
                    </select>
                    <input value={commentText} onChange={e => setCommentText(e.target.value)}
                      placeholder={replyTo ? "R\u00e9pondre..." : "Ajouter un commentaire..."}
                      style={{ ...inputStyle, flex: 1, marginBottom: 0, fontSize: "0.78rem" }}
                      onKeyDown={e => { if (e.key === "Enter") addComment(idea.id); }} />
                    <button onClick={() => addComment(idea.id)}
                      style={{ ...btnStyle, background: "var(--brand-primary, #e07b39)", color: "#161a26", border: "none", fontWeight: 700, padding: "7px 14px", fontSize: "0.75rem" }}>
                      <IC icon={SendHorizontal} style={{ marginRight: 6 }} />{"Envoyer"}
                    </button>
                  </div>
                </div>
              )}
              </div>
            </div>
          );
        })}
      </div>
      {ideas.length === 0 && <div style={{ color: "#888888", padding: 40, textAlign: "center" }}>{"Pas encore d\u2019id\u00e9es. Commencez \u00e0 brainstormer !"}</div>}
    </div>
  );
}

// --- ACTIVITY LOG ---
const LOG_ICONS = {
  create: Plus,
  edit: Pen,
  delete: Trash2,
  status: ArrowRightToLine,
  idea: Lightbulb,
  convert: Rocket,
  bulk: Layers,
};
const LOG_COLORS = {
  create: "#3e9041",
  edit: "var(--brand-primary, #e07b39)",
  delete: "#d13b1a",
  status: "#5865f2",
  idea: "#ed9121",
  convert: "#3e9041",
  bulk: "#525066",
};

const ACTION_VIEW = {
  create: "board", edit: "board", delete: "board", status: "board",
  bulk: "list", convert: "board",
  idea: "whiteboard",
  map_annotate: "mapview",
};
const ACTION_LABEL = {
  board: "Board", list: "Liste", whiteboard: "Idées", mapview: "Map",
};

function ActivityLogView({ log, members, onNavigate }) {
  const [filter, setFilter] = useState("all");

  const filtered = filter === "all" ? log : log.filter(l => l.author === filter);

  const timeAgo = (ts) => {
    const t = typeof ts === "number" ? ts : Date.parse(ts);
    if (!t || isNaN(t)) return "—";
    const diff = Date.now() - t;
    if (diff < 60000) return "maintenant";
    if (diff < 3600000) return `il y a ${Math.floor(diff / 60000)}min`;
    if (diff < 4 * 3600000) return `il y a ${Math.floor(diff / 3600000)}h`;
    return new Date(t).toLocaleDateString("fr-FR", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
  };

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 20 }}>
        <h2 style={{ fontSize: "1.3rem", fontWeight: 800, color: "#ffffff" }}>
          <IC icon={Activity} size={18} style={{ marginRight: 10, color: "var(--brand-primary, #e07b39)" }} />
          {"Activit\u00e9 r\u00e9cente"}
        </h2>
        <select value={filter} onChange={e => setFilter(e.target.value)} style={{ ...inputStyle, width: 140, marginBottom: 0, fontSize: "0.78rem", padding: "6px 10px" }}>
          <option value="all">{"Tout le monde"}</option>
          {members.filter(m => m.id !== "equipe" && m.id !== "map").map(m => <option key={m.id} value={m.id}>{m.name}</option>)}
        </select>
      </div>

      {filtered.length === 0 ? (
        <div style={{ color: "#888888", padding: 40, textAlign: "center", fontSize: "0.85rem" }}>{"Aucune activit\u00e9 enregistr\u00e9e"}</div>
      ) : (
        <div style={{ position: "relative", paddingLeft: 24 }}>
          {/* Timeline line */}
          <div style={{ position: "absolute", left: 9, top: 0, bottom: 0, width: 2, background: "rgba(255,255,255,0.06)" }} />

          {filtered.map(entry => {
            const member = members.find(m => m.id === entry.author);
            const IconComp = LOG_ICONS[entry.action] || Pen;
            const color = LOG_COLORS[entry.action] || "#888888";
            return (
              <div key={entry.id} style={{ display: "flex", gap: 14, marginBottom: 6, position: "relative" }}>
                {/* Dot on timeline */}
                <div style={{
                  width: 20, height: 20, borderRadius: "50%", background: "#2a2f3e",
                  border: `2px solid ${color}`, display: "flex", alignItems: "center", justifyContent: "center",
                  position: "absolute", left: -24, zIndex: 1,
                }}>
                  <IC icon={IconComp} size={10} style={{ color }} />
                </div>

                {/* Content */}
                <div style={{
                  flex: 1, background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.06)",
                  borderRadius: 8, padding: "10px 14px",
                }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 3 }}>
                    {member && (
                      <>
                        <MemberAvatar member={member} size={20} />
                        <span style={{ fontSize: "0.72rem", fontWeight: 700, color: member.color }}>{member.name}</span>
                      </>
                    )}
                    <span style={{ fontSize: "0.65rem", color: "#888888" }}>{timeAgo(entry.timestamp ?? entry.created_at)}</span>
                    {ACTION_VIEW[entry.action] && onNavigate && (
                      <button onClick={() => onNavigate(ACTION_VIEW[entry.action])} style={{ marginLeft: "auto", fontSize: "0.65rem", color: color, background: `${color}18`, border: `1px solid ${color}40`, borderRadius: 5, padding: "2px 8px", cursor: "pointer", fontFamily: "inherit", fontWeight: 600, flexShrink: 0 }}>
                        {ACTION_LABEL[ACTION_VIEW[entry.action]] || ACTION_VIEW[entry.action]} →
                      </button>
                    )}
                  </div>
                  <div style={{ fontSize: "0.8rem", color: "#e8eaed", lineHeight: 1.4 }}>{entry.detail}</div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

// --- MY BOARD (personal kanban) ---
function MyBoardView({ tasks, members, myId, onEditTask, onUpdateStatus }) {
  const myTasks = tasks.filter(t => (t.assignees || []).includes(myId));
  const columns = ["todo", "in_progress", "to_test", "bug", "done"];
  const member = members.find(m => m.id === myId);
  const [draggedId, setDraggedId] = useState(null);
  const [dragOverCol, setDragOverCol] = useState(null);

  const stats = {
    total: myTasks.length,
    done: myTasks.filter(t => t.status === "done").length,
    active: myTasks.filter(t => t.status !== "done" && t.status !== "v2" && t.status !== "archived").length,
  };
  const pct = stats.total ? Math.round((stats.done / stats.total) * 100) : 0;

  return (
    <div>
      {/* Personal header */}
      <div style={{ display: "flex", alignItems: "center", gap: 16, marginBottom: 24 }}>
        <div style={{ width: 40, height: 40, borderRadius: 10, overflow: "hidden", flexShrink: 0 }}>
          {member?.avatar
            ? <img src={member.avatar} alt={member.name} style={{ width: "100%", height: "100%", objectFit: "cover" }} onError={e => { e.target.style.display="none"; e.target.parentElement.style.background=member.color; }} />
            : <div style={{ width: "100%", height: "100%", background: member?.color || "#888", display: "flex", alignItems: "center", justifyContent: "center" }}><IC icon={User} size={20} style={{ color: "#fff" }} /></div>
          }
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ fontWeight: 800, fontSize: "1.2rem", color: "#ffffff" }}>{member?.name || myId}</div>
          <div style={{ fontSize: "0.75rem", color: "#888888" }}>{member?.role} {"— "}{stats.active}{" t\u00e2ches actives"}</div>
        </div>
        <div style={{ textAlign: "right" }}>
          <div style={{ fontFamily: "monospace", fontSize: "1.6rem", fontWeight: 700, color: "#3e9041" }}>{pct}%</div>
          <div style={{ fontSize: "0.68rem", color: "#888888" }}>{stats.done}/{stats.total}{" termin\u00e9es"}</div>
        </div>
      </div>

      {/* Progress bar */}
      <div style={{ background: "rgba(255,255,255,0.05)", borderRadius: 8, height: 8, marginBottom: 24, overflow: "hidden" }}>
        <div style={{ height: "100%", width: `${pct}%`, background: member?.color || "#3e9041", borderRadius: 8, transition: "width 0.5s" }} />
      </div>

      {/* Kanban columns */}
      <div style={{ display: "grid", gridTemplateColumns: `repeat(${columns.length}, 1fr)`, gap: 12, minHeight: 300 }}>
        {columns.map(status => {
          const col = myTasks.filter(t => t.status === status);
          const cfg = STATUS_CONFIG[status];
          const isOver = dragOverCol === status;
          return (
            <div key={status}
              onDragOver={e => { e.preventDefault(); setDragOverCol(status); }}
              onDragLeave={() => setDragOverCol(null)}
              onDrop={() => {
                if (draggedId && draggedId !== status) {
                  const t = myTasks.find(t => t.id === draggedId);
                  if (t && t.status !== status) onUpdateStatus(draggedId, status);
                }
                setDraggedId(null); setDragOverCol(null);
              }}
              style={{ borderRadius: 10, transition: "background 0.15s", background: isOver ? cfg.color + "11" : "transparent", outline: isOver ? `2px dashed ${cfg.color}55` : "2px dashed transparent", padding: 4 }}
            >
              <div style={{ fontSize: "0.72rem", fontWeight: 700, textTransform: "uppercase", color: cfg.color, marginBottom: 10, display: "flex", alignItems: "center", gap: 6 }}>
                <span style={{ width: 8, height: 8, borderRadius: "50%", background: cfg.color }} />
                {cfg.label}
                <span style={{ fontFamily: "monospace", color: "#888888", fontWeight: 400 }}>({col.length})</span>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {col.map(t => {
                  const stDone = (t.subtasks || []).filter(s => s.done).length;
                  const stTotal = (t.subtasks || []).length;
                  const isDragging = draggedId === t.id;
                  return (
                    <div key={t.id}
                      draggable
                      onDragStart={() => setDraggedId(t.id)}
                      onDragEnd={() => { setDraggedId(null); setDragOverCol(null); }}
                      onClick={() => onEditTask(t)}
                      style={{
                        background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 10,
                        padding: "10px 12px", cursor: "grab", transition: "border-color 0.15s, opacity 0.15s",
                        opacity: isDragging ? 0.4 : 1,
                        userSelect: "none",
                      }}
                      onMouseEnter={e => { if (!draggedId) e.currentTarget.style.borderColor = "rgba(60, 173, 217,0.35)"; }}
                      onMouseLeave={e => e.currentTarget.style.borderColor = "rgba(255,255,255,0.08)"}
                    >
                      <div style={{ fontSize: "0.8rem", color: "#e8eaed", lineHeight: 1.45, marginBottom: 6 }}>{t.text}</div>
                      {stTotal > 0 && (
                        <div style={{ marginBottom: 6 }}>
                          <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 2 }}>
                            <span style={{ fontSize: "0.62rem", color: "#888888" }}><IC icon={Check} size={10} style={{ marginRight: 2 }} />{stDone}/{stTotal}</span>
                          </div>
                          <div style={{ background: "rgba(255,255,255,0.05)", borderRadius: 3, height: 3, overflow: "hidden" }}>
                            <div style={{ height: "100%", width: `${stTotal ? (stDone / stTotal) * 100 : 0}%`, background: stDone === stTotal ? "#3e9041" : "var(--brand-primary, #e07b39)", borderRadius: 3 }} />
                          </div>
                        </div>
                      )}
                      <div style={{ display: "flex", gap: 4, alignItems: "center" }}>
                        <PrioBadge priority={t.priority} />
                        {t.deadline && (
                          <span style={{ fontSize: "0.62rem", color: new Date(t.deadline) < new Date() ? "#d13b1a" : "#888888" }}>
                            <IC icon={Clock} size={10} style={{ marginRight: 2 }} />
                            {new Date(t.deadline).toLocaleDateString("fr-FR", { day: "numeric", month: "short" })}
                          </span>
                        )}
                      </div>
                    </div>
                  );
                })}
                {col.length === 0 && (
                  <div style={{ padding: "20px 10px", textAlign: "center", fontSize: "0.72rem", color: isOver ? cfg.color : "#888888", fontStyle: "italic", border: `1px dashed ${isOver ? cfg.color + "55" : "rgba(255,255,255,0.06)"}`, borderRadius: 8, transition: "all 0.15s" }}>
                    {isOver ? "Déposer ici" : "Vide"}
                  </div>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// --- ROADMAP TIMELINE ---
function RoadmapView({ milestones, setMilestones, tasks, members, onEditTask }) {
  const [editingMs, setEditingMs] = useState(null);
  const [editMsValues, setEditMsValues] = useState({ name: "", date: "", color: "var(--brand-primary, #e07b39)", description: "" });
  const [showAddMs, setShowAddMs] = useState(false);
  const [newMs, setNewMs] = useState({ name: "", date: "", color: "var(--brand-primary, #e07b39)", description: "" });
  const [dragTaskId, setDragTaskId] = useState(null);
  const [overMs, setOverMs] = useState(null);
  const [dragOverTaskId, setDragOverTaskId] = useState(null); // task id we're hovering over
  const [dragOverPos, setDragOverPos] = useState(null);      // 'before' | 'after'
  const [showUnassigned, setShowUnassigned] = useState(false);
  const [collapsedMs, setCollapsedMs] = useState({});
  const [filterStatus, setFilterStatus] = useState("all");
  const [archiveView, setArchiveView] = useState("active"); // "active" | "archived" | "all"

  const now = new Date();
  const archivedCount = milestones.filter(m => m.archived).length;
  const visibleMilestones = milestones.filter(m =>
    archiveView === "all" ? true : archiveView === "archived" ? m.archived : !m.archived
  );
  const sorted = [...visibleMilestones].sort((a, b) => new Date(a.date) - new Date(b.date));

  // Active milestones (non-archived) drive global stats and unassigned-task computation
  const activeMilestones = milestones.filter(m => !m.archived);
  const assignedTaskIds = new Set(activeMilestones.flatMap(m => m.taskIds || []));
  const unassignedTasks = tasks.filter(t => !assignedTaskIds.has(t.id) && t.status !== "done" && t.status !== "v2" && t.status !== "archived");

  // Global stats — exclude archived milestones from the headline progress
  const allMsTasks = activeMilestones.flatMap(m => (m.taskIds || []).map(id => tasks.find(t => t.id === id)).filter(Boolean));
  const totalDone = allMsTasks.filter(t => t.status === "done").length;
  const globalPct = allMsTasks.length ? Math.round((totalDone / allMsTasks.length) * 100) : 0;
  const nextMs = [...activeMilestones].sort((a, b) => new Date(a.date) - new Date(b.date)).find(m => new Date(m.date) >= now);
  const overdueMs = activeMilestones.filter(m => {
    const msTasks = (m.taskIds || []).map(id => tasks.find(t => t.id === id)).filter(Boolean);
    const doneCnt = msTasks.filter(t => t.status === "done").length;
    return new Date(m.date) < now && doneCnt < msTasks.length;
  });

  const addMilestone = () => {
    if (!newMs.name || !newMs.date) return;
    setMilestones(prev => [...prev, { ...newMs, id: `ms_${Date.now()}`, taskIds: [] }]);
    setNewMs({ name: "", date: "", color: "var(--brand-primary, #e07b39)", description: "" });
    setShowAddMs(false);
  };

  const deleteMilestone = (id) => {
    if (!confirm("Supprimer ce milestone ?")) return;
    setMilestones(prev => prev.filter(m => m.id !== id));
  };

  const updateMilestone = (id, updates) => {
    setMilestones(prev => prev.map(m => m.id === id ? { ...m, ...updates } : m));
  };

  const toggleArchive = (id) => {
    setMilestones(prev => prev.map(m => m.id === id
      ? { ...m, archived: !m.archived, archivedAt: !m.archived ? Date.now() : null }
      : m));
  };

  const togglePublic = (id) => {
    setMilestones(prev => prev.map(m => m.id === id ? { ...m, public: !m.public } : m));
  };

  // Insert taskId into msId at position relative to anchorId ('before'|'after'), or at end if no anchor
  const insertTaskInMilestone = (msId, taskId, anchorId = null, position = "after") => {
    // Garde-fou : on n'insère jamais dans un milestone archivé (lecture seule)
    const targetMs = milestones.find(m => m.id === msId);
    if (targetMs?.archived) return;

    setMilestones(prev => prev.map(m => {
      // Remove from every milestone first
      const filtered = (m.taskIds || []).filter(id => id !== taskId);
      if (m.id !== msId) return { ...m, taskIds: filtered };
      if (!anchorId) return { ...m, taskIds: [...filtered, taskId] };
      const anchorIdx = filtered.indexOf(anchorId);
      if (anchorIdx === -1) return { ...m, taskIds: [...filtered, taskId] };
      const insertAt = position === "before" ? anchorIdx : anchorIdx + 1;
      const next = [...filtered];
      next.splice(insertAt, 0, taskId);
      return { ...m, taskIds: next };
    }));
  };

  const removeTaskFromMilestone = (msId, taskId) => {
    setMilestones(prev => prev.map(m =>
      m.id === msId ? { ...m, taskIds: (m.taskIds || []).filter(id => id !== taskId) } : m
    ));
  };

  const toggleCollapse = (msId) => setCollapsedMs(p => ({ ...p, [msId]: !p[msId] }));

  // Drag handlers
  const handleDragStart = (e, taskId) => {
    setDragTaskId(taskId);
    e.dataTransfer.effectAllowed = "move";
    requestAnimationFrame(() => { if (e.target) e.target.style.opacity = "0.4"; });
  };
  const handleDragEnd = (e) => {
    e.target.style.opacity = "1";
    setDragTaskId(null);
    setOverMs(null);
    setDragOverTaskId(null);
    setDragOverPos(null);
  };
  const handleDragOverMs = (e, msId) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
    if (overMs !== msId) setOverMs(msId);
  };
  const handleDragLeaveMs = (e, msId) => {
    if (!e.currentTarget.contains(e.relatedTarget)) {
      if (overMs === msId) setOverMs(null);
    }
  };
  const handleDropMs = (e, msId) => {
    e.preventDefault();
    if (dragTaskId) insertTaskInMilestone(msId, dragTaskId, null, "after");
    setDragTaskId(null);
    setOverMs(null);
    setDragOverTaskId(null);
    setDragOverPos(null);
  };
  // Per-task drag-over: detect top/bottom half to pick before/after
  const handleDragOverTask = (e, taskId) => {
    e.preventDefault();
    e.stopPropagation();
    const rect = e.currentTarget.getBoundingClientRect();
    const pos = e.clientY < rect.top + rect.height / 2 ? "before" : "after";
    setDragOverTaskId(taskId);
    setDragOverPos(pos);
  };
  const handleDragLeaveTask = (e) => {
    if (!e.currentTarget.contains(e.relatedTarget)) {
      setDragOverTaskId(null);
      setDragOverPos(null);
    }
  };
  const handleDropOnTask = (e, msId, anchorTaskId) => {
    e.preventDefault();
    e.stopPropagation();
    if (dragTaskId) insertTaskInMilestone(msId, dragTaskId, anchorTaskId, dragOverPos || "after");
    setDragTaskId(null);
    setOverMs(null);
    setDragOverTaskId(null);
    setDragOverPos(null);
  };

  const msColors = ["var(--brand-primary, #e07b39)", "#5865f2", "#ed9121", "#3e9041", "#d13b1a", "#9b59b6", "#525066", "#e74c3c", "#00b5d8", "#f39c12"];

  // Days until milestone
  function daysUntil(dateStr) {
    const d = new Date(dateStr);
    const diff = Math.ceil((d - now) / (1000 * 60 * 60 * 24));
    return diff;
  }

  function daysLabel(days) {
    if (days === 0) return "Aujourd'hui";
    if (days < 0) return `${Math.abs(days)}j dépassé`;
    if (days === 1) return "Demain";
    return `Dans ${days}j`;
  }

  // Status filter on tasks
  function filterTasks(taskList) {
    if (filterStatus === "all") return taskList;
    return taskList.filter(t => t.status === filterStatus);
  }

  const STATUS_FILTER_OPTIONS = [
    { value: "all",         label: "Tous" },
    { value: "todo",        label: "À faire" },
    { value: "in_progress", label: "En cours" },
    { value: "to_test",     label: "À tester" },
    { value: "done",        label: "Fait" },
    { value: "bug",         label: "Bug" },
    { value: "v2",          label: "V2" },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>

      {/* ── Header bar ───────────────────────────────────────────── */}
      <div style={{
        display: "flex", justifyContent: "space-between", alignItems: "center",
        marginBottom: 20, flexWrap: "wrap", gap: 12,
      }}>
        <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
          <h2 style={{ fontSize: "1.25rem", fontWeight: 800, color: "#ffffff", margin: 0 }}>
            <IC icon={Route} style={{ marginRight: 8, color: "var(--brand-primary, #e07b39)" }} />
            <span style={{ color: "var(--brand-primary, #e07b39)" }}>Roadmap</span>
          </h2>
          <span style={{ fontSize: "0.72rem", color: "#888888", background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 20, padding: "3px 10px" }}>
            {sorted.length} milestone{sorted.length !== 1 ? "s" : ""}
          </span>
          {/* Archive view tabs */}
          <div style={{ display: "flex", gap: 3, background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 8, padding: 3 }}>
            {[
              { key: "active",   label: "Actifs" },
              { key: "archived", label: "Archivés", count: archivedCount },
              { key: "all",      label: "Tout" },
            ].map(({ key, label, count }) => {
              const active = archiveView === key;
              return (
                <button key={key} onClick={() => setArchiveView(key)} style={{
                  ...btnStyle, padding: "3px 10px", fontSize: "0.72rem",
                  background: active ? "var(--brand-primary, #e07b39)" : "transparent",
                  color: active ? "#161a26" : "#888",
                  border: "none", fontWeight: active ? 700 : 500,
                  borderRadius: 6, display: "flex", alignItems: "center", gap: 4,
                }}>
                  {key === "archived" && <IC icon={Archive} size={11} />}
                  {label}
                  {count > 0 && <span style={{ fontSize: "0.62rem", background: active ? "rgba(0,0,0,0.2)" : "rgba(255,255,255,0.08)", borderRadius: 6, padding: "0 5px" }}>{count}</span>}
                </button>
              );
            })}
          </div>
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          {/* Status filter */}
          <select
            value={filterStatus}
            onChange={e => setFilterStatus(e.target.value)}
            style={{
              background: "#1a1f2c", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8,
              color: "#aaaaaa", fontSize: "0.78rem", padding: "6px 10px", cursor: "pointer",
              fontFamily: "inherit", outline: "none",
            }}>
            {STATUS_FILTER_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
          <button onClick={() => setShowUnassigned(!showUnassigned)} style={{
            ...btnStyle,
            background: showUnassigned ? "rgba(237,145,33,0.12)" : "transparent",
            color: showUnassigned ? "#ed9121" : "#888888",
            border: `1px solid ${showUnassigned ? "rgba(237,145,33,0.3)" : "rgba(255,255,255,0.1)"}`,
            fontSize: "0.78rem", padding: "6px 14px", display: "flex", alignItems: "center", gap: 6,
          }}>
            <IC icon={showUnassigned ? EyeOff : Inbox} size={14} />
            Non assignées
            <span style={{ background: showUnassigned ? "rgba(237,145,33,0.2)" : "rgba(255,255,255,0.07)", borderRadius: 10, padding: "1px 7px", fontSize: "0.7rem", color: showUnassigned ? "#ed9121" : "#888888" }}>
              {unassignedTasks.length}
            </span>
          </button>
          <button
            onClick={() => setShowAddMs(true)}
            style={{ ...btnStyle, background: "var(--brand-primary, #e07b39)", color: "#111111", border: "none", fontWeight: 700, fontSize: "0.78rem", padding: "6px 16px", display: "flex", alignItems: "center", gap: 6 }}>
            <IC icon={Plus} size={14} />Milestone
          </button>
        </div>
      </div>

      {/* ── Global progress bar ───────────────────────────────────── */}
      {allMsTasks.length > 0 && (
        <div style={{ marginBottom: 24, background: "#1a1f2c", borderRadius: 12, padding: "14px 18px", border: "1px solid rgba(255,255,255,0.06)" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 10, flexWrap: "wrap", gap: 8 }}>
            <div style={{ display: "flex", gap: 20 }}>
              <div style={{ display: "flex", flexDirection: "column" }}>
                <span style={{ fontSize: "0.65rem", color: "#888888", textTransform: "uppercase", letterSpacing: "0.06em" }}>Progression globale</span>
                <span style={{ fontSize: "1.1rem", fontWeight: 800, color: "#e8eaed" }}>
                  {globalPct}<span style={{ fontSize: "0.7rem", color: "#888888" }}>%</span>
                  <span style={{ fontSize: "0.72rem", color: "#888888", fontWeight: 400, marginLeft: 8 }}>{totalDone}/{allMsTasks.length} tâches</span>
                </span>
              </div>
              {nextMs && (
                <div style={{ display: "flex", flexDirection: "column", borderLeft: "1px solid rgba(255,255,255,0.06)", paddingLeft: 20 }}>
                  <span style={{ fontSize: "0.65rem", color: "#888888", textTransform: "uppercase", letterSpacing: "0.06em" }}>Prochain milestone</span>
                  <span style={{ fontSize: "0.85rem", fontWeight: 700, color: nextMs.color }}>{nextMs.name}</span>
                  <span style={{ fontSize: "0.7rem", color: "#888888" }}>{daysLabel(daysUntil(nextMs.date))}</span>
                </div>
              )}
            </div>
            {overdueMs.length > 0 && (
              <span style={{ fontSize: "0.72rem", background: "rgba(209,59,26,0.1)", color: "#d13b1a", border: "1px solid rgba(209,59,26,0.2)", borderRadius: 8, padding: "4px 10px" }}>
                ⚠ {overdueMs.length} milestone{overdueMs.length > 1 ? "s" : ""} en retard
              </span>
            )}
          </div>
          {/* Track bar */}
          <div style={{ background: "rgba(255,255,255,0.05)", borderRadius: 8, height: 8, overflow: "hidden", position: "relative" }}>
            <div style={{ height: "100%", width: `${globalPct}%`, background: "linear-gradient(90deg, var(--brand-primary, #e07b39), #ed9121)", borderRadius: 8, transition: "width 0.5s ease" }} />
          </div>
        </div>
      )}

      {/* ── Add milestone form ────────────────────────────────────── */}
      {showAddMs && (
        <div style={{
          background: "#1a1f2c", border: "1px solid rgba(60, 173, 217,0.25)", borderRadius: 14,
          padding: "20px 22px", marginBottom: 24,
          boxShadow: "0 4px 24px rgba(0,0,0,0.3)",
        }}>
          <div style={{ fontWeight: 700, fontSize: "0.8rem", color: "var(--brand-primary, #e07b39)", textTransform: "uppercase", letterSpacing: "0.08em", marginBottom: 16, display: "flex", alignItems: "center", gap: 8 }}>
            <IC icon={Plus} size={13} /> Nouveau milestone
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 180px", gap: 12 }}>
            <div>
              <label style={labelStyle}>Nom du milestone</label>
              <input
                value={newMs.name}
                onChange={e => setNewMs(p => ({ ...p, name: e.target.value }))}
                placeholder="Ex: Update Police…"
                autoFocus
                onKeyDown={e => e.key === "Enter" && addMilestone()}
                style={{ ...inputStyle, marginBottom: 0 }}
              />
            </div>
            <div>
              <label style={labelStyle}>Date cible</label>
              <input type="date" value={newMs.date} onChange={e => setNewMs(p => ({ ...p, date: e.target.value }))} style={{ ...inputStyle, marginBottom: 0 }} />
            </div>
          </div>
          <div>
            <label style={labelStyle}>Description (optionnel)</label>
            <input value={newMs.description} onChange={e => setNewMs(p => ({ ...p, description: e.target.value }))} placeholder="Objectif ou contexte de ce milestone…" style={{ ...inputStyle, marginBottom: 0 }} />
          </div>
          <div style={{ marginTop: 12 }}>
            <label style={labelStyle}>Couleur</label>
            <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
              {msColors.map(c => (
                <div key={c} onClick={() => setNewMs(p => ({ ...p, color: c }))} style={{
                  width: 26, height: 26, borderRadius: 6, background: c, cursor: "pointer",
                  border: newMs.color === c ? `3px solid #ffffff` : "3px solid transparent",
                  boxShadow: newMs.color === c ? `0 0 0 1px ${c}` : "none",
                  transition: "all 0.15s",
                }} />
              ))}
            </div>
          </div>
          <div style={{ display: "flex", gap: 8, marginTop: 16 }}>
            <button onClick={addMilestone} style={{ ...btnStyle, background: "#3e9041", color: "#fff", border: "none", fontWeight: 700, display: "flex", alignItems: "center", gap: 6 }}>
              <IC icon={Check} size={14} />Créer le milestone
            </button>
            <button onClick={() => { setShowAddMs(false); setNewMs({ name: "", date: "", color: "var(--brand-primary, #e07b39)", description: "" }); }}
              style={{ ...btnStyle, background: "transparent", color: "#888888", border: "1px solid rgba(255,255,255,0.1)", display: "flex", alignItems: "center", gap: 6 }}>
              <IC icon={X} size={14} />Annuler
            </button>
          </div>
        </div>
      )}

      {/* ── Timeline ─────────────────────────────────────────────── */}
      <div style={{ position: "relative", paddingLeft: 32 }}>
        {/* Vertical spine */}
        <div style={{ position: "absolute", left: 14, top: 8, bottom: 8, width: 2, background: "linear-gradient(180deg, rgba(60, 173, 217,0.5) 0%, rgba(255,255,255,0.05) 100%)", borderRadius: 2 }} />

        {sorted.map((ms, i) => {
          const msTasks = (ms.taskIds || []).map(id => tasks.find(t => t.id === id)).filter(Boolean);
          const filteredMsTasks = filterTasks(msTasks);
          const doneCnt = msTasks.filter(t => t.status === "done").length;
          const pct = msTasks.length ? Math.round((doneCnt / msTasks.length) * 100) : 0;
          const isOver = overMs === ms.id;
          const isPast = new Date(ms.date) < now;
          const isCompleted = msTasks.length > 0 && doneCnt === msTasks.length;
          const isCollapsed = collapsedMs[ms.id];
          const days = daysUntil(ms.date);

          // Status badge
          let statusBadge = null;
          if (ms.archived) statusBadge = { label: "📦 Archivé", bg: "rgba(102,102,102,0.15)", color: "#aaa", border: "rgba(102,102,102,0.3)" };
          else if (isCompleted) statusBadge = { label: "✓ Complété", bg: "rgba(62,144,65,0.15)", color: "#3e9041", border: "rgba(62,144,65,0.3)" };
          else if (isPast) statusBadge = { label: "⚠ En retard", bg: "rgba(209,59,26,0.12)", color: "#d13b1a", border: "rgba(209,59,26,0.25)" };
          else if (days <= 7) statusBadge = { label: "🔥 Imminent", bg: "rgba(237,145,33,0.12)", color: "#ed9121", border: "rgba(237,145,33,0.3)" };

          // Dot style
          const dotBg = ms.archived ? "#666" : isCompleted ? "#3e9041" : isPast ? "#d13b1a" : ms.color;

          return (
            <div key={ms.id}
              style={{ marginBottom: 20, position: "relative" }}
              onDragOver={ms.archived ? undefined : e => handleDragOverMs(e, ms.id)}
              onDragLeave={ms.archived ? undefined : e => handleDragLeaveMs(e, ms.id)}
              onDrop={ms.archived ? undefined : e => handleDropMs(e, ms.id)}>

              {/* Timeline dot */}
              <div style={{
                position: "absolute", left: -24, top: 18,
                width: 16, height: 16, borderRadius: "50%",
                background: dotBg,
                border: `3px solid #11151f`,
                boxShadow: `0 0 0 2px ${dotBg}40`,
                zIndex: 2, transition: "all 0.2s",
              }} />

              {/* Milestone card */}
              <div style={{
                background: isOver ? `${ms.color}10` : "#161a26",
                border: isOver
                  ? `2px dashed ${ms.color}70`
                  : ms.archived
                    ? "1px dashed rgba(255,255,255,0.08)"
                    : isCompleted
                      ? "1px solid rgba(62,144,65,0.2)"
                      : isPast
                        ? "1px solid rgba(209,59,26,0.2)"
                        : "1px solid rgba(255,255,255,0.07)",
                borderRadius: 14,
                transition: "all 0.2s",
                overflow: "hidden",
                opacity: ms.archived ? 0.6 : 1,
              }}>

                {/* Color accent top strip */}
                <div style={{ height: 3, background: `linear-gradient(90deg, ${ms.color}, ${ms.color}40)` }} />

                {/* Card header */}
                <div style={{ padding: "14px 18px 10px" }}>
                  {editingMs === ms.id ? (
                    /* ── Edit mode ── */
                    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                      <div style={{ display: "grid", gridTemplateColumns: "1fr 180px", gap: 10 }}>
                        <div>
                          <label style={labelStyle}>Nom</label>
                          <input value={editMsValues.name} onChange={e => setEditMsValues(p => ({ ...p, name: e.target.value }))}
                            autoFocus style={{ ...inputStyle, marginBottom: 0 }} />
                        </div>
                        <div>
                          <label style={labelStyle}>Date cible</label>
                          <input type="date" value={editMsValues.date} onChange={e => setEditMsValues(p => ({ ...p, date: e.target.value }))}
                            style={{ ...inputStyle, marginBottom: 0 }} />
                        </div>
                      </div>
                      <div>
                        <label style={labelStyle}>Description</label>
                        <input value={editMsValues.description || ""} onChange={e => setEditMsValues(p => ({ ...p, description: e.target.value }))}
                          placeholder="Objectif de ce milestone…" style={{ ...inputStyle, marginBottom: 0 }} />
                      </div>
                      <div>
                        <label style={labelStyle}>Couleur</label>
                        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                          {msColors.map(c => (
                            <div key={c} onClick={() => setEditMsValues(p => ({ ...p, color: c }))} style={{
                              width: 24, height: 24, borderRadius: 6, background: c, cursor: "pointer",
                              border: editMsValues.color === c ? "3px solid #ffffff" : "3px solid transparent",
                              boxShadow: editMsValues.color === c ? `0 0 0 1px ${c}` : "none",
                              transition: "all 0.15s",
                            }} />
                          ))}
                        </div>
                      </div>
                      <div style={{ display: "flex", gap: 8 }}>
                        <button onClick={() => { updateMilestone(ms.id, editMsValues); setEditingMs(null); }}
                          style={{ ...btnStyle, background: "#3e9041", color: "#fff", border: "none", fontWeight: 700, display: "flex", alignItems: "center", gap: 6 }}>
                          <IC icon={Check} size={13} />Sauvegarder
                        </button>
                        <button onClick={() => setEditingMs(null)}
                          style={{ ...btnStyle, background: "transparent", color: "#888888", border: "1px solid rgba(255,255,255,0.1)", display: "flex", alignItems: "center", gap: 6 }}>
                          <IC icon={X} size={13} />Annuler
                        </button>
                      </div>
                    </div>
                  ) : (
                    /* ── View mode ── */
                    <div>
                      <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                        {/* Milestone name */}
                        <span style={{ fontWeight: 800, fontSize: "1rem", color: ms.color }}>{ms.name}</span>

                        {/* Public badge */}
                        {ms.public && (
                          <span style={{
                            fontSize: "0.6rem", fontWeight: 700, padding: "2px 7px", borderRadius: 20,
                            background: "rgba(62,144,65,0.12)", color: "#3e9041",
                            border: "1px solid rgba(62,144,65,0.3)",
                            display: "inline-flex", alignItems: "center", gap: 4, letterSpacing: "0.04em",
                          }} title="Visible sur la roadmap publique">
                            <IC icon={Eye} size={10} />PUBLIC
                          </span>
                        )}

                        {/* Date badge */}
                        <span style={{
                          fontFamily: "monospace", fontSize: "0.72rem", color: isPast ? "#d13b1a" : "#888888",
                          background: isPast ? "rgba(209,59,26,0.08)" : "rgba(255,255,255,0.05)",
                          border: `1px solid ${isPast ? "rgba(209,59,26,0.2)" : "rgba(255,255,255,0.06)"}`,
                          padding: "2px 8px", borderRadius: 6, display: "flex", alignItems: "center", gap: 5,
                        }}>
                          <IC icon={Clock} size={11} style={{ opacity: 0.7 }} />
                          {new Date(ms.date).toLocaleDateString("fr-FR", { day: "numeric", month: "short", year: "numeric" })}
                          {" · "}{daysLabel(days)}
                        </span>

                        {/* Status badge */}
                        {statusBadge && (
                          <span style={{ fontSize: "0.65rem", fontWeight: 700, padding: "2px 8px", borderRadius: 20, background: statusBadge.bg, color: statusBadge.color, border: `1px solid ${statusBadge.border}` }}>
                            {statusBadge.label}
                          </span>
                        )}

                        {/* Task count */}
                        <span style={{ fontSize: "0.7rem", color: "#888888" }}>
                          {doneCnt}/{msTasks.length} tâche{msTasks.length !== 1 ? "s" : ""}
                        </span>

                        <span style={{ flex: 1 }} />

                        {/* Actions */}
                        <div style={{ display: "flex", gap: 4, alignItems: "center" }}>
                          <button onClick={() => { setEditingMs(ms.id); setEditMsValues({ name: ms.name, date: ms.date, color: ms.color, description: ms.description || "" }); }}
                            style={{ background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.06)", borderRadius: 6, color: "#888888", cursor: "pointer", padding: "4px 8px", display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem" }} title="Éditer">
                            <IC icon={Pencil} size={12} />
                          </button>
                          <button onClick={() => toggleCollapse(ms.id)}
                            style={{ background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.06)", borderRadius: 6, color: "#888888", cursor: "pointer", padding: "4px 8px", display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem" }} title={isCollapsed ? "Déplier" : "Replier"}>
                            <IC icon={isCollapsed ? ChevronDown : ChevronUp} size={12} />
                          </button>
                          <button onClick={() => togglePublic(ms.id)}
                            style={{
                              background: ms.public ? "rgba(62,144,65,0.12)" : "rgba(255,255,255,0.04)",
                              border: `1px solid ${ms.public ? "rgba(62,144,65,0.35)" : "rgba(255,255,255,0.06)"}`,
                              borderRadius: 6, color: ms.public ? "#3e9041" : "#888888",
                              cursor: "pointer", padding: "4px 8px", display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem",
                            }} title={ms.public ? "Visible sur le site public — cliquer pour cacher" : "Privé — cliquer pour publier sur le site"}>
                            <IC icon={ms.public ? Eye : EyeOff} size={12} />
                          </button>
                          <button onClick={() => toggleArchive(ms.id)}
                            style={{
                              background: ms.archived ? "rgba(102,102,102,0.12)" : "rgba(255,255,255,0.04)",
                              border: `1px solid ${ms.archived ? "rgba(102,102,102,0.25)" : "rgba(255,255,255,0.06)"}`,
                              borderRadius: 6, color: ms.archived ? "#bbb" : "#888888",
                              cursor: "pointer", padding: "4px 8px", display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem",
                            }} title={ms.archived ? "Désarchiver" : "Archiver"}>
                            <IC icon={Archive} size={12} />
                          </button>
                          <button onClick={() => deleteMilestone(ms.id)}
                            style={{ background: "rgba(209,59,26,0.06)", border: "1px solid rgba(209,59,26,0.12)", borderRadius: 6, color: "#d13b1a", cursor: "pointer", padding: "4px 8px", display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem" }} title="Supprimer">
                            <IC icon={Trash2} size={12} />
                          </button>
                        </div>
                      </div>

                      {/* Description */}
                      {ms.description && (
                        <p style={{ margin: "6px 0 0", fontSize: "0.78rem", color: "#888888", lineHeight: 1.5 }}>{ms.description}</p>
                      )}
                    </div>
                  )}
                </div>

                {/* Progress bar */}
                {msTasks.length > 0 && !editingMs !== ms.id && (
                  <div style={{ padding: "0 18px 14px" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 5 }}>
                      <span style={{ fontSize: "0.65rem", color: "#888888", textTransform: "uppercase", letterSpacing: "0.05em" }}>Progression</span>
                      <span style={{ fontSize: "0.65rem", fontWeight: 700, color: pct === 100 ? "#3e9041" : ms.color }}>{pct}%</span>
                    </div>
                    <div style={{ background: "rgba(255,255,255,0.05)", borderRadius: 6, height: 6, overflow: "hidden" }}>
                      <div style={{
                        height: "100%", width: `${pct}%`,
                        background: pct === 100 ? "#3e9041" : `linear-gradient(90deg, ${ms.color}, ${ms.color}cc)`,
                        borderRadius: 6, transition: "width 0.4s ease",
                      }} />
                    </div>
                    {/* Status mini breakdown */}
                    <div style={{ display: "flex", gap: 10, marginTop: 8, flexWrap: "wrap" }}>
                      {Object.entries(STATUS_CONFIG).map(([key, cfg]) => {
                        const cnt = msTasks.filter(t => t.status === key).length;
                        if (!cnt) return null;
                        return (
                          <span key={key} style={{ fontSize: "0.65rem", color: cfg.color, display: "flex", alignItems: "center", gap: 4 }}>
                            <span style={{ width: 6, height: 6, borderRadius: "50%", background: cfg.color, display: "inline-block" }} />
                            {cfg.label} ({cnt})
                          </span>
                        );
                      })}
                    </div>
                  </div>
                )}

                {/* Tasks list */}
                {!isCollapsed && (
                  <div style={{ borderTop: "1px solid rgba(255,255,255,0.05)", padding: "10px 14px 14px" }}>
                    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                      {filteredMsTasks.map(t => {
                        const isHovered = dragOverTaskId === t.id;
                        return (
                          <div key={t.id} style={{ position: "relative" }}
                            onDragOver={ms.archived ? undefined : e => handleDragOverTask(e, t.id)}
                            onDragLeave={ms.archived ? undefined : handleDragLeaveTask}
                            onDrop={ms.archived ? undefined : e => handleDropOnTask(e, ms.id, t.id)}>
                            {/* Insert indicator — top */}
                            {isHovered && dragOverPos === "before" && (
                              <div style={{ position: "absolute", top: -2, left: 0, right: 0, height: 3, borderRadius: 2, background: ms.color, zIndex: 10, boxShadow: `0 0 6px ${ms.color}` }} />
                            )}
                          <div draggable onDragStart={e => handleDragStart(e, t.id)} onDragEnd={handleDragEnd}
                            style={{
                              display: "flex", alignItems: "center", gap: 8, padding: "7px 10px",
                              background: isHovered ? `${ms.color}10` : "rgba(255,255,255,0.025)",
                              borderRadius: 8, cursor: "grab",
                              border: isHovered ? `1px solid ${ms.color}50` : "1px solid rgba(255,255,255,0.04)",
                              transition: "background 0.1s, border-color 0.1s",
                            }}
                            onMouseEnter={e => { if (!dragTaskId) { e.currentTarget.style.background = "rgba(255,255,255,0.05)"; e.currentTarget.style.borderColor = "rgba(255,255,255,0.09)"; } }}
                            onMouseLeave={e => { if (!dragTaskId) { e.currentTarget.style.background = "rgba(255,255,255,0.025)"; e.currentTarget.style.borderColor = "rgba(255,255,255,0.04)"; } }}>
                            {/* Status dot */}
                            <span style={{ width: 8, height: 8, borderRadius: "50%", background: STATUS_CONFIG[t.status]?.color, flexShrink: 0, boxShadow: `0 0 5px ${STATUS_CONFIG[t.status]?.color}60` }} />
                            {/* Task text */}
                            <span style={{ fontSize: "0.8rem", color: "#e8eaed", flex: 1, cursor: "pointer", lineHeight: 1.4 }} onClick={() => onEditTask(t)}>{t.text}</span>
                            {/* Category */}
                            {t.category && (
                              <span style={{ fontSize: "0.62rem", color: "#888888", background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.06)", borderRadius: 4, padding: "1px 6px" }}>{t.category}</span>
                            )}
                            {/* Priority */}
                            <PrioBadge priority={t.priority} />
                            {/* Assignees */}
                            <div style={{ display: "flex", gap: 3 }}>
                              {(t.assignees || []).slice(0, 3).map(a => {
                                const m = members.find(x => x.id === a);
                                return m ? <MemberAvatar key={a} member={m} size={20} style={{ border: `2px solid #161a26` }} /> : null;
                              })}
                              {(t.assignees || []).length > 3 && (
                                <span style={{ width: 20, height: 20, borderRadius: "50%", background: "rgba(255,255,255,0.1)", display: "flex", alignItems: "center", justifyContent: "center", fontSize: "0.55rem", color: "#888", border: "2px solid #161a26", flexShrink: 0 }}>+{(t.assignees || []).length - 3}</span>
                              )}
                            </div>
                            {/* Remove button */}
                            <button onClick={e => { e.stopPropagation(); removeTaskFromMilestone(ms.id, t.id); }}
                              style={{ background: "none", border: "none", color: "#666666", cursor: "pointer", padding: "2px 4px", borderRadius: 4, display: "flex", alignItems: "center", flexShrink: 0 }}
                              onMouseEnter={e => e.currentTarget.style.color = "#d13b1a"}
                              onMouseLeave={e => e.currentTarget.style.color = "#666666"}
                              title="Retirer du milestone">
                              <IC icon={X} size={12} />
                            </button>
                          </div>
                            {/* Insert indicator — bottom */}
                            {isHovered && dragOverPos === "after" && (
                              <div style={{ position: "absolute", bottom: -2, left: 0, right: 0, height: 3, borderRadius: 2, background: ms.color, zIndex: 10, boxShadow: `0 0 6px ${ms.color}` }} />
                            )}
                          </div>
                        );
                      })}
                      {filterStatus !== "all" && filteredMsTasks.length === 0 && msTasks.length > 0 && (
                        <div style={{ padding: "10px", textAlign: "center", fontSize: "0.75rem", color: "#888888" }}>
                          Aucune tâche avec ce filtre
                        </div>
                      )}
                      {/* Drop zone — masquée sur les milestones archivés (lecture seule) */}
                      {!ms.archived && (
                        <div style={{
                          padding: isOver ? "18px 10px" : "10px",
                          textAlign: "center", fontSize: "0.73rem",
                          color: isOver ? ms.color : "#555555",
                          fontStyle: "italic",
                          border: `1px dashed ${isOver ? ms.color + "80" : "rgba(255,255,255,0.06)"}`,
                          borderRadius: 8, marginTop: 2,
                          background: isOver ? `${ms.color}08` : "transparent",
                          transition: "all 0.2s",
                        }}>
                          <IC icon={isOver ? ArrowDown : Inbox} size={13} style={{ marginRight: 6, opacity: 0.6 }} />
                          {isOver ? "Déposer ici" : "Glisser des tâches ici"}
                        </div>
                      )}
                    </div>
                  </div>
                )}
                {isCollapsed && msTasks.length > 0 && (
                  <div style={{ padding: "6px 18px 12px", display: "flex", gap: 6, flexWrap: "wrap" }}>
                    {msTasks.slice(0, 5).map(t => (
                      <span key={t.id} style={{
                        fontSize: "0.65rem", background: STATUS_CONFIG[t.status]?.bg, color: STATUS_CONFIG[t.status]?.color,
                        border: `1px solid ${STATUS_CONFIG[t.status]?.color}30`, borderRadius: 6, padding: "2px 8px",
                        maxWidth: 150, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
                      }} title={t.text}>{t.text}</span>
                    ))}
                    {msTasks.length > 5 && <span style={{ fontSize: "0.65rem", color: "#888888" }}>+{msTasks.length - 5} autres</span>}
                  </div>
                )}
              </div>
            </div>
          );
        })}

        {sorted.length === 0 && (
          <div style={{
            textAlign: "center", padding: "56px 24px",
            color: "#555555", fontSize: "0.85rem",
            background: "#161a26", border: "1px dashed rgba(255,255,255,0.06)",
            borderRadius: 14,
          }}>
            <IC icon={archiveView === "archived" ? Archive : Route} size={32} style={{ marginBottom: 12, color: "var(--brand-primary, #e07b39)", opacity: 0.4, display: "block", margin: "0 auto 14px" }} />
            <div style={{ fontWeight: 700, color: "#888888", marginBottom: 6 }}>
              {archiveView === "archived" ? "Aucun milestone archivé" : archiveView === "active" && archivedCount > 0 ? "Aucun milestone actif" : "Aucun milestone"}
            </div>
            <div style={{ fontSize: "0.78rem" }}>
              {archiveView === "archived" ? "Les milestones archivés apparaîtront ici."
                : archiveView === "active" && archivedCount > 0 ? `${archivedCount} milestone${archivedCount > 1 ? "s" : ""} archivé${archivedCount > 1 ? "s" : ""} — bascule sur l'onglet Archivés pour les voir.`
                : "Créez votre premier milestone pour structurer la roadmap."}
            </div>
            {archiveView !== "archived" && (
              <button onClick={() => setShowAddMs(true)} style={{ ...btnStyle, marginTop: 16, background: "var(--brand-primary, #e07b39)", color: "#11151f", border: "none", fontWeight: 700, display: "inline-flex", alignItems: "center", gap: 6 }}>
                <IC icon={Plus} size={13} />Créer un milestone
              </button>
            )}
          </div>
        )}
      </div>

      {/* ── Unassigned tasks drawer ───────────────────────────────── */}
      {showUnassigned && (
        <div style={{ marginTop: 28, background: "#161a26", border: "1px solid rgba(255,255,255,0.06)", borderRadius: 14, padding: "18px 20px" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14 }}>
            <h3 style={{ fontWeight: 700, fontSize: "0.9rem", color: "#e8eaed", margin: 0, display: "flex", alignItems: "center", gap: 8 }}>
              <IC icon={Inbox} size={15} style={{ color: "#ed9121" }} />
              Tâches non assignées à un milestone
            </h3>
            <span style={{ background: "rgba(237,145,33,0.12)", color: "#ed9121", border: "1px solid rgba(237,145,33,0.2)", borderRadius: 12, padding: "1px 8px", fontSize: "0.7rem", fontWeight: 700 }}>
              {unassignedTasks.length}
            </span>
          </div>
          {unassignedTasks.length === 0 ? (
            <div style={{ fontSize: "0.78rem", color: "#888888", textAlign: "center", padding: "20px 0" }}>
              🎉 Toutes les tâches actives sont assignées à un milestone.
            </div>
          ) : (
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 6 }}>
              {unassignedTasks.slice(0, 60).map(t => (
                <div key={t.id} draggable onDragStart={e => handleDragStart(e, t.id)} onDragEnd={handleDragEnd}
                  style={{
                    display: "flex", alignItems: "center", gap: 8, padding: "8px 10px",
                    background: "#222222", border: "1px solid rgba(255,255,255,0.06)", borderRadius: 8,
                    cursor: "grab", fontSize: "0.78rem", color: "#e8eaed", transition: "all 0.12s",
                  }}
                  onMouseEnter={e => { e.currentTarget.style.borderColor = "rgba(60, 173, 217,0.3)"; e.currentTarget.style.background = "#262626"; }}
                  onMouseLeave={e => { e.currentTarget.style.borderColor = "rgba(255,255,255,0.06)"; e.currentTarget.style.background = "#222222"; }}>
                  <span style={{ width: 7, height: 7, borderRadius: "50%", background: STATUS_CONFIG[t.status]?.color, flexShrink: 0 }} />
                  <span style={{ flex: 1, lineHeight: 1.4, cursor: "pointer" }} onClick={() => onEditTask(t)}>{t.text}</span>
                  {t.category && <span style={{ fontSize: "0.62rem", color: "#888888", flexShrink: 0 }}>{t.category}</span>}
                </div>
              ))}
              {unassignedTasks.length > 60 && (
                <div style={{ fontSize: "0.75rem", color: "#888888", padding: "10px", gridColumn: "1/-1", textAlign: "center" }}>
                  +{unassignedTasks.length - 60} autres tâches non assignées
                </div>
              )}
            </div>
          )}
          <div style={{ marginTop: 12, fontSize: "0.72rem", color: "#666666", fontStyle: "italic" }}>
            💡 Glissez une tâche sur un milestone pour l'y assigner.
          </div>
        </div>
      )}
    </div>
  );
}

// ============================================================
// MAP CANVAS VIEW — Interactive map annotation per project
// ============================================================
function MapCanvasView({ projects, members, annotations, setAnnotations, logActivity, defaultAuthor, tasks, onCreateTask, onEditTask }) {
  const [selectedProject, setSelectedProject] = useState(projects[0]?.id || "sl-v1");
  const [tool, setTool] = useState("select"); // select, pin, rect, circle, draw
  const [currentAuthor, setCurrentAuthor] = useState(defaultAuthor || null);
  const [lightbox, setLightbox] = useState(null);
  useEffect(() => { if (defaultAuthor) setCurrentAuthor(defaultAuthor); }, [defaultAuthor]);
  // Sync selectedProject when the projects list changes (e.g. global project filter)
  useEffect(() => {
    if (projects.length > 0 && !projects.find(p => p.id === selectedProject)) {
      setSelectedProject(projects[0].id);
    }
  }, [projects]);
  const canvasRef = useRef(null);
  const containerRef = useRef(null);

  // Pan/zoom state
  const [camera, setCamera] = useState({ x: 0, y: 0, zoom: 1 });
  const [isPanning, setIsPanning] = useState(false);
  const panStart = useRef({ x: 0, y: 0, cx: 0, cy: 0 });

  // Drawing state
  const [drawing, setDrawing] = useState(false);
  const [drawStart, setDrawStart] = useState(null);
  const [currentPath, setCurrentPath] = useState([]);
  const [previewShape, setPreviewShape] = useState(null);

  // Selection & comment
  const [selectedId, setSelectedId] = useState(null);
  const [commentText, setCommentText] = useState("");
  const [commentImageUrl, setCommentImageUrl] = useState("");
  const [showCommentImageInput, setShowCommentImageInput] = useState(false);
  const [imageUrl, setImageUrl] = useState("");
  const [showImageInput, setShowImageInput] = useState(false);
  const [commentSort, setCommentSort] = useState("asc"); // asc = oldest first, desc = newest first

  // Get annotations for current project
  const projAnnotations = annotations[selectedProject] || { imageUrl: "", items: [] };
  const items = projAnnotations.items || [];
  const bgImage = projAnnotations.imageUrl || "";

  const updateProjectAnnotations = (updater) => {
    setAnnotations(prev => {
      const current = prev[selectedProject] || { imageUrl: "", items: [] };
      const updated = typeof updater === "function" ? updater(current) : { ...current, ...updater };
      return { ...prev, [selectedProject]: updated };
    });
  };

  const addItem = (item) => {
    updateProjectAnnotations(prev => ({ ...prev, items: [...(prev.items || []), { ...item, id: `ann_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`, author: currentAuthor, createdAt: Date.now(), comments: [] }] }));
    logActivity("map_annotate", `Annotation ajoutée sur ${projects.find(p => p.id === selectedProject)?.name || selectedProject}`, currentAuthor);
  };

  const deleteItem = (id) => {
    updateProjectAnnotations(prev => ({ ...prev, items: (prev.items || []).filter(i => i.id !== id) }));
    setSelectedId(null);
  };

  const addComment = (itemId) => {
    if (!commentText.trim() && !commentImageUrl.trim()) return;
    const comment = {
      id: `c_${Date.now()}`,
      text: commentText,
      author: currentAuthor,
      createdAt: Date.now(),
      images: commentImageUrl.trim() ? [commentImageUrl.trim()] : [],
    };
    updateProjectAnnotations(prev => ({
      ...prev,
      items: (prev.items || []).map(i => i.id === itemId ? { ...i, comments: [...(i.comments || []), comment] } : i),
    }));
    setCommentText("");
    setCommentImageUrl("");
    setShowCommentImageInput(false);
  };

  const deleteComment = (itemId, commentId) => {
    updateProjectAnnotations(prev => ({
      ...prev,
      items: (prev.items || []).map(i => i.id === itemId
        ? { ...i, comments: (i.comments || []).filter(c => c.id !== commentId) }
        : i),
    }));
  };

  const linkTask = (itemId, taskId) => {
    updateProjectAnnotations(prev => ({
      ...prev,
      items: (prev.items || []).map(i => i.id === itemId ? { ...i, taskId: taskId || null } : i),
    }));
  };

  const createTaskFromPoint = (item) => {
    const newTask = {
      projectId: selectedProject,
      category: "Map",
      text: item.label || `Point carte — ${projects.find(p => p.id === selectedProject)?.name || ""}`,
      assignees: [],
      status: "todo",
      priority: null,
      deadline: null,
      notes: `Point créé depuis la carte : ${item.label || ""}`,
    };
    onCreateTask(newTask, (savedTask) => {
      linkTask(item.id, savedTask.id);
    });
  };

  const setBgImage = () => {
    updateProjectAnnotations(prev => ({ ...prev, imageUrl: imageUrl }));
    setShowImageInput(false);
    setImageUrl("");
  };

  // Screen → world coords
  const screenToWorld = (sx, sy) => {
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect) return { x: 0, y: 0 };
    return {
      x: (sx - rect.left - camera.x) / camera.zoom,
      y: (sy - rect.top - camera.y) / camera.zoom,
    };
  };

  // Mouse handlers
  const handleMouseDown = (e) => {
    if (e.button === 2) return; // ignore right click
    // Middle-click always pans. Left-click in select mode pans if clicking on canvas bg (not on an annotation)
    const isCanvasBg = e.target === containerRef.current || e.target === canvasRef.current || e.target.tagName === "rect" || e.target.tagName === "IMG";
    if (e.button === 1 || (e.button === 0 && tool === "select" && isCanvasBg)) {
      setIsPanning(true);
      panStart.current = { x: e.clientX, y: e.clientY, cx: camera.x, cy: camera.y };
      e.preventDefault();
      return;
    }
    if (tool === "select") return;

    const world = screenToWorld(e.clientX, e.clientY);
    setDrawing(true);
    setDrawStart(world);

    if (tool === "pin") {
      addItem({ type: "pin", x: world.x, y: world.y, label: "Nouveau point" });
      setDrawing(false);
    } else if (tool === "draw") {
      setCurrentPath([world]);
    }
  };

  const handleMouseMove = (e) => {
    if (isPanning) {
      setCamera(prev => ({
        ...prev,
        x: panStart.current.cx + (e.clientX - panStart.current.x),
        y: panStart.current.cy + (e.clientY - panStart.current.y),
      }));
      return;
    }
    if (!drawing || !drawStart) return;
    const world = screenToWorld(e.clientX, e.clientY);

    if (tool === "rect" || tool === "circle") {
      setPreviewShape({ type: tool, x: Math.min(drawStart.x, world.x), y: Math.min(drawStart.y, world.y), w: Math.abs(world.x - drawStart.x), h: Math.abs(world.y - drawStart.y) });
    } else if (tool === "draw") {
      setCurrentPath(prev => [...prev, world]);
    }
  };

  const handleMouseUp = () => {
    if (isPanning) { setIsPanning(false); return; }
    if (!drawing) return;
    setDrawing(false);

    if ((tool === "rect" || tool === "circle") && previewShape && previewShape.w > 5 && previewShape.h > 5) {
      addItem({ type: tool, x: previewShape.x, y: previewShape.y, w: previewShape.w, h: previewShape.h, label: "" });
    } else if (tool === "draw" && currentPath.length > 2) {
      addItem({ type: "path", points: currentPath, label: "" });
    }
    setPreviewShape(null);
    setCurrentPath([]);
    setDrawStart(null);
  };

  const handleWheel = (e) => {
    e.preventDefault();
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect) return;
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;
    const factor = e.deltaY < 0 ? 1.12 : 1 / 1.12;
    const newZoom = Math.max(0.1, Math.min(5, camera.zoom * factor));
    setCamera(prev => ({
      zoom: newZoom,
      x: mx - (mx - prev.x) * (newZoom / prev.zoom),
      y: my - (my - prev.y) * (newZoom / prev.zoom),
    }));
  };

  // Undo last
  const undoLast = () => {
    updateProjectAnnotations(prev => ({ ...prev, items: (prev.items || []).slice(0, -1) }));
  };

  const selectedItem = items.find(i => i.id === selectedId);
  const authorMember = (id) => members.find(m => m.id === id);

  const toolBtns = [
    { id: "select", icon: MousePointer, label: "Sélection" },
    { id: "pin", icon: MapPin, label: "Marqueur" },
    { id: "rect", icon: RectangleHorizontal, label: "Rectangle" },
    { id: "circle", icon: Circle, label: "Cercle" },
    { id: "draw", icon: Pencil, label: "Dessin libre" },
  ];

  const renderItem = (item) => {
    const isSelected = item.id === selectedId;
    const memberColor = authorMember(item.author)?.color || "var(--brand-primary, #e07b39)";
    const color = item.color || memberColor;
    const textColor = item.textColor || "#e8eaed";
    const baseStyle = { position: "absolute", cursor: "pointer" };

    const handleClick = (e) => { e.stopPropagation(); setSelectedId(item.id); };

    if (item.type === "pin") {
      return (
        <div key={item.id} onClick={handleClick} style={{ ...baseStyle, left: item.x - 12, top: item.y - 28, zIndex: isSelected ? 50 : 10 }}>
          <MapPin size={24} fill={color} color="#161a26" strokeWidth={1.5} />
          {item.label && <div style={{ position: "absolute", top: -20, left: "50%", transform: "translateX(-50%)", background: "#2a2f3e", border: `1px solid ${color}50`, borderRadius: 5, padding: "2px 8px", fontSize: "0.68rem", color: textColor, whiteSpace: "nowrap", fontWeight: 600 }}>{item.label}</div>}
          {isSelected && <div style={{ position: "absolute", top: -4, left: -4, width: 32, height: 36, border: `2px solid ${color}`, borderRadius: 6 }} />}
        </div>
      );
    }

    if (item.type === "rect") {
      return (
        <div key={item.id} onClick={handleClick} style={{ ...baseStyle, left: item.x, top: item.y, width: item.w, height: item.h, border: `2px solid ${color}`, borderRadius: 4, background: `${color}15`, boxShadow: isSelected ? `0 0 0 2px ${color}` : "none" }}>
          {item.label && <span style={{ position: "absolute", top: -18, left: 4, fontSize: "0.65rem", color: textColor, fontWeight: 600 }}>{item.label}</span>}
        </div>
      );
    }

    if (item.type === "circle") {
      return (
        <div key={item.id} onClick={handleClick} style={{ ...baseStyle, left: item.x, top: item.y, width: item.w, height: item.h, border: `2px solid ${color}`, borderRadius: "50%", background: `${color}15`, boxShadow: isSelected ? `0 0 0 2px ${color}` : "none" }}>
          {item.label && <span style={{ position: "absolute", top: -18, left: 4, fontSize: "0.65rem", color: textColor, fontWeight: 600 }}>{item.label}</span>}
        </div>
      );
    }

    if (item.type === "path" && item.points?.length > 1) {
      const pts = item.points;
      const minX = Math.min(...pts.map(p => p.x)) - 4;
      const minY = Math.min(...pts.map(p => p.y)) - 4;
      const maxX = Math.max(...pts.map(p => p.x)) + 4;
      const maxY = Math.max(...pts.map(p => p.y)) + 4;
      const d = pts.map((p, i) => `${i === 0 ? "M" : "L"} ${p.x - minX} ${p.y - minY}`).join(" ");
      return (
        <svg key={item.id} onClick={handleClick} style={{ ...baseStyle, left: minX, top: minY, overflow: "visible", cursor: "pointer" }} width={maxX - minX} height={maxY - minY}>
          <path d={d} fill="none" stroke={color} strokeWidth={isSelected ? 3 : 2} strokeLinecap="round" strokeLinejoin="round" />
          {isSelected && <path d={d} fill="none" stroke={color} strokeWidth={5} strokeOpacity={0.3} strokeLinecap="round" />}
        </svg>
      );
    }
    return null;
  };

  // Preview shapes
  const renderPreview = () => {
    if (previewShape) {
      const color = authorMember(currentAuthor)?.color || "var(--brand-primary, #e07b39)";
      if (previewShape.type === "rect") {
        return <div style={{ position: "absolute", left: previewShape.x, top: previewShape.y, width: previewShape.w, height: previewShape.h, border: `2px dashed ${color}`, borderRadius: 4, background: `${color}10`, pointerEvents: "none" }} />;
      }
      if (previewShape.type === "circle") {
        return <div style={{ position: "absolute", left: previewShape.x, top: previewShape.y, width: previewShape.w, height: previewShape.h, border: `2px dashed ${color}`, borderRadius: "50%", background: `${color}10`, pointerEvents: "none" }} />;
      }
    }
    if (currentPath.length > 1) {
      const color = authorMember(currentAuthor)?.color || "var(--brand-primary, #e07b39)";
      const pts = currentPath;
      const minX = Math.min(...pts.map(p => p.x)) - 4;
      const minY = Math.min(...pts.map(p => p.y)) - 4;
      const maxX = Math.max(...pts.map(p => p.x)) + 4;
      const maxY = Math.max(...pts.map(p => p.y)) + 4;
      const d = pts.map((p, i) => `${i === 0 ? "M" : "L"} ${p.x - minX} ${p.y - minY}`).join(" ");
      return <svg style={{ position: "absolute", left: minX, top: minY, overflow: "visible", pointerEvents: "none" }} width={maxX - minX} height={maxY - minY}><path d={d} fill="none" stroke={color} strokeWidth={2} strokeLinecap="round" strokeDasharray="4 4" /></svg>;
    }
    return null;
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "calc(100vh - 50px)", overflow: "hidden" }}>
      {/* Toolbar */}
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12, flexWrap: "wrap", flexShrink: 0 }}>
        {toolBtns.map(t => (
          <button key={t.id} onClick={() => setTool(t.id)} title={t.label} style={{ padding: "6px 10px", background: tool === t.id ? "var(--brand-primary, #e07b39)" : "#333", border: "1px solid " + (tool === t.id ? "var(--brand-primary, #e07b39)" : "rgba(255,255,255,0.1)"), borderRadius: 8, color: tool === t.id ? "#161a26" : "#888", cursor: "pointer", fontFamily: "inherit", display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem", fontWeight: tool === t.id ? 700 : 400 }}>
            <IC icon={t.icon} size={14} />{t.label}
          </button>
        ))}

        <span style={{ width: 1, height: 24, background: "rgba(255,255,255,0.1)" }} />

        <button onClick={undoLast} title="Annuler dernier" style={{ padding: "6px 10px", background: "#333", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8, color: "#888", cursor: "pointer", fontFamily: "inherit", display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem" }}>
          <IC icon={Undo2} size={14} />Annuler
        </button>

        <button onClick={() => setShowImageInput(!showImageInput)} title="Image de fond" style={{ padding: "6px 10px", background: "#333", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8, color: "#888", cursor: "pointer", fontFamily: "inherit", display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem" }}>
          <IC icon={Image} size={14} />Fond
        </button>

        <button onClick={() => setCamera({ x: 0, y: 0, zoom: 1 })} title="Recentrer" style={{ padding: "6px 10px", background: "#333", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8, color: "#888", cursor: "pointer", fontFamily: "inherit", display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem" }}>
          <IC icon={Move} size={14} />Reset vue
        </button>

        <span style={{ flex: 1 }} />

        <span style={{ fontSize: "0.68rem", color: "#666", background: "#2a2f3e", padding: "4px 8px", borderRadius: 6 }}>{Math.round(camera.zoom * 100)}%</span>
      </div>

      {/* Image URL input */}
      {showImageInput && (
        <div style={{ display: "flex", gap: 8, marginBottom: 12, alignItems: "center", flexShrink: 0 }}>
          <input value={imageUrl} onChange={e => setImageUrl(e.target.value)} placeholder="URL image vue de haut (ex: https://...)" style={{ flex: 1, background: "#333", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8, padding: "8px 12px", color: "#e8eaed", fontSize: "0.82rem", fontFamily: "inherit" }} />
          <button onClick={setBgImage} style={{ padding: "8px 16px", background: "var(--brand-primary, #e07b39)", color: "#161a26", border: "none", borderRadius: 8, cursor: "pointer", fontWeight: 700, fontSize: "0.82rem", fontFamily: "inherit" }}>Appliquer</button>
          {bgImage && <button onClick={() => { updateProjectAnnotations(prev => ({ ...prev, imageUrl: "" })); setShowImageInput(false); }} style={{ padding: "8px 12px", background: "#d13b1a", color: "#fff", border: "none", borderRadius: 8, cursor: "pointer", fontSize: "0.78rem", fontFamily: "inherit" }}>Retirer</button>}
        </div>
      )}

      <div style={{ display: "flex", gap: 16, flex: 1, minHeight: 0 }}>
        {/* Canvas area */}
        <div ref={containerRef} style={{ flex: 1, position: "relative", background: "#1f2330", borderRadius: 12, border: "1px solid rgba(255,255,255,0.08)", overflow: "hidden", cursor: isPanning ? "grabbing" : (tool === "select" ? "default" : "crosshair") }}
          onMouseDown={handleMouseDown} onMouseMove={handleMouseMove} onMouseUp={handleMouseUp} onMouseLeave={() => { setIsPanning(false); if (drawing) handleMouseUp(); }}
          onWheel={handleWheel}>

          {/* Transformed layer */}
          <div ref={canvasRef} style={{ position: "absolute", left: 0, top: 0, transformOrigin: "0 0", transform: `translate(${camera.x}px, ${camera.y}px) scale(${camera.zoom})`, width: 2000, height: 2000 }}>

            {/* Background image */}
            {bgImage && <img src={bgImage} alt="Map" style={{ position: "absolute", top: 0, left: 0, maxWidth: 2000, maxHeight: 2000, pointerEvents: "none", opacity: 0.85 }} onError={e => { e.target.style.display = "none"; }} />}

            {/* Grid (when no image) */}
            {!bgImage && (
              <svg style={{ position: "absolute", top: 0, left: 0, width: 2000, height: 2000, pointerEvents: "none" }}>
                <defs><pattern id="grid" width="50" height="50" patternUnits="userSpaceOnUse"><path d="M 50 0 L 0 0 0 50" fill="none" stroke="rgba(255,255,255,0.04)" strokeWidth="1" /></pattern></defs>
                <rect width="2000" height="2000" fill="url(#grid)" />
              </svg>
            )}

            {/* Rendered annotations */}
            {items.map(renderItem)}
            {renderPreview()}
          </div>

          {/* Empty state */}
          {!bgImage && items.length === 0 && (
            <div style={{ position: "absolute", top: "50%", left: "50%", transform: "translate(-50%, -50%)", textAlign: "center", color: "#555", pointerEvents: "none" }}>
              <IC icon={MapPin} size={32} style={{ marginBottom: 8 }} />
              <div style={{ fontSize: "0.85rem", fontWeight: 600 }}>Aucune annotation</div>
              <div style={{ fontSize: "0.75rem", marginTop: 4 }}>Cliquez "Fond" pour charger une image de map,<br/>puis utilisez les outils pour annoter</div>
            </div>
          )}
        </div>

        {/* Side panel — selected item details + comments */}
        <div style={{ width: 280, flexShrink: 0, overflowY: "auto" }}>
          {selectedItem ? (
            <div style={{ background: "#2a2f3e", borderRadius: 12, border: "1px solid rgba(255,255,255,0.08)", padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
                <span style={{ fontSize: "0.82rem", fontWeight: 700, color: "#fff" }}>
                  {selectedItem.type === "pin" ? "Marqueur" : selectedItem.type === "rect" ? "Rectangle" : selectedItem.type === "circle" ? "Cercle" : "Dessin"}
                </span>
                <div style={{ display: "flex", gap: 4 }}>
                  <button onClick={() => deleteItem(selectedItem.id)} style={{ background: "transparent", border: "none", color: "#d13b1a", cursor: "pointer", padding: 4 }}><IC icon={Trash2} size={14} /></button>
                  <button onClick={() => setSelectedId(null)} style={{ background: "transparent", border: "none", color: "#888", cursor: "pointer", padding: 4 }}><IC icon={X} size={14} /></button>
                </div>
              </div>

              {/* Author badge */}
              {authorMember(selectedItem.author) && <Badge member={authorMember(selectedItem.author)} small />}
              <div style={{ fontSize: "0.65rem", color: "#666", marginTop: 4, marginBottom: 12 }}>{new Date(selectedItem.createdAt).toLocaleString("fr-FR")}</div>

              {/* Label edit */}
              <label style={{ fontSize: "0.68rem", fontWeight: 600, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em" }}>Label</label>
              <input value={selectedItem.label || ""} onChange={e => {
                updateProjectAnnotations(prev => ({ ...prev, items: (prev.items || []).map(i => i.id === selectedItem.id ? { ...i, label: e.target.value } : i) }));
              }} style={{ width: "100%", background: "#333", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 6, padding: "6px 10px", color: "#e8eaed", fontSize: "0.82rem", fontFamily: "inherit", marginTop: 4, marginBottom: 12, outline: "none", boxSizing: "border-box" }} />

              {/* Color pickers */}
              <div style={{ display: "flex", gap: 10, marginBottom: 12 }}>
                <div style={{ flex: 1 }}>
                  <label style={{ fontSize: "0.68rem", fontWeight: 600, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em", display: "block", marginBottom: 4 }}>Couleur</label>
                  <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <input type="color" value={selectedItem.color || authorMember(selectedItem.author)?.color || "var(--brand-primary, #e07b39)"} onChange={e => {
                      updateProjectAnnotations(prev => ({ ...prev, items: (prev.items || []).map(i => i.id === selectedItem.id ? { ...i, color: e.target.value } : i) }));
                    }} style={{ width: 32, height: 28, padding: 2, border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, background: "#333", cursor: "pointer" }} />
                    <span style={{ fontSize: "0.72rem", color: "#666", fontFamily: "monospace" }}>{selectedItem.color || authorMember(selectedItem.author)?.color || "var(--brand-primary, #e07b39)"}</span>
                    {selectedItem.color && <button onClick={() => updateProjectAnnotations(prev => ({ ...prev, items: (prev.items || []).map(i => i.id === selectedItem.id ? { ...i, color: undefined } : i) }))} title="Réinitialiser" style={{ background: "none", border: "none", color: "#555", cursor: "pointer", padding: 0, fontSize: "0.7rem" }}>↺</button>}
                  </div>
                </div>
                <div style={{ flex: 1 }}>
                  <label style={{ fontSize: "0.68rem", fontWeight: 600, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em", display: "block", marginBottom: 4 }}>Texte</label>
                  <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <input type="color" value={selectedItem.textColor || "#e8eaed"} onChange={e => {
                      updateProjectAnnotations(prev => ({ ...prev, items: (prev.items || []).map(i => i.id === selectedItem.id ? { ...i, textColor: e.target.value } : i) }));
                    }} style={{ width: 32, height: 28, padding: 2, border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, background: "#333", cursor: "pointer" }} />
                    <span style={{ fontSize: "0.72rem", color: "#666", fontFamily: "monospace" }}>{selectedItem.textColor || "#e8eaed"}</span>
                    {selectedItem.textColor && <button onClick={() => updateProjectAnnotations(prev => ({ ...prev, items: (prev.items || []).map(i => i.id === selectedItem.id ? { ...i, textColor: undefined } : i) }))} title="Réinitialiser" style={{ background: "none", border: "none", color: "#555", cursor: "pointer", padding: 0, fontSize: "0.7rem" }}>↺</button>}
                  </div>
                </div>
              </div>

              {/* Comments */}
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6 }}>
                <label style={{ fontSize: "0.68rem", fontWeight: 600, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em" }}>Commentaires ({(selectedItem.comments || []).length})</label>
                {(selectedItem.comments || []).length > 1 && (
                  <button onClick={() => setCommentSort(s => s === "asc" ? "desc" : "asc")} title={commentSort === "asc" ? "Plus récent en premier" : "Plus ancien en premier"} style={{ background: "transparent", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 4, padding: "2px 6px", cursor: "pointer", color: "#888", fontSize: "0.6rem", display: "flex", alignItems: "center", gap: 3 }}>
                    <IC icon={commentSort === "asc" ? ArrowUpNarrowWide : ArrowDownNarrowWide} size={11} />
                    {commentSort === "asc" ? "Ancien" : "Récent"}
                  </button>
                )}
              </div>
              <div style={{ maxHeight: 260, overflowY: "auto", marginTop: 0 }}>
                {[...(selectedItem.comments || [])].sort((a, b) => commentSort === "asc" ? a.createdAt - b.createdAt : b.createdAt - a.createdAt).map(c => {
                  const cm = authorMember(c.author);
                  return (
                    <div key={c.id} style={{ background: "#333", borderRadius: 6, padding: "6px 8px", marginBottom: 4, fontSize: "0.75rem" }}>
                      <div style={{ display: "flex", alignItems: "center", gap: 4, marginBottom: 2 }}>
                        <span style={{ fontWeight: 700, color: cm?.color || "var(--brand-primary, #e07b39)", fontSize: "0.7rem" }}>{cm?.name || c.author}</span>
                        <span style={{ fontSize: "0.6rem", color: "#666" }}>{new Date(c.createdAt).toLocaleString("fr-FR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}</span>
                        <button onClick={() => deleteComment(selectedItem.id, c.id)} style={{ marginLeft: "auto", background: "transparent", border: "none", color: "#555", cursor: "pointer", padding: "0 2px", lineHeight: 1 }} title="Supprimer"><IC icon={X} size={11} /></button>
                      </div>
                      {c.text && <div style={{ color: "#e8eaed", marginBottom: (c.images || []).length ? 6 : 0 }}>{c.text}</div>}
                      {(c.images || []).length > 0 && (
                        <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
                          {c.images.map((img, i) => (
                            <img key={i} src={img} alt="ref" style={{ maxWidth: "100%", maxHeight: 120, borderRadius: 4, objectFit: "cover", border: "1px solid rgba(255,255,255,0.1)", cursor: "zoom-in" }}
                              onClick={() => setLightbox(img)}
                              onError={e => { e.target.style.display = "none"; }} />
                          ))}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
              {/* Comment input */}
              <div style={{ marginTop: 6 }}>
                <div style={{ display: "flex", gap: 4 }}>
                  <input value={commentText} onChange={e => setCommentText(e.target.value)} onKeyDown={e => e.key === "Enter" && !e.shiftKey && addComment(selectedItem.id)} placeholder="Ajouter un commentaire..." style={{ flex: 1, background: "#333", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 6, padding: "6px 8px", color: "#e8eaed", fontSize: "0.78rem", fontFamily: "inherit", outline: "none" }} />
                  <button onClick={() => setShowCommentImageInput(v => !v)} title="Ajouter une image" style={{ background: showCommentImageInput ? "var(--brand-primary, #e07b39)" : "#333", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 6, padding: "6px 8px", cursor: "pointer", color: showCommentImageInput ? "#161a26" : "#aaa" }}><IC icon={Image} size={14} /></button>
                  <button onClick={() => addComment(selectedItem.id)} style={{ background: "var(--brand-primary, #e07b39)", border: "none", borderRadius: 6, padding: "6px 8px", cursor: "pointer", color: "#161a26" }}><IC icon={SendHorizontal} size={14} /></button>
                </div>
                {showCommentImageInput && (
                  <input value={commentImageUrl} onChange={e => setCommentImageUrl(e.target.value)} placeholder="URL de l'image de référence…" style={{ width: "100%", marginTop: 4, background: "#333", border: "1px solid rgba(60, 173, 217,0.4)", borderRadius: 6, padding: "6px 8px", color: "#e8eaed", fontSize: "0.75rem", fontFamily: "inherit", outline: "none", boxSizing: "border-box" }} />
                )}
              </div>

              {/* Tâche liée */}
              <div style={{ marginTop: 14, paddingTop: 12, borderTop: "1px solid rgba(255,255,255,0.06)" }}>
                <label style={{ fontSize: "0.68rem", fontWeight: 600, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em" }}>Tâche liée</label>
                {(() => {
                  const linkedTask = tasks?.find(t => t.id === selectedItem.taskId);
                  if (linkedTask) {
                    return (
                      <div style={{ marginTop: 6, background: "#333", borderRadius: 6, padding: "6px 10px", display: "flex", alignItems: "center", gap: 6 }}>
                        <span style={{ width: 8, height: 8, borderRadius: "50%", background: STATUS_CONFIG[linkedTask.status]?.color, flexShrink: 0 }} />
                        <span style={{ fontSize: "0.78rem", color: "#e8eaed", flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{linkedTask.text}</span>
                        <button onClick={() => onEditTask(linkedTask)} title="Ouvrir la tâche" style={{ background: "transparent", border: "none", color: "var(--brand-primary, #e07b39)", cursor: "pointer", padding: 2 }}><IC icon={ExternalLink} size={13} /></button>
                        <button onClick={() => linkTask(selectedItem.id, null)} title="Délier" style={{ background: "transparent", border: "none", color: "#666", cursor: "pointer", padding: 2 }}><IC icon={X} size={13} /></button>
                      </div>
                    );
                  }
                  return (
                    <div style={{ marginTop: 6, display: "flex", flexDirection: "column", gap: 6 }}>
                      <select onChange={e => e.target.value && linkTask(selectedItem.id, e.target.value)} defaultValue="" style={{ width: "100%", background: "#333", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 6, padding: "6px 8px", color: "#888", fontSize: "0.75rem", fontFamily: "inherit", outline: "none" }}>
                        <option value="">— Lier une tâche existante —</option>
                        {(tasks || []).filter(t => t.projectId === selectedProject).map(t => (
                          <option key={t.id} value={t.id}>{t.text.substring(0, 50)}</option>
                        ))}
                      </select>
                      <button onClick={() => createTaskFromPoint(selectedItem)} style={{ background: "transparent", border: "1px dashed rgba(60, 173, 217,0.4)", borderRadius: 6, padding: "6px 10px", color: "var(--brand-primary, #e07b39)", fontSize: "0.75rem", cursor: "pointer", fontFamily: "inherit", textAlign: "left" }}>
                        <IC icon={Plus} size={13} style={{ marginRight: 6 }} />Créer une tâche depuis ce point
                      </button>
                    </div>
                  );
                })()}
              </div>
            </div>
          ) : (
            <div style={{ background: "#2a2f3e", borderRadius: 12, border: "1px solid rgba(255,255,255,0.08)", padding: 16 }}>
              <div style={{ fontSize: "0.82rem", fontWeight: 700, color: "#fff", marginBottom: 8 }}>Annotations</div>
              <div style={{ fontSize: "0.75rem", color: "#888", marginBottom: 12 }}>{items.length} annotation{items.length !== 1 ? "s" : ""} sur cette map</div>

              {/* List of annotations */}
              <div style={{ maxHeight: 400, overflowY: "auto" }}>
                {items.map(item => {
                  const am = authorMember(item.author);
                  return (
                    <div key={item.id} onClick={() => setSelectedId(item.id)} style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 8px", borderRadius: 6, cursor: "pointer", marginBottom: 2, background: "transparent", transition: "background 0.1s" }}
                      onMouseEnter={e => e.currentTarget.style.background = "#333"}
                      onMouseLeave={e => e.currentTarget.style.background = "transparent"}>
                      <IC icon={item.type === "pin" ? MapPin : item.type === "rect" ? RectangleHorizontal : item.type === "circle" ? Circle : Pencil} size={12} style={{ color: am?.color || "var(--brand-primary, #e07b39)", flexShrink: 0 }} />
                      <span style={{ fontSize: "0.75rem", color: "#e8eaed", flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{item.label || (item.type === "pin" ? "Marqueur" : item.type === "rect" ? "Rectangle" : item.type === "circle" ? "Cercle" : "Dessin")}</span>
                      <span style={{ fontSize: "0.6rem", color: am?.color || "#888" }}>{am?.name}</span>
                      {(item.comments || []).length > 0 && <span style={{ fontSize: "0.6rem", color: "#666" }}><IC icon={MessageCircle} size={10} /> {item.comments.length}</span>}
                    </div>
                  );
                })}
              </div>

              {items.length === 0 && (
                <div style={{ fontSize: "0.72rem", color: "#555", textAlign: "center", padding: 20 }}>
                  Sélectionnez un outil et cliquez/glissez sur le canvas pour annoter
                </div>
              )}
            </div>
          )}
        </div>
      </div>
      {lightbox && <ImageLightbox url={lightbox} onClose={() => setLightbox(null)} />}
    </div>
  );
}
// ============================================================
// ASSET CATALOGUE VIEW — Purchased assets library
// ============================================================
function AssetCatalogueView({ assets: assetsProp, setAssets: setAssetsProp, currentUser }) {
  const isPrivileged = currentUser?.role === 'owner' || currentUser?.role === 'admin';

  const [assetsLocal, setAssetsLocal] = useState([]);
  const [loading, setLoading]     = useState(!assetsProp);
  const assets    = assetsProp    ?? assetsLocal;
  const setAssets = setAssetsProp ?? setAssetsLocal;

  // Demandes de téléchargement
  const [myRequests,   setMyRequests]   = useState([]);
  const [allRequests,  setAllRequests]  = useState([]);
  const [reqSending,   setReqSending]   = useState({});
  const [showReqPanel, setShowReqPanel] = useState(false);

  const loadMyRequests = useCallback(async () => {
    if (isPrivileged) return;
    try { setMyRequests(await api.ncGetMyRequests()); } catch { /* silencieux */ }
  }, [isPrivileged]);

  const loadAllRequests = useCallback(async () => {
    if (!isPrivileged) return;
    try { setAllRequests(await api.ncGetRequests()); } catch { /* silencieux */ }
  }, [isPrivileged]);

  useEffect(() => { loadMyRequests(); }, [loadMyRequests]);
  useEffect(() => { loadAllRequests(); }, [loadAllRequests]);

  const myRequestMap = useMemo(() => {
    const m = {};
    for (const r of myRequests) m[r.file_path] = r;
    return m;
  }, [myRequests]);

  const pendingCount = allRequests.filter(r => r.status === 'pending').length;

  async function requestDownload(asset) {
    const key = `cat:${asset.id}`;
    if (reqSending[key]) return;
    setReqSending(p => ({ ...p, [key]: true }));
    try {
      await api.ncCreateRequest(key, asset.name);
      await loadMyRequests();
    } catch (e) {
      if (!e.message?.includes('409')) alert('Erreur : ' + e.message);
      await loadMyRequests();
    } finally {
      setReqSending(p => { const n = { ...p }; delete n[key]; return n; });
    }
  }

  async function reviewRequest(id, status) {
    try {
      await api.ncPatchRequest(id, status);
      await loadAllRequests();
    } catch (e) { alert('Erreur : ' + e.message); }
  }

  function DlButton({ asset, size = 10 }) {
    const key = `cat:${asset.id}`;
    const dlUrl = asset.download_url?.startsWith('/') ? api.ncDownload(asset.download_url) : asset.download_url;

    if (isPrivileged) {
      return (
        <a href={dlUrl} target="_blank" rel="noreferrer"
          style={{ display: "flex", alignItems: "center", gap: 3, padding: size > 9 ? "3px 9px" : "3px 8px", background: "#5865f2", color: "#fff", borderRadius: 6, fontSize: size > 9 ? "0.68rem" : "0.65rem", fontWeight: 700, textDecoration: "none", flexShrink: 0 }}>
          <IC icon={Download} size={size} />DL
        </a>
      );
    }
    const req = myRequestMap[key];
    if (req?.status === 'approved') {
      return (
        <a href={dlUrl} target="_blank" rel="noreferrer"
          style={{ display: "flex", alignItems: "center", gap: 3, padding: size > 9 ? "3px 9px" : "3px 8px", background: "#3e9041", color: "#fff", borderRadius: 6, fontSize: size > 9 ? "0.68rem" : "0.65rem", fontWeight: 700, textDecoration: "none", flexShrink: 0 }}>
          <IC icon={Download} size={size} />DL
        </a>
      );
    }
    if (req?.status === 'pending') {
      return (
        <span style={{ display: "flex", alignItems: "center", gap: 3, padding: "3px 8px", background: "rgba(250,204,21,0.1)", color: "#facc15", borderRadius: 6, fontSize: "0.63rem", fontWeight: 700, border: "1px solid rgba(250,204,21,0.2)", flexShrink: 0 }}>
          <IC icon={AlertCircle} size={size} />En attente
        </span>
      );
    }
    if (req?.status === 'denied') {
      return (
        <span style={{ display: "flex", alignItems: "center", gap: 3, padding: "3px 8px", background: "rgba(248,113,113,0.08)", color: "#f87171", borderRadius: 6, fontSize: "0.63rem", fontWeight: 700, border: "1px solid rgba(248,113,113,0.18)", flexShrink: 0 }}>
          <IC icon={XCircle} size={size} />Refusé
        </span>
      );
    }
    return (
      <button onClick={() => requestDownload(asset)} disabled={!!reqSending[key]}
        style={{ display: "flex", alignItems: "center", gap: 3, padding: size > 9 ? "3px 9px" : "3px 8px", background: "rgba(96,165,250,0.1)", color: "#60a5fa", borderRadius: 6, fontSize: size > 9 ? "0.68rem" : "0.65rem", fontWeight: 700, border: "1px solid rgba(96,165,250,0.22)", cursor: "pointer", fontFamily: "inherit", flexShrink: 0, opacity: reqSending[key] ? 0.6 : 1 }}>
        <IC icon={Lock} size={size} />{reqSending[key] ? '…' : 'Demander'}
      </button>
    );
  }
  const [showForm, setShowForm]   = useState(false);
  const [editId, setEditId]       = useState(null);
  const [search, setSearch]       = useState("");
  const [form, setForm]           = useState({ name: "", vendor: "", description: "", store_url: "", download_url: "", price: "", thumbnail: "" });
  const [saving, setSaving]       = useState(false);
  const [error, setError]         = useState("");
  const [viewMode, setViewMode]   = useState(() => localStorage.getItem('catalogue_viewMode') || 'gallery'); // gallery | list | vendor

  // ── Scanner Nextcloud ──
  const [showScanner, setShowScanner] = useState(false);
  const [scanPacks, setScanPacks]     = useState(() => {
    try { return JSON.parse(localStorage.getItem('catalogue_scanPacks') || '[]'); } catch { return []; }
  });
  const [scanning, setScanning]       = useState(false);
  const [scanError, setScanError]     = useState("");
  const [importMsg, setImportMsg]     = useState("");

  // Persiste scanPacks dans localStorage
  useEffect(() => {
    try { localStorage.setItem('catalogue_scanPacks', JSON.stringify(scanPacks)); } catch {}
  }, [scanPacks]);

  // Calcul dynamique : quel asset du catalogue correspond à ce pack ?
  // Lien via download_url (chemin Nextcloud exact) ou nom normalisé en fallback
  const catalogueByPath = useMemo(() => {
    const map = {};
    assets.forEach(a => {
      if (a.download_url) map[a.download_url] = a;
    });
    return map;
  }, [assets]);

  const catalogueByName = useMemo(() => {
    const map = {};
    assets.forEach(a => {
      if (a.name) map[a.name.toLowerCase()] = a;
    });
    return map;
  }, [assets]);

  const getLinkedAsset = (pack) => {
    // 1. Lien exact via download_url (après premier import)
    if (pack.path && catalogueByPath[pack.path]) return catalogueByPath[pack.path];
    // 2. Fallback par nom normalisé
    const n = (pack.name || '').toLowerCase();
    return catalogueByName[n] || null;
  };

  const doScan = async () => {
    setScanning(true); setScanError(""); setScanPacks([]);
    try { localStorage.removeItem('catalogue_scanPacks'); } catch {}
    try {
      const { packs } = await api.ncScanAssets();
      setScanPacks(packs);
    } catch (e) { setScanError(e.message); }
    finally { setScanning(false); }
  };

  useEffect(() => {
    if (assetsProp) return; // géré par HubPanel
    api.getAssets().then(setAssetsLocal).catch(() => {}).finally(() => setLoading(false));
  }, [assetsProp]);

  const openNew = () => {
    setForm({ name: "", vendor: "", description: "", store_url: "", download_url: "", price: "", thumbnail: "" });
    setEditId(null);
    setShowForm(true);
    setError("");
  };
  const openEdit = (a) => {
    setForm({ name: a.name, vendor: a.vendor, description: a.description, store_url: a.store_url, download_url: a.download_url, price: a.price, thumbnail: a.thumbnail });
    setEditId(a.id);
    setShowForm(true);
    setError("");
  };
  const closeForm = () => { setShowForm(false); setEditId(null); };

  const save = async () => {
    if (!form.name.trim()) { setError("Le nom est requis."); return; }
    setSaving(true); setError("");
    try {
      if (editId) {
        const updated = await api.updateAsset(editId, form);
        setAssets(prev => prev.map(a => a.id === editId ? updated : a));
      } else {
        const created = await api.createAsset(form);
        setAssets(prev => [created, ...prev]);
      }
      closeForm();
    } catch (e) { setError(e.message); }
    finally { setSaving(false); }
  };

  const del = async (id) => {
    if (!confirm("Supprimer cet asset ?")) return;
    await api.deleteAsset(id).catch(() => {});
    setAssets(prev => prev.filter(a => a.id !== id));
  };

  const q = search.toLowerCase();
  const filtered = assets.filter(a =>
    !q || a.name.toLowerCase().includes(q) || (a.vendor || "").toLowerCase().includes(q) || (a.description || "").toLowerCase().includes(q)
  );

  const inS = { width: "100%", background: "#1a1f2c", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8, padding: "8px 12px", color: "#e8eaed", fontSize: "0.82rem", fontFamily: "inherit", boxSizing: "border-box", outline: "none" };
  const lbS = { display: "block", fontSize: "0.7rem", fontWeight: 600, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: 4, marginTop: 12 };

  return (
    <div>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 20, flexWrap: "wrap" }}>
        <h2 style={{ fontWeight: 800, fontSize: "1.2rem", color: "#fff", margin: 0 }}>
          <IC icon={Database} size={18} style={{ marginRight: 8, color: "#5865f2" }} />Catalogue Assets
        </h2>
        <span style={{ fontSize: "0.75rem", color: "#666" }}>{assets.length} asset{assets.length !== 1 ? "s" : ""}</span>
        <span style={{ flex: 1 }} />
        <div style={{ position: "relative" }}>
          <IC icon={Search} size={13} style={{ position: "absolute", left: 10, top: "50%", transform: "translateY(-50%)", color: "#555", pointerEvents: "none" }} />
          <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Rechercher…" style={{ ...inS, width: 200, paddingLeft: 30 }} />
        </div>
        <button onClick={() => setShowScanner(v => !v)} style={{ padding: "7px 14px", background: showScanner ? "rgba(60, 173, 217,0.15)" : "#2a2f3e", color: showScanner ? "var(--brand-primary, #e07b39)" : "#aaa", border: `1px solid ${showScanner ? "rgba(60, 173, 217,0.5)" : "rgba(255,255,255,0.1)"}`, borderRadius: 8, cursor: "pointer", fontWeight: 600, fontSize: "0.82rem", fontFamily: "inherit", display: "flex", alignItems: "center", gap: 6 }}>
          <IC icon={FolderSearch} size={14} />Scanner Unreal
        </button>
        {/* View mode toggle */}
        <div style={{ display: 'flex', background: '#161a26', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 8, overflow: 'hidden' }}>
          {[['gallery', LayoutGrid, 'Galerie'], ['list', Rows3, 'Liste'], ['vendor', Building2, 'Studio']].map(([mode, Icon, label]) => (
            <button key={mode} onClick={() => { setViewMode(mode); localStorage.setItem('catalogue_viewMode', mode); }}
              title={label}
              style={{ padding: '6px 11px', background: viewMode === mode ? 'rgba(88,101,242,0.25)' : 'transparent', color: viewMode === mode ? '#7b8ef5' : '#555', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 4, fontSize: '0.72rem', fontFamily: 'inherit', borderRight: mode !== 'vendor' ? '1px solid rgba(255,255,255,0.06)' : 'none' }}>
              <IC icon={Icon} size={13} />{label}
            </button>
          ))}
        </div>
        <button onClick={openNew} style={{ padding: "7px 14px", background: "#5865f2", color: "#fff", border: "none", borderRadius: 8, cursor: "pointer", fontWeight: 700, fontSize: "0.82rem", fontFamily: "inherit", display: "flex", alignItems: "center", gap: 6 }}>
          <IC icon={Plus} size={14} />Asset
        </button>
      </div>

      {/* Panneau de validation des demandes de téléchargement (admin/owner) */}
      {isPrivileged && (
        <div style={{ marginBottom: 16 }}>
          <button
            onClick={() => { setShowReqPanel(p => !p); if (!showReqPanel) loadAllRequests(); }}
            style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 14px", background: pendingCount > 0 ? "rgba(250,204,21,0.08)" : "rgba(255,255,255,0.04)", border: `1px solid ${pendingCount > 0 ? "rgba(250,204,21,0.3)" : "rgba(255,255,255,0.1)"}`, borderRadius: 8, cursor: "pointer", color: pendingCount > 0 ? "#facc15" : "#888", fontFamily: "inherit", fontSize: "0.82rem", fontWeight: 600, width: "100%", textAlign: "left" }}>
            <IC icon={Inbox} size={14} />
            Demandes de téléchargement
            {pendingCount > 0 && (
              <span style={{ background: "#facc15", color: "#161a26", borderRadius: 99, padding: "1px 7px", fontSize: "0.7rem", fontWeight: 800 }}>{pendingCount}</span>
            )}
            <IC icon={showReqPanel ? ChevronDown : ChevronRight} size={12} style={{ marginLeft: "auto" }} />
          </button>
          {showReqPanel && (
            <div style={{ marginTop: 6, background: "#1a1f2c", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 10, overflow: "hidden" }}>
              {allRequests.length === 0 ? (
                <div style={{ padding: "20px", textAlign: "center", color: "#555", fontSize: "0.82rem" }}>Aucune demande.</div>
              ) : allRequests.map((r, idx) => (
                <div key={r.id} style={{ display: "flex", alignItems: "center", gap: 10, padding: "10px 14px", borderBottom: idx < allRequests.length - 1 ? "1px solid rgba(255,255,255,0.05)" : "none", flexWrap: "wrap" }}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: "0.82rem", color: "#e8eaed", fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{r.file_name}</div>
                    <div style={{ fontSize: "0.7rem", color: "#666", marginTop: 2 }}>
                      par <span style={{ color: "#a1a1aa" }}>{r.requester_name || r.requester_id}</span>
                      {" · "}{new Date(r.created_at).toLocaleDateString("fr-FR", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                    </div>
                  </div>
                  {r.status === 'pending' ? (
                    <div style={{ display: "flex", gap: 6, flexShrink: 0 }}>
                      <button onClick={() => reviewRequest(r.id, 'approved')} style={{ display: "flex", alignItems: "center", gap: 5, padding: "5px 12px", background: "rgba(74,222,128,0.12)", color: "#4ade80", border: "1px solid rgba(74,222,128,0.3)", borderRadius: 7, cursor: "pointer", fontSize: "0.78rem", fontWeight: 700, fontFamily: "inherit" }}>
                        <IC icon={CheckCircle2} size={12} />Approuver
                      </button>
                      <button onClick={() => reviewRequest(r.id, 'denied')} style={{ display: "flex", alignItems: "center", gap: 5, padding: "5px 10px", background: "rgba(248,113,113,0.1)", color: "#f87171", border: "1px solid rgba(248,113,113,0.25)", borderRadius: 7, cursor: "pointer", fontSize: "0.78rem", fontWeight: 700, fontFamily: "inherit" }}>
                        <IC icon={XCircle} size={12} />Refuser
                      </button>
                    </div>
                  ) : (
                    <span style={{ padding: "4px 10px", borderRadius: 7, fontSize: "0.72rem", fontWeight: 700, background: r.status === 'approved' ? "rgba(74,222,128,0.12)" : "rgba(248,113,113,0.1)", color: r.status === 'approved' ? "#4ade80" : "#f87171", flexShrink: 0 }}>
                      {r.status === 'approved' ? 'Approuvé' : 'Refusé'}
                      {r.reviewed_by && <span style={{ fontWeight: 400, opacity: 0.7 }}> · {r.reviewed_by}</span>}
                    </span>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Scanner Nextcloud */}
      {showScanner && (
        <div style={{ background: "#2a2f3e", border: "1px solid rgba(60, 173, 217,0.3)", borderRadius: 12, padding: 20, marginBottom: 24 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14 }}>
            <div style={{ fontWeight: 700, fontSize: "0.92rem", color: "var(--brand-primary, #e07b39)", display: "flex", alignItems: "center", gap: 8 }}>
              <IC icon={FolderSearch} size={16} />Scanner Nextcloud <span style={{ fontFamily: "monospace", fontSize: "0.78rem", color: "#666" }}>/unreal_asset</span>
            </div>
            <button onClick={doScan} disabled={scanning} style={{ marginLeft: "auto", padding: "7px 16px", background: "var(--brand-primary, #e07b39)", color: "#161a26", border: "none", borderRadius: 8, cursor: "pointer", fontWeight: 700, fontSize: "0.82rem", fontFamily: "inherit", display: "flex", alignItems: "center", gap: 6, opacity: scanning ? 0.6 : 1 }}>
              {scanning ? <IC icon={Loader2} size={14} style={{ animation: "adm-spin 0.7s linear infinite" }} /> : <IC icon={FolderSearch} size={14} />}
              {scanning ? "Scan en cours…" : "Scanner"}
            </button>
          </div>
          {scanError && <div style={{ color: "#e74c3c", fontSize: "0.78rem", marginBottom: 10 }}>{scanError}</div>}

          {scanPacks.length > 0 && (
            <>
              {/* Barre d'actions */}
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12, flexWrap: "wrap" }}>
                <span style={{ fontSize: "0.78rem", color: "#888" }}>
                  <strong style={{ color: "#e8eaed" }}>{scanPacks.length}</strong> pack{scanPacks.length > 1 ? "s" : ""} trouvé{scanPacks.length > 1 ? "s" : ""}
                </span>
{(() => { const cnt = scanPacks.filter(p => !!getLinkedAsset(p)).length; return cnt > 0 ? (
                  <span style={{ fontSize: "0.72rem", color: "#3e9041", background: "rgba(62,144,65,0.1)", padding: "2px 8px", borderRadius: 8, border: "1px solid rgba(62,144,65,0.25)" }}>
                    ✓ {cnt} déjà dans le catalogue
                  </span>
                ) : null; })()}
                {importMsg && <span style={{ fontSize: "0.75rem", color: importMsg.startsWith("✓") ? "#3e9041" : "#e74c3c" }}>{importMsg}</span>}
              </div>

              {/* Liste des packs groupés par vendor */}
              <div style={{ display: "flex", flexDirection: "column", gap: 8, maxHeight: 560, overflowY: "auto" }}>
                {(() => {
                  // Grouper par vendor
                  const groups = {};
                  scanPacks.forEach(p => {
                    const v = p.vendor || "— Sans vendor —";
                    (groups[v] = groups[v] || []).push(p);
                  });
                  return Object.entries(groups).map(([vendor, packs]) => (
                    <div key={vendor}>
                      <div style={{ fontSize: "0.7rem", fontWeight: 700, color: "#666", textTransform: "uppercase", letterSpacing: "0.05em", padding: "4px 0 6px", display: "flex", alignItems: "center", gap: 6 }}>
                        <IC icon={FolderOpen} size={11} />{vendor}
                        <span style={{ fontWeight: 400, color: "#444" }}>({packs.length})</span>
                      </div>
                      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                        {packs.map(pack => {
                          const linkedAsset = getLinkedAsset(pack);
                          const alreadyInCatalogue = !!linkedAsset;
                          return (
                            <div key={pack.path} style={{ background: "#1f2330", borderRadius: 8, border: `1px solid ${alreadyInCatalogue ? "rgba(62,144,65,0.3)" : "rgba(255,255,255,0.06)"}`, padding: "8px 12px" }}>
                              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                                <div style={{ flex: 1, minWidth: 0 }}>
                                  <span style={{ fontFamily: "monospace", fontSize: "0.8rem", color: alreadyInCatalogue ? "#3e9041" : "#e8eaed", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", display: "block" }}>
                                    {pack.name}<span style={{ color: "#444" }}>.zip</span>
                                  </span>
                                  {pack.size > 0 && <span style={{ fontSize: "0.6rem", color: "#555" }}>{(pack.size / 1024 / 1024).toFixed(0)} Mo</span>}
                                  {alreadyInCatalogue && linkedAsset.store_url && (
                                    <a href={linkedAsset.store_url} target="_blank" rel="noreferrer" style={{ fontSize: '0.6rem', color: '#5865f2', textDecoration: 'none', display: 'block', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{linkedAsset.name}</a>
                                  )}
                                </div>
                                {alreadyInCatalogue && (
                                  <span style={{ fontSize: "0.65rem", color: "#3e9041", background: "rgba(62,144,65,0.1)", padding: "2px 7px", borderRadius: 10, border: "1px solid rgba(62,144,65,0.3)", flexShrink: 0 }}>✓ catalogue</span>
                                )}
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  ));
                })()}
              </div>
            </>
          )}
        </div>
      )}

      {/* Form */}
      {showForm && (
        <div style={{ background: "#2a2f3e", border: "1px solid rgba(88,101,242,0.35)", borderRadius: 12, padding: 20, marginBottom: 24 }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem", color: "#e8eaed", marginBottom: 12 }}>{editId ? "Modifier l'asset" : "Nouvel asset"}</div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <div>
              <label style={lbS}>Nom *</label>
              <input value={form.name} onChange={e => setForm(p => ({ ...p, name: e.target.value }))} style={inS} placeholder="ex: Forest Pack Vol.2" />
            </div>
            <div>
              <label style={lbS}>Vendeur</label>
              <input value={form.vendor} onChange={e => setForm(p => ({ ...p, vendor: e.target.value }))} style={inS} placeholder="ex: KitBash3D" />
            </div>
            <div style={{ gridColumn: "1 / -1" }}>
              <label style={lbS}>Description</label>
              <textarea value={form.description} onChange={e => setForm(p => ({ ...p, description: e.target.value }))} rows={2} style={{ ...inS, resize: "vertical" }} placeholder="Description courte de l'asset" />
            </div>
            <div>
              <label style={lbS}>Lien store (page produit)</label>
              <input value={form.store_url} onChange={e => setForm(p => ({ ...p, store_url: e.target.value }))} style={inS} placeholder="https://www.fab.com/listings/..." />
            </div>
            <div>
              <label style={lbS}>Lien téléchargement (Nextcloud)</label>
              <input value={form.download_url} onChange={e => setForm(p => ({ ...p, download_url: e.target.value }))} style={inS} placeholder="/unreal_asset/ForestPack.zip" />
              <div style={{ fontSize: "0.65rem", color: "#555", marginTop: 3 }}>Chemin Nextcloud relatif ou URL externe</div>
            </div>
            <div>
              <label style={lbS}>Prix</label>
              <input value={form.price} onChange={e => setForm(p => ({ ...p, price: e.target.value }))} style={inS} placeholder="ex: 29.99 €" />
            </div>
            <div>
              <label style={lbS}>Miniature (URL)</label>
              <input value={form.thumbnail} onChange={e => setForm(p => ({ ...p, thumbnail: e.target.value }))} style={inS} placeholder="https://..." />
            </div>
          </div>
          {error && <div style={{ color: "#e74c3c", fontSize: "0.78rem", marginTop: 8 }}>{error}</div>}
          <div style={{ display: "flex", gap: 8, marginTop: 16, justifyContent: "flex-end" }}>
            <button onClick={closeForm} style={{ padding: "7px 16px", background: "#333", color: "#888", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8, cursor: "pointer", fontFamily: "inherit" }}>Annuler</button>
            <button onClick={save} disabled={saving} style={{ padding: "7px 16px", background: "#5865f2", color: "#fff", border: "none", borderRadius: 8, cursor: "pointer", fontWeight: 700, fontFamily: "inherit", opacity: saving ? 0.6 : 1 }}>
              {saving ? "Enregistrement…" : (editId ? "Modifier" : "Ajouter")}
            </button>
          </div>
        </div>
      )}

      {/* List / Gallery / Vendor views */}
      {loading ? (
        <div style={{ textAlign: "center", padding: 40, color: "#555" }}>
          <IC icon={Loader2} size={24} style={{ animation: "adm-spin 0.7s linear infinite" }} />
        </div>
      ) : filtered.length === 0 ? (
        <div style={{ textAlign: "center", padding: "60px 20px", color: "#555" }}>
          <IC icon={Package} size={36} style={{ marginBottom: 12, opacity: 0.3 }} />
          <div style={{ fontWeight: 600, marginBottom: 4 }}>{search ? "Aucun résultat" : "Catalogue vide"}</div>
          <div style={{ fontSize: "0.78rem" }}>{search ? "Essayez un autre terme" : "Cliquez \"+Asset\" pour ajouter un pack"}</div>
        </div>
      ) : viewMode === 'gallery' ? (
        /* ── GALERIE ── */
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))", gap: 12 }}>
          {filtered.map(a => (
            <div key={a.id} style={{ background: "#2a2f3e", borderRadius: 10, border: "1px solid rgba(255,255,255,0.07)", overflow: "hidden", display: "flex", flexDirection: "column" }}>
              {a.thumbnail
                ? <img src={a.thumbnail} alt={a.name} style={{ width: "100%", height: 118, objectFit: "cover" }} onError={e => e.target.style.display = "none"} />
                : <div style={{ height: 60, background: "#1a1f2c", display: "flex", alignItems: "center", justifyContent: "center" }}><IC icon={Package} size={22} style={{ color: "#333" }} /></div>}
              <div style={{ padding: "9px 11px", flex: 1, display: "flex", flexDirection: "column", gap: 2 }}>
                <div style={{ fontWeight: 700, fontSize: "0.82rem", color: "#e8eaed", lineHeight: 1.3, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={a.name}>{a.name}</div>
                {a.vendor && <div style={{ fontSize: "0.67rem", color: "#666" }}>{a.vendor}</div>}
                {a.price && <div style={{ fontSize: "0.7rem", color: "#5865f2", fontWeight: 700 }}>{a.price}</div>}
                <div style={{ flex: 1 }} />
                <div style={{ display: "flex", gap: 4, marginTop: 7, alignItems: "center" }}>
                  {a.download_url && <DlButton asset={a} size={10} />}
                  {a.store_url && (
                    <a href={a.store_url} target="_blank" rel="noreferrer"
                      style={{ display: "flex", alignItems: "center", gap: 3, padding: "3px 8px", background: "transparent", color: "#555", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 6, fontSize: "0.68rem", textDecoration: "none" }}>
                      <IC icon={ExternalLink} size={9} />
                    </a>
                  )}
                  <span style={{ flex: 1 }} />
                  <button onClick={() => openEdit(a)} style={{ padding: "3px 8px", background: "transparent", color: "#555", border: "1px solid rgba(255,255,255,0.07)", borderRadius: 5, cursor: "pointer", fontFamily: "inherit" }}><IC icon={Pen} size={10} /></button>
                  <button onClick={() => del(a.id)} style={{ padding: "3px 8px", background: "transparent", color: "#e74c3c44", border: "1px solid rgba(231,76,60,0.15)", borderRadius: 5, cursor: "pointer", fontFamily: "inherit" }}><IC icon={Trash2} size={10} /></button>
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : viewMode === 'list' ? (
        /* ── LISTE ── */
        <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <div style={{ display: "grid", gridTemplateColumns: "56px 1fr 140px 90px 76px 66px", gap: 8, padding: "4px 10px 6px", fontSize: "0.6rem", fontWeight: 700, color: "#444", textTransform: "uppercase", letterSpacing: "0.05em", borderBottom: "1px solid rgba(255,255,255,0.05)" }}>
            <span/><span>Nom</span><span>Studio</span><span>Prix</span><span style={{ textAlign: 'center' }}>Liens</span><span style={{ textAlign: 'right' }}>Actions</span>
          </div>
          {filtered.map(a => (
            <div key={a.id} style={{ display: "grid", gridTemplateColumns: "56px 1fr 140px 90px 76px 66px", gap: 8, alignItems: "center", padding: "5px 10px", background: "#1a1f2c", borderRadius: 6, border: "1px solid rgba(255,255,255,0.04)" }}
              onMouseEnter={e => e.currentTarget.style.borderColor = 'rgba(255,255,255,0.09)'}
              onMouseLeave={e => e.currentTarget.style.borderColor = 'rgba(255,255,255,0.04)'}>
              {a.thumbnail
                ? <img src={a.thumbnail} alt="" style={{ width: 54, height: 36, objectFit: "cover", borderRadius: 4 }} onError={e => e.target.style.display = "none"} />
                : <div style={{ width: 54, height: 36, background: "#2a2f3e", borderRadius: 4, display: "flex", alignItems: "center", justifyContent: "center" }}><IC icon={Package} size={13} style={{ color: "#333" }} /></div>}
              <div style={{ minWidth: 0 }}>
                <div style={{ fontSize: "0.81rem", fontWeight: 600, color: "#e8eaed", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{a.name}</div>
                {a.description && <div style={{ fontSize: "0.61rem", color: "#555", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{a.description}</div>}
              </div>
              <div style={{ fontSize: "0.72rem", color: "#777", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{a.vendor || <span style={{ color: "#333" }}>—</span>}</div>
              <div style={{ fontSize: "0.73rem", color: a.price ? "#5865f2" : "#333", fontWeight: 700 }}>{a.price || "—"}</div>
              <div style={{ display: "flex", gap: 4, justifyContent: "center" }}>
                {a.download_url && <DlButton asset={a} size={9} />}
                {a.store_url && (
                  <a href={a.store_url} target="_blank" rel="noreferrer"
                    style={{ display: "flex", alignItems: "center", padding: "3px 7px", background: "transparent", color: "#444", border: "1px solid rgba(255,255,255,0.07)", borderRadius: 5, fontSize: "0.63rem", textDecoration: "none" }}>
                    <IC icon={ExternalLink} size={9} />
                  </a>
                )}
              </div>
              <div style={{ display: "flex", gap: 3, justifyContent: "flex-end" }}>
                <button onClick={() => openEdit(a)} style={{ padding: "3px 7px", background: "transparent", color: "#555", border: "1px solid rgba(255,255,255,0.07)", borderRadius: 5, cursor: "pointer", fontFamily: "inherit" }}><IC icon={Pen} size={10} /></button>
                <button onClick={() => del(a.id)} style={{ padding: "3px 7px", background: "transparent", color: "#e74c3c44", border: "1px solid rgba(231,76,60,0.13)", borderRadius: 5, cursor: "pointer", fontFamily: "inherit" }}><IC icon={Trash2} size={10} /></button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        /* ── PAR STUDIO ── */
        (() => {
          const grouped = {};
          filtered.forEach(a => {
            const v = a.vendor?.trim() || '— Sans studio —';
            (grouped[v] = grouped[v] || []).push(a);
          });
          const sorted = Object.entries(grouped).sort(([a], [b]) => a === '— Sans studio —' ? 1 : b === '— Sans studio —' ? -1 : a.localeCompare(b));
          return (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 22 }}>
              {sorted.map(([vendor, list]) => (
                <div key={vendor}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10, paddingBottom: 6, borderBottom: '1px solid rgba(255,255,255,0.06)' }}>
                    <IC icon={Building2} size={13} style={{ color: '#5865f2' }} />
                    <span style={{ fontSize: '0.82rem', fontWeight: 700, color: '#e8eaed' }}>{vendor}</span>
                    <span style={{ fontSize: '0.66rem', color: '#444', background: '#1a1f2c', padding: '1px 7px', borderRadius: 8 }}>{list.length}</span>
                  </div>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(190px, 1fr))', gap: 10 }}>
                    {list.map(a => (
                      <div key={a.id} style={{ background: '#1f2330', borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
                        {a.thumbnail
                          ? <img src={a.thumbnail} alt={a.name} style={{ width: '100%', height: 100, objectFit: 'cover' }} onError={e => e.target.style.display = 'none'} />
                          : <div style={{ height: 50, background: '#1a1f2c', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><IC icon={Package} size={18} style={{ color: '#2a2f3e' }} /></div>}
                        <div style={{ padding: '7px 9px', flex: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
                          <div style={{ fontSize: '0.78rem', fontWeight: 700, color: '#e8eaed', lineHeight: 1.3, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={a.name}>{a.name}</div>
                          {a.price && <div style={{ fontSize: '0.66rem', color: '#5865f2', fontWeight: 700 }}>{a.price}</div>}
                          <div style={{ flex: 1 }} />
                          <div style={{ display: 'flex', gap: 4, marginTop: 5, alignItems: 'center' }}>
                            {a.download_url && <DlButton asset={a} size={9} />}
                            {a.store_url && (
                              <a href={a.store_url} target="_blank" rel="noreferrer"
                                style={{ display: 'flex', alignItems: 'center', padding: '3px 7px', background: 'transparent', color: '#444', border: '1px solid rgba(255,255,255,0.07)', borderRadius: 5, fontSize: '0.65rem', textDecoration: 'none' }}>
                                <IC icon={ExternalLink} size={9} />
                              </a>
                            )}
                            <span style={{ flex: 1 }} />
                            <button onClick={() => openEdit(a)} style={{ padding: '3px 7px', background: 'transparent', color: '#444', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 5, cursor: 'pointer', fontFamily: 'inherit' }}><IC icon={Pen} size={9} /></button>
                            <button onClick={() => del(a.id)} style={{ padding: '3px 7px', background: 'transparent', color: '#e74c3c33', border: '1px solid rgba(231,76,60,0.1)', borderRadius: 5, cursor: 'pointer', fontFamily: 'inherit' }}><IC icon={Trash2} size={9} /></button>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          );
        })()
      )}
    </div>
  );
}

// ============================================================
// NEXTCLOUD VIEW — WebDAV file browser
// ============================================================
const NC_FOLDERS = [
  { key: "unreal_asset",  path: "/unreal_asset",  label: "Assets Unreal",  color: "#3e9041" },
  { key: "unreal_export", path: "/unreal_export",  label: "Exports Unreal", color: "#5865f2" },
];

function NextcloudView() {
  const [rootKey, setRootKey]   = useState("unreal_asset");
  const [path, setPath]         = useState(NC_FOLDERS[0].path);
  const [entries, setEntries]   = useState([]);
  const [loading, setLoading]   = useState(false);
  const [error, setError]       = useState("");
  const [history, setHistory]   = useState([NC_FOLDERS[0].path]);

  const currentRoot = NC_FOLDERS.find(f => f.key === rootKey) || NC_FOLDERS[0];

  const browse = useCallback(async (target) => {
    setLoading(true); setError("");
    try {
      const data = await api.ncBrowse(target);
      setEntries(data.entries || []);
      setPath(target);
    } catch (e) {
      setError(e.message || "Erreur de connexion à Nextcloud");
    } finally {
      setLoading(false);
    }
  }, []);

  const switchRoot = (folder) => {
    setRootKey(folder.key);
    setHistory([folder.path]);
    browse(folder.path);
  };

  useEffect(() => { browse(NC_FOLDERS[0].path); }, [browse]);

  const navigate = (entry) => {
    const newPath = entry.path;
    setHistory(prev => [...prev, newPath]);
    browse(newPath);
  };

  const goBack = () => {
    if (history.length <= 1) return;
    const newHistory = history.slice(0, -1);
    setHistory(newHistory);
    browse(newHistory[newHistory.length - 1]);
  };

  const breadcrumbs = path.replace(currentRoot.path, "").split("/").filter(Boolean);

  const formatSize = (bytes) => {
    if (!bytes) return "";
    if (bytes < 1024) return `${bytes} o`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} Ko`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} Mo`;
    return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} Go`;
  };

  return (
    <div>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 16, flexWrap: "wrap" }}>
        <h2 style={{ fontWeight: 800, fontSize: "1.2rem", color: "#fff", margin: 0 }}>
          <IC icon={HardDrive} size={18} style={{ marginRight: 8, color: currentRoot.color }} />Nextcloud
        </h2>
        <span style={{ flex: 1 }} />
        <button onClick={() => browse(path)} style={{ padding: "7px 12px", background: "transparent", color: "#888", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8, cursor: "pointer", fontFamily: "inherit", display: "flex", alignItems: "center", gap: 5, fontSize: "0.8rem" }}>
          <IC icon={RefreshCw} size={13} />Rafraîchir
        </button>
      </div>

      {/* Root folder tabs */}
      <div style={{ display: "flex", gap: 6, marginBottom: 16 }}>
        {NC_FOLDERS.map(folder => (
          <button key={folder.key} onClick={() => switchRoot(folder)} style={{
            padding: "7px 16px", borderRadius: 8, border: `1px solid ${rootKey === folder.key ? folder.color + "88" : "rgba(255,255,255,0.1)"}`,
            background: rootKey === folder.key ? folder.color + "22" : "transparent",
            color: rootKey === folder.key ? folder.color : "#888",
            fontWeight: rootKey === folder.key ? 700 : 400,
            cursor: "pointer", fontFamily: "inherit", fontSize: "0.82rem", display: "flex", alignItems: "center", gap: 6,
          }}>
            <IC icon={FolderOpen} size={13} />{folder.label}
          </button>
        ))}
      </div>

      {/* Breadcrumb */}
      <div style={{ display: "flex", alignItems: "center", gap: 4, marginBottom: 16, fontSize: "0.78rem", color: "#666", flexWrap: "wrap" }}>
        <button onClick={() => { setHistory([currentRoot.path]); browse(currentRoot.path); }}
          style={{ background: "none", border: "none", color: "#888", cursor: "pointer", fontFamily: "inherit", fontSize: "0.78rem", padding: "2px 4px", borderRadius: 4 }}>
          <IC icon={HardDrive} size={12} style={{ marginRight: 3 }} />{currentRoot.key}
        </button>
        {breadcrumbs.map((seg, i) => {
          const targetPath = currentRoot.path + "/" + breadcrumbs.slice(0, i + 1).join("/");
          const isLast = i === breadcrumbs.length - 1;
          return (
            <span key={i} style={{ display: "flex", alignItems: "center", gap: 4 }}>
              <IC icon={ChevronRight} size={11} style={{ color: "#444" }} />
              {isLast ? (
                <span style={{ color: "#e8eaed" }}>{seg}</span>
              ) : (
                <button onClick={() => {
                  const newHist = [...history.slice(0, history.indexOf(currentRoot.path) + 1), targetPath];
                  setHistory(newHist); browse(targetPath);
                }} style={{ background: "none", border: "none", color: "#888", cursor: "pointer", fontFamily: "inherit", fontSize: "0.78rem", padding: "2px 4px", borderRadius: 4 }}>
                  {seg}
                </button>
              )}
            </span>
          );
        })}
      </div>

      {/* Back button */}
      {path !== currentRoot.path && (
        <button onClick={goBack} style={{ display: "flex", alignItems: "center", gap: 6, padding: "7px 12px", background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 8, cursor: "pointer", color: "#888", fontFamily: "inherit", fontSize: "0.8rem", marginBottom: 12 }}>
          ← Retour
        </button>
      )}

      {error && (
        <div style={{ background: "rgba(231,76,60,0.12)", border: "1px solid rgba(231,76,60,0.3)", borderRadius: 8, padding: "10px 14px", color: "#e74c3c", fontSize: "0.82rem", marginBottom: 16 }}>
          {error}
        </div>
      )}

      {loading ? (
        <div style={{ textAlign: "center", padding: 40, color: "#555" }}>
          <IC icon={Loader2} size={24} style={{ animation: "adm-spin 0.7s linear infinite" }} />
        </div>
      ) : entries.length === 0 ? (
        <div style={{ textAlign: "center", padding: "60px 20px", color: "#555" }}>
          <IC icon={FolderOpen} size={36} style={{ marginBottom: 12, opacity: 0.3 }} />
          <div>Dossier vide</div>
        </div>
      ) : (
        <div style={{ background: "#2a2f3e", borderRadius: 12, border: "1px solid rgba(255,255,255,0.07)", overflow: "hidden" }}>
          {entries.map((entry, idx) => (
            <div key={entry.path} style={{ display: "flex", alignItems: "center", gap: 10, padding: "10px 16px", borderBottom: idx < entries.length - 1 ? "1px solid rgba(255,255,255,0.05)" : "none", transition: "background 0.1s" }}
              onMouseEnter={e => e.currentTarget.style.background = "#333"}
              onMouseLeave={e => e.currentTarget.style.background = "transparent"}>

              <IC icon={entry.isDir ? FolderOpen : Package} size={16}
                style={{ color: entry.isDir ? "#ed9121" : "#888", flexShrink: 0 }} />

              <div style={{ flex: 1, minWidth: 0 }}>
                {entry.isDir ? (
                  <button onClick={() => navigate(entry)}
                    style={{ background: "none", border: "none", color: "#e8eaed", cursor: "pointer", fontFamily: "inherit", fontSize: "0.85rem", fontWeight: 600, padding: 0, textAlign: "left" }}>
                    {entry.name}/
                  </button>
                ) : (
                  <span style={{ color: "#e8eaed", fontSize: "0.85rem" }}>{entry.name}</span>
                )}
                {entry.modified && (
                  <div style={{ fontSize: "0.65rem", color: "#555", marginTop: 1 }}>
                    {new Date(entry.modified).toLocaleDateString("fr-FR", { day: "numeric", month: "short", year: "numeric" })}
                  </div>
                )}
              </div>

              {!entry.isDir && entry.size > 0 && (
                <span style={{ fontSize: "0.7rem", color: "#555", flexShrink: 0 }}>{formatSize(entry.size)}</span>
              )}

              {!entry.isDir && (
                <a href={api.ncDownload(entry.path)} target="_blank" rel="noreferrer"
                  style={{ display: "flex", alignItems: "center", gap: 5, padding: "5px 12px", background: "#3e9041", color: "#fff", borderRadius: 7, fontSize: "0.75rem", fontWeight: 700, textDecoration: "none", flexShrink: 0 }}>
                  <IC icon={Download} size={12} />DL
                </a>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ============================================================
// SEARCH OVERLAY — Global search across tasks, ideas, milestones
// ============================================================
export function SearchOverlay({ tasks, ideas, milestones, catalogueAssets, projects, members, onClose, onEditTask, onNavigate }) {
  const [query, setQuery] = useState("");
  const inputRef = useRef(null);

  useEffect(() => { inputRef.current?.focus(); }, []);

  const normalize = (s) => (s || "").toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "");

  const results = useMemo(() => {
    const q = normalize(query);
    if (q.length < 2) return { tasks: [], ideas: [], milestones: [], fab: [], catalogue: [] };

    const matchedTasks = tasks.filter(t =>
      normalize(t.text).includes(q) ||
      normalize(t.category).includes(q) ||
      normalize(t.notes).includes(q) ||
      (t.assignees || []).some(a => normalize(members.find(m => m.id === a)?.name).includes(q))
    ).slice(0, 15);

    const matchedIdeas = ideas.filter(i =>
      normalize(i.text).includes(q) ||
      (i.comments || []).some(c => normalize(c.text).includes(q))
    ).slice(0, 10);

    const matchedMs = milestones.filter(m =>
      normalize(m.name).includes(q)
    ).slice(0, 5);

    const matchedCatalogue = (catalogueAssets || []).filter(a =>
      normalize(a.name).includes(q) ||
      normalize(a.vendor).includes(q) ||
      normalize(a.description).includes(q)
    ).slice(0, 8);

    return { tasks: matchedTasks, ideas: matchedIdeas, milestones: matchedMs, catalogue: matchedCatalogue };
  }, [query, tasks, ideas, milestones, catalogueAssets, members]);

  const total = results.tasks.length + results.ideas.length + results.milestones.length + results.catalogue.length;

  const highlight = (text, max) => {
    if (!text) return "";
    const t = text.length > (max || 80) ? text.substring(0, max || 80) + "…" : text;
    const q = normalize(query);
    if (q.length < 2) return t;
    const idx = normalize(t).indexOf(q);
    if (idx === -1) return t;
    return <>{t.substring(0, idx)}<mark style={{ background: "var(--brand-primary, #e07b39)40", color: "var(--brand-primary, #e07b39)", borderRadius: 2, padding: "0 1px" }}>{t.substring(idx, idx + query.length)}</mark>{t.substring(idx + query.length)}</>;
  };

  const projectName = (id) => projects.find(p => p.id === id)?.name || id;

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.65)", zIndex: 2000, display: "flex", alignItems: "flex-start", justifyContent: "center", paddingTop: 80 }} onClick={onClose}>
      <div onClick={e => e.stopPropagation()} style={{ background: "#2a2f3e", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 16, width: "100%", maxWidth: 640, maxHeight: "70vh", display: "flex", flexDirection: "column", overflow: "hidden", boxShadow: "0 20px 60px rgba(0,0,0,0.5)" }}>

        {/* Search input */}
        <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "14px 18px", borderBottom: "1px solid rgba(255,255,255,0.08)" }}>
          <IC icon={Search} size={18} style={{ color: "#888", flexShrink: 0 }} />
          <input ref={inputRef} value={query} onChange={e => setQuery(e.target.value)} placeholder="Rechercher tâches, idées, milestones…" style={{ flex: 1, background: "transparent", border: "none", color: "#e8eaed", fontSize: "0.95rem", fontFamily: "inherit", outline: "none" }} />
          <kbd style={{ fontSize: "0.65rem", color: "#666", background: "#333", padding: "2px 6px", borderRadius: 4, border: "1px solid rgba(255,255,255,0.08)" }}>ESC</kbd>
        </div>

        {/* Results */}
        <div style={{ overflowY: "auto", padding: "8px 0" }}>
          {query.length < 2 && (
            <div style={{ padding: "30px 18px", textAlign: "center", color: "#555", fontSize: "0.82rem" }}>
              <IC icon={Search} size={24} style={{ marginBottom: 8, opacity: 0.4 }} />
              <div>Tapez au moins 2 caractères</div>
              <div style={{ fontSize: "0.7rem", marginTop: 4, color: "#444" }}>Recherche dans les tâches, idées et milestones</div>
            </div>
          )}

          {query.length >= 2 && total === 0 && (
            <div style={{ padding: "30px 18px", textAlign: "center", color: "#555", fontSize: "0.82rem" }}>
              Aucun résultat pour « {query} »
            </div>
          )}

          {/* Tasks */}
          {results.tasks.length > 0 && (
            <div>
              <div style={{ padding: "6px 18px", fontSize: "0.68rem", fontWeight: 700, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em" }}>Tâches ({results.tasks.length})</div>
              {results.tasks.map(t => (
                <div key={t.id} onClick={() => { onEditTask(t); onClose(); }} style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 18px", cursor: "pointer", transition: "background 0.1s" }}
                  onMouseEnter={e => e.currentTarget.style.background = "#333"}
                  onMouseLeave={e => e.currentTarget.style.background = "transparent"}>
                  <span style={{ width: 7, height: 7, borderRadius: "50%", background: STATUS_CONFIG[t.status]?.color, flexShrink: 0 }} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: "0.82rem", color: "#e8eaed", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{highlight(t.text, 70)}</div>
                    <div style={{ fontSize: "0.68rem", color: "#666", display: "flex", gap: 8, marginTop: 2 }}>
                      <span>{projectName(t.projectId)}</span>
                      {t.category && <span>· {t.category}</span>}
                      {(t.assignees || []).length > 0 && <span>· {t.assignees.map(a => members.find(m => m.id === a)?.name || a).join(", ")}</span>}
                    </div>
                  </div>
                  <StatusBadge status={t.status} />
                </div>
              ))}
            </div>
          )}

          {/* Ideas */}
          {results.ideas.length > 0 && (
            <div>
              <div style={{ padding: "6px 18px", fontSize: "0.68rem", fontWeight: 700, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em", marginTop: 4 }}>Idées ({results.ideas.length})</div>
              {results.ideas.map(i => (
                <div key={i.id} onClick={() => { onNavigate("whiteboard"); onClose(); }} style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 18px", cursor: "pointer", transition: "background 0.1s" }}
                  onMouseEnter={e => e.currentTarget.style.background = "#333"}
                  onMouseLeave={e => e.currentTarget.style.background = "transparent"}>
                  <IC icon={Lightbulb} size={14} style={{ color: "#ed9121", flexShrink: 0 }} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: "0.82rem", color: "#e8eaed", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{highlight(i.text, 70)}</div>
                    <div style={{ fontSize: "0.68rem", color: "#666" }}>{projectName(i.projectId)}</div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Milestones */}
          {results.milestones.length > 0 && (
            <div>
              <div style={{ padding: "6px 18px", fontSize: "0.68rem", fontWeight: 700, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em", marginTop: 4 }}>Milestones ({results.milestones.length})</div>
              {results.milestones.map(m => (
                <div key={m.id} onClick={() => { onNavigate("roadmap"); onClose(); }} style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 18px", cursor: "pointer", transition: "background 0.1s" }}
                  onMouseEnter={e => e.currentTarget.style.background = "#333"}
                  onMouseLeave={e => e.currentTarget.style.background = "transparent"}>
                  <IC icon={Route} size={14} style={{ color: m.color || "var(--brand-primary, #e07b39)", flexShrink: 0 }} />
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: "0.82rem", color: "#e8eaed" }}>{highlight(m.name, 60)}</div>
                    <div style={{ fontSize: "0.68rem", color: "#666" }}>{m.date}</div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Catalogue Assets */}
          {results.catalogue.length > 0 && (
            <div>
              <div style={{ padding: "6px 18px", fontSize: "0.68rem", fontWeight: 700, color: "#888", textTransform: "uppercase", letterSpacing: "0.04em", marginTop: 4 }}>Catalogue ({results.catalogue.length})</div>
              {results.catalogue.map(a => (
                <div key={a.id} onClick={() => { onNavigate("catalogue"); onClose(); }} style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 18px", cursor: "pointer", transition: "background 0.1s" }}
                  onMouseEnter={e => e.currentTarget.style.background = "#333"}
                  onMouseLeave={e => e.currentTarget.style.background = "transparent"}>
                  <IC icon={Database} size={14} style={{ color: "#5865f2", flexShrink: 0 }} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: "0.82rem", color: "#e8eaed", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{highlight(a.name, 60)}</div>
                    <div style={{ fontSize: "0.68rem", color: "#666", display: "flex", gap: 8 }}>
                      {a.vendor && <span>{a.vendor}</span>}
                      {a.price && <span style={{ color: "#5865f2" }}>{a.price}</span>}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Footer hint */}
        {query.length >= 2 && total > 0 && (
          <div style={{ padding: "8px 18px", borderTop: "1px solid rgba(255,255,255,0.06)", fontSize: "0.68rem", color: "#555", display: "flex", gap: 12 }}>
            <span>{total} résultat{total > 1 ? "s" : ""}</span>
            <span style={{ flex: 1 }} />
            <span>Cliquer pour ouvrir</span>
          </div>
        )}
      </div>
    </div>
  );
}

const DEFAULT_MILESTONES = [];

// --- STYLES ---
const labelStyle = { display: "block", fontSize: "0.72rem", fontWeight: 600, color: "#888888", marginBottom: 4, marginTop: 12, textTransform: "uppercase", letterSpacing: "0.04em" };
const inputStyle = { width: "100%", background: "#333333", border: "1px solid rgba(255,255,255,0.1)", borderRadius: 8, padding: "8px 12px", color: "#e8eaed", fontSize: "0.85rem", fontFamily: "inherit", marginBottom: 4, outline: "none" };
const btnStyle = { padding: "8px 18px", borderRadius: 8, cursor: "pointer", fontSize: "0.82rem", fontFamily: "inherit" };
// ============================================================
// MAIN APP
// ============================================================
export default function HubPanel({ view: externalView, onChangeView, currentUser, projectFilter = "all", openTaskId, onTaskOpened }) {
  const [loaded, setLoaded] = useState(false);
  const [tasks, setTasks] = useState([]);
  const [ideas, setIdeas] = useState([]);
  const [milestones, setMilestones] = useState([]);
  const [projects] = useState(DEFAULT_PROJECTS);
  const [members, setMembers] = useState([]);
  const [view, setViewInternal] = useState(externalView || "dashboard");
  const [taskSubView, setTaskSubView] = useState("myboard");
  const [personFilter, setPersonFilter] = useState("all");
  const [myBoardPerson, setMyBoardPerson] = useState(null);
  const [currentMemberId, setCurrentMemberId] = useState(null);
  const [editingTask, setEditingTask] = useState(null);
  const [showNewTask, setShowNewTask] = useState(false);
  const [newTaskCategory, setNewTaskCategory] = useState("");
  const [showImport, setShowImport] = useState(false);
  const [activityLog, setActivityLog] = useState([]);
  const [mapAnnotations, setMapAnnotations] = useState({});
  const [catalogueAssets, setCatalogueAssets] = useState([]);
  const [searchOpen, setSearchOpen] = useState(false);
  const isLoaded = useRef(false);    // garde pour ne pas sauvegarder pendant le chargement initial
  const isReloading = useRef(false); // garde pour ne pas sauvegarder pendant un reload SSE
  // Refs miroir des states pour pouvoir lire les valeurs courantes dans les callbacks useCallback([])
  const tasksRef         = useRef([]);
  const milestonesRef    = useRef([]);
  const mapAnnotationsRef= useRef({});

  // Auto-sélectionner le user connecté dans les filtres
  useEffect(() => {
    if (!currentUser) return;
    const id = currentUser.displayName?.toLowerCase().replace(/\s+/g, "_") || null;
    if (id) {
      setMyBoardPerson(id);
      setCurrentMemberId(id);
      // personFilter reste "all" par défaut — l'utilisateur peut filtrer manuellement
    }
  }, [currentUser]);

  // Ouvrir une tâche depuis l'extérieur (ex: recherche globale dans AdminApp)
  useEffect(() => {
    if (!openTaskId || !loaded) return;
    const task = tasksRef.current.find(t => t.id === openTaskId);
    if (task) {
      setViewInternal("tasks");
      setEditingTask(task);
      onTaskOpened?.();
    }
  }, [openTaskId, loaded]);

  // Sync view with external prop
  useEffect(() => {
    if (externalView == null) return;
    if (externalView === "myboard" || externalView === "board" || externalView === "list") {
      setTaskSubView(externalView);
      setViewInternal("tasks");
    } else {
      setViewInternal(externalView);
    }
  }, [externalView]);

  const setView = (v) => {
    // Aliases pour rétrocompatibilité : myboard/board/list → tasks + sous-vue
    if (v === "myboard" || v === "board" || v === "list") {
      setTaskSubView(v);
      setViewInternal("tasks");
      if (onChangeView) onChangeView("tasks");
    } else {
      setViewInternal(v);
      if (onChangeView) onChangeView(v);
    }
  };

  // Lock body scroll on map view to prevent double scrollbar
  useEffect(() => {
    if (view === "mapview") {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }
    return () => { document.body.style.overflow = ""; };
  }, [view]);

  // Ctrl+K / Cmd+K to open search
  useEffect(() => {
    const handler = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        setSearchOpen(prev => !prev);
      }
      if (e.key === "Escape") setSearchOpen(false);
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);

  const logActivity = useCallback((action, detail, author, targetType, targetId) => {
    const entry = {
      id: `log_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`,
      action,
      detail,
      author: author || "system",
      target_type: targetType || "",
      target_id: targetId || "",
      timestamp: Date.now(),
      created_at: new Date().toISOString(),
    };
    setActivityLog(prev => [entry, ...prev].slice(0, 200));
    // Persiste en DB (fire-and-forget)
    api.addActivity({ action, detail, author: author || "system", targetType, targetId }).catch(() => {});
  }, []);

  // Timestamp du dernier reload réussi — évite les rechargements trop rapprochés sur reconnexion
  const lastReloadAt = useRef(0);

  // Load
  const reload = useCallback(async (opts = {}) => {
    const now = Date.now();
    // Anti-bounce : ignore les reloads déclenchés par le socket moins de 2 s après le précédent
    // sauf si c'est le chargement initial (opts.initial)
    if (!opts.initial && now - lastReloadAt.current < 500) return;

    isReloading.current = true; // bloque la sauvegarde pendant le reload
    let data, activity, dbUsers;
    try {
      [data, activity, dbUsers] = await Promise.all([
        loadData(),
        api.getActivity(200).catch(() => []),
        api.getUsers().catch(() => []),
      ]);
      if (opts.initial) {
        api.getAssets().then(setCatalogueAssets).catch(() => {});
      }
    } catch {
      // Erreur réseau (ex: ERR_QUIC_PROTOCOL_ERROR) — on ne touche pas à l'état existant
      isReloading.current = false;
      return;
    }
    lastReloadAt.current = Date.now();

    // Convertir les users DB en membres Hub
    const hubMembers = (dbUsers || []).map((u, idx) => dbUserToHub(u, idx));
    setMembers(hubMembers);
    if (data) {
      setTasks(data.tasks || []);
      setIdeas(data.ideas || []);
      setMilestones(data.milestones || DEFAULT_MILESTONES);
      setMapAnnotations(data.mapAnnotations || {});
    } else {
      // DB vraiment vide (première utilisation) — initialise l'état sans écraser en DB
      setTasks([]);
      setIdeas([]);
      setMilestones([]);
    }
    setActivityLog(activity);
    setLoaded(true);
  }, []);

  useEffect(() => { reload({ initial: true }) }, [reload]);

  // Dispatcher socket — applique les deltas directement, évite le full reload pour tasks/ideas
  useAdminSocket([
    'task_created', 'task_updated', 'task_deleted',
    'tasks_bulk_created', 'tasks_bulk_updated',
    'idea_created', 'idea_updated', 'idea_deleted', 'ideas_bulk_created',
    'misc_updated', 'hub_activity', 'members_updated', 'hub_updated',
  ], (event, data) => {
    switch (event) {
      case 'task_created':       setTasks(prev => prev.some(t => t.id === data.task.id) ? prev : [...prev, data.task]); break
      case 'task_updated':       setTasks(prev => prev.map(t => t.id === data.id ? data.task : t)); break
      case 'task_deleted':       setTasks(prev => prev.filter(t => t.id !== data.id)); setMilestones(prev => prev.map(m => ({ ...m, taskIds: (m.taskIds || []).filter(tid => tid !== data.id) }))); break
      case 'tasks_bulk_created': setTasks(prev => { const ids = new Set(prev.map(t => t.id)); return [...prev, ...data.tasks.filter(t => !ids.has(t.id))] }); break
      case 'tasks_bulk_updated': setTasks(prev => prev.map(t => data.ids.includes(t.id) ? { ...t, ...data.changes, updatedAt: Date.now() } : t)); break
      case 'idea_created':       setIdeas(prev => prev.some(i => i.id === data.idea.id) ? prev : [...prev, data.idea]); break
      case 'idea_updated':       setIdeas(prev => prev.map(i => i.id === data.id ? data.idea : i)); break
      case 'idea_deleted':       setIdeas(prev => prev.filter(i => i.id !== data.id)); break
      case 'ideas_bulk_created': setIdeas(prev => { const ids = new Set(prev.map(i => i.id)); return [...prev, ...data.ideas.filter(i => !ids.has(i.id))] }); break
      case 'misc_updated':       reload(); break
      case 'hub_updated':        reload(); break
      case 'hub_activity':       api.getActivity(200).then(setActivityLog).catch(() => {}); break
      case 'members_updated':    api.getUsers().then(dbUsers => setMembers((dbUsers || []).map((u, idx) => dbUserToHub(u, idx)))).catch(() => {}); break
      default: break
    }
  });

  // Polling de secours — rafraîchit toutes les 30 s (filet de sécurité si un event socket est manqué)
  useEffect(() => {
    const id = setInterval(() => reload(), 30_000)
    return () => clearInterval(id)
  }, [reload])

  // Save on change — ne s'exécute JAMAIS lors du chargement initial ni pendant un reload SSE
  // Synchronise les refs miroir — toujours avant l'effet de sauvegarde
  useEffect(() => { tasksRef.current          = tasks          }, [tasks])
  useEffect(() => { milestonesRef.current     = milestones     }, [milestones])
  useEffect(() => { mapAnnotationsRef.current = mapAnnotations }, [mapAnnotations])

  // Sauvegarde du misc blob (milestones, mapAnnotations)
  // tasks et ideas sont désormais sauvegardés via des endpoints granulaires
  useEffect(() => {
    if (!isLoaded.current) return;
    if (isReloading.current) { isReloading.current = false; return; }
    api.saveMisc({ milestones, mapAnnotations }).catch(err => console.error('saveMisc failed:', err));
  }, [milestones, mapAnnotations]);

  // Active la sauvegarde après le chargement initial (s'exécute après l'effet ci-dessus)
  useEffect(() => {
    if (loaded) isLoaded.current = true;
  }, [loaded]);

  // Save task with single Discord notification
  const handleSaveTaskWithNotif = useCallback((formWithMs) => {
    // Extrait l'\u00e9ventuelle assignation milestone du payload (pass\u00e9e par TaskModal)
    const { _milestoneId, _previousMilestoneId, ...form } = formWithMs;
    const milestoneChanged = _milestoneId !== undefined && _milestoneId !== _previousMilestoneId;

    const existingTask = tasks.find(t => t.id === form.id);
    const isNew = !existingTask;
    const statusChanged = existingTask && existingTask.status !== form.status;
    const now = Date.now();

    // Helper : applique le changement de milestone de fa\u00e7on synchrone c\u00f4t\u00e9 state
    // ET c\u00f4t\u00e9 backend (saveMisc), en respectant le garde-fou archiv\u00e9.
    const applyMilestoneChange = (taskId) => {
      if (!milestoneChanged) return;
      const targetMs = _milestoneId ? milestones.find(m => m.id === _milestoneId) : null;
      if (targetMs?.archived) {
        console.warn('[milestone] tentative d\'ajout dans un milestone archiv\u00e9 bloqu\u00e9e:', _milestoneId);
        return;
      }
      const updated = milestones.map(m => {
        const filtered = (m.taskIds || []).filter(id => id !== taskId);
        if (m.id === _milestoneId) return { ...m, taskIds: [...filtered, taskId] };
        return { ...m, taskIds: filtered };
      });
      setMilestones(updated);
      // Sauvegarde imm\u00e9diate avec la liste fra\u00eeche pour \u00e9viter l'\u00e9crasement
      // par l'auto-save useEffect qui pourrait partir avec l'ancien state.
      api.saveMisc({ milestones: updated, mapAnnotations })
        .catch(err => console.error('saveMisc (milestone change) failed:', err));
    };

    if (isNew) {
      const newTask = { ...form, id: form.id || genId(), createdAt: now, updatedAt: now };
      setTasks(prev => [...prev, newTask]);
      api.createTask(newTask).catch(err => {
        console.error('createTask failed:', err);
        setTasks(prev => prev.filter(t => t.id !== newTask.id));
      });
      // Le rattachement milestone se fait juste apr\u00e8s, dans la m\u00eame boucle React
      // \u2192 un seul render, un seul saveMisc avec la valeur correcte.
      applyMilestoneChange(newTask.id);
      queueDiscordNotif("new_task", { task: newTask, members, projects });
      logActivity("create", `Nouvelle t\u00e2che: ${form.text.substring(0, 60)}`, currentMemberId, "task", newTask.id);
    } else {
      const updatedTask = { ...form, updatedAt: now };
      setTasks(prev => prev.map(t => t.id === form.id ? updatedTask : t));
      api.updateTask(form.id, form).catch(err => {
        console.error('updateTask failed:', err);
        setTasks(prev => prev.map(t => t.id === form.id ? existingTask : t));
      });
      if (statusChanged) {
        queueDiscordNotif("status_change", { task: form, oldStatus: existingTask.status, newStatus: form.status, members, projects });
        logActivity("status", `${form.text.substring(0, 50)} : ${STATUS_CONFIG[existingTask.status]?.label} \u2192 ${STATUS_CONFIG[form.status]?.label}`, currentMemberId, "task", form.id);
      } else {
        const changes = [];
        if (existingTask.text !== form.text)
          changes.push({ field: "Nom", from: existingTask.text, to: form.text });
        if ((existingTask.description || "") !== (form.description || ""))
          changes.push({ field: "Description", to: (form.description || "").substring(0, 200) || "(vidée)" });
        if ((existingTask.notes || "") !== (form.notes || ""))
          changes.push({ field: "Notes", to: (form.notes || "").substring(0, 200) || "(vidées)" });
        if (existingTask.priority !== form.priority)
          changes.push({ field: "Priorité", from: PRIO_CONFIG[existingTask.priority]?.label || "Non priorisée", to: PRIO_CONFIG[form.priority]?.label || "Non priorisée" });
        if ((existingTask.deadline || "") !== (form.deadline || ""))
          changes.push({ field: "Deadline", from: existingTask.deadline || "—", to: form.deadline || "—" });
        if ((existingTask.category || "") !== (form.category || ""))
          changes.push({ field: "Catégorie", from: existingTask.category || "—", to: form.category || "—" });
        const oldA = existingTask.assignees || [], newA = form.assignees || [];
        const memberName = (id) => members.find(m => m.id === id)?.name || id;
        const addedA = newA.filter(a => !oldA.includes(a));
        const removedA = oldA.filter(a => !newA.includes(a));
        if (addedA.length) changes.push({ field: "Assigné ajouté", to: addedA.map(memberName).join(", ") });
        if (removedA.length) changes.push({ field: "Assigné retiré", to: removedA.map(memberName).join(", ") });
        const oldSub = existingTask.subtasks || [], newSub = form.subtasks || [];
        const addedSub = newSub.filter(s => !oldSub.find(o => o.id === s.id));
        const doneSub = newSub.filter(s => { const o = oldSub.find(o => o.id === s.id); return o && !o.done && s.done; });
        const undoneSub = newSub.filter(s => { const o = oldSub.find(o => o.id === s.id); return o && o.done && !s.done; });
        const removedSub = oldSub.filter(s => !newSub.find(n => n.id === s.id));
        if (addedSub.length) changes.push({ field: "Sous-tâche ajoutée", to: addedSub.map(s => s.text).join(", ") });
        if (doneSub.length) changes.push({ field: "Sous-tâche complétée", to: doneSub.map(s => s.text).join(", ") });
        if (undoneSub.length) changes.push({ field: "Sous-tâche rouverte", to: undoneSub.map(s => s.text).join(", ") });
        if (removedSub.length) changes.push({ field: "Sous-tâche supprimée", to: removedSub.map(s => s.text).join(", ") });
        if (changes.length > 0)
          queueDiscordNotif("task_edited", { task: form, changes, members, projects });
        // Un log par champ modifié — plus de visibilité dans l'historique
        const taskLabel = form.text.substring(0, 50);
        for (const c of changes) {
          const detail = c.from !== undefined
            ? `${taskLabel} · ${c.field}: ${c.from} \u2192 ${c.to}`
            : `${taskLabel} · ${c.field}: ${c.to}`;
          logActivity("edit", detail, currentMemberId, "task", form.id);
        }
      }
      // Édition d'une tâche existante : applique aussi le changement de milestone
      // si l'utilisateur l'a modifié dans la pop-up.
      applyMilestoneChange(form.id);
    }
  }, [tasks, members, projects, logActivity, currentMemberId, milestones, mapAnnotations]);

  const handleDeleteTask = useCallback((id) => {
    const task = tasks.find(t => t.id === id);
    if (task) {
      queueDiscordNotif("task_deleted", { task, members, projects });
      logActivity("delete", `Supprim\u00e9: ${task.text.substring(0, 60)}`, currentMemberId, "task", id);
    }
    setTasks(prev => prev.filter(t => t.id !== id));
    // Retirer la tâche de tous les milestones pour éviter les références fantômes
    setMilestones(prev => prev.map(m => ({
      ...m,
      taskIds: (m.taskIds || []).filter(tid => tid !== id)
    })));
    api.deleteTask(id).catch(err => {
      console.error('deleteTask failed:', err);
      if (task) setTasks(prev => [...prev, task]);
    });
  }, [tasks, members, projects, logActivity, currentMemberId]);

  const handleUpdateStatus = useCallback((id, status) => {
    const task = tasks.find(t => t.id === id);
    if (task) {
      queueDiscordNotif("status_change", { task: { ...task, status }, oldStatus: task.status, newStatus: status, members, projects });
      logActivity("status", `${task.text.substring(0, 50)} : ${STATUS_CONFIG[task.status]?.label} \u2192 ${STATUS_CONFIG[status]?.label}`, currentMemberId, "task", id);
    }
    setTasks(prev => prev.map(t => t.id === id ? { ...t, status, updatedAt: Date.now() } : t));
    api.updateTask(id, { status }).catch(err => {
      console.error('updateStatus failed:', err);
      if (task) setTasks(prev => prev.map(t => t.id === id ? task : t));
    });
  }, [tasks, members, projects, logActivity, currentMemberId]);

  const handleAddIdea = useCallback((text, projectId, description) => {
    const idea = { id: `i_${Date.now()}`, text, description: description || '', projectId, createdAt: Date.now(), comments: [], votes: {} };
    queueDiscordNotif("new_idea", { idea, projects });
    logActivity("idea", `Nouvelle id\u00e9e: ${text.substring(0, 60)}`);
    setIdeas(prev => [...prev, idea]);
    api.createIdea(idea).catch(err => {
      console.error('createIdea failed:', err);
      setIdeas(prev => prev.filter(i => i.id !== idea.id));
    });
  }, [projects, logActivity]);

  const handleDeleteIdea = useCallback((id) => {
    const idea = ideas.find(i => i.id === id);
    if (idea) logActivity("delete", `Id\u00e9e supprim\u00e9e: ${idea.text.substring(0, 60)}`);
    setIdeas(prev => prev.filter(i => i.id !== id));
    api.deleteIdea(id).catch(err => {
      console.error('deleteIdea failed:', err);
      if (idea) setIdeas(prev => [...prev, idea]);
    });
  }, [ideas, logActivity]);

  const handleConvertIdea = useCallback((idea) => {
    const newTask = {
      id: genId(), projectId: idea.projectId, category: "Depuis id\u00e9es",
      text: idea.text, description: idea.description || '', assignees: [], status: "todo", priority: null,
      deadline: null, notes: "", createdAt: Date.now(), updatedAt: Date.now(),
    };
    queueDiscordNotif("idea_converted", { idea, projects });
    logActivity("convert", `Id\u00e9e \u2192 t\u00e2che: ${idea.text.substring(0, 60)}`);
    setTasks(prev => [...prev, newTask]);
    setIdeas(prev => prev.filter(i => i.id !== idea.id));
    setEditingTask(newTask);
    api.createTask(newTask).catch(err => {
      console.error('createTask (convert) failed:', err);
      setTasks(prev => prev.filter(t => t.id !== newTask.id));
    });
    api.deleteIdea(idea.id).catch(err => {
      console.error('deleteIdea (convert) failed:', err);
      setIdeas(prev => [...prev, idea]);
    });
  }, [projects, logActivity]);

  const handleImport = useCallback((newTasks, newIdeas) => {
    if (newTasks.length) setTasks(prev => [...prev, ...newTasks]);
    if (newIdeas.length) setIdeas(prev => [...prev, ...newIdeas]);
    if (newTasks.length) api.bulkCreateTasks({ tasks: newTasks }).catch(err => console.error('bulkCreateTasks failed:', err));
    if (newIdeas.length) api.bulkCreateIdeas({ ideas: newIdeas }).catch(err => console.error('bulkCreateIdeas failed:', err));
  }, []);

  const handleResetData = async () => {
    if (confirm("Réinitialiser toutes les données ? Cette action est irréversible.")) {
      setTasks(DEFAULT_TASKS);
      setIdeas(DEFAULT_IDEAS);
      setMilestones(DEFAULT_MILESTONES);
    }
  };

  if (!loaded) return <div style={{ background: "#161a26", color: "#e8eaed", minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center" }}>Chargement...</div>;

  const navItems = [
    { id: "dashboard", label: "Dashboard", icon: BarChart3 },
    { id: "tasks", label: "Tâches", icon: SquareCheck },
    { id: "roadmap", label: "Roadmap", icon: Route },
    { id: "whiteboard", label: "Idées", icon: Lightbulb },
    { id: "mapview", label: "Map", icon: MapPin },
    { id: "activity", label: "Activité", icon: Activity },
  ];

  return (
    <div style={{ background: "#161a26", color: "#e8eaed", ...(view === "mapview" ? { height: "calc(100vh - 50px)", overflow: "hidden" } : { minHeight: "100vh" }), fontFamily: "'Inter', system-ui, sans-serif" }}>
      <style>{`
        select, select option { background: #333333; color: #e8eaed; }
        select option:checked { background: var(--brand-primary, #e07b39); color: #161a26; }
        select option:hover { background: #3a3a3a; }
      `}</style>

      {/* Bouton flottant + Tâche */}
      <button
        onClick={() => setShowNewTask(true)}
        style={{ position: "fixed", bottom: 28, right: 28, zIndex: 300, display: "flex", alignItems: "center", gap: 8, background: "var(--brand-primary, #e07b39)", color: "#161a26", border: "none", borderRadius: 12, padding: "12px 20px", fontWeight: 800, fontSize: "0.85rem", cursor: "pointer", boxShadow: "0 4px 20px rgba(60, 173, 217,0.4)", fontFamily: "'Inter', system-ui, sans-serif" }}
      >
        <IC icon={Plus} size={16} /> Tâche
      </button>

      {/* CONTENT */}
      <div style={{ maxWidth: 1400, margin: "0 auto", padding: view === "mapview" ? "24px 24px 0" : "24px 24px 80px", height: view === "mapview" ? "calc(100vh - 50px)" : undefined, overflow: view === "mapview" ? "hidden" : undefined, display: view === "mapview" ? "flex" : undefined, flexDirection: view === "mapview" ? "column" : undefined }}>

        {(() => {
          const ft = projectFilter === "all" ? tasks : tasks.filter(t => t.projectId === projectFilter);
          const fi = projectFilter === "all" ? ideas : ideas.filter(i => i.projectId === projectFilter);
          const fp = projectFilter === "all" ? projects : projects.filter(p => p.id === projectFilter);
          const fProjects = fp.length ? fp : projects;

          // Barre d'onglets + filtres pour la vue Tâches unifiée
          const TasksToolbar = () => {
            const subViews = [
              { id: "myboard", label: "Mon board", icon: User },
              { id: "board",   label: "Board",     icon: Columns3 },
              { id: "list",    label: "Liste",      icon: List },
            ];
            const listFilterMembers = [...members.filter(m => m.id !== "equipe" && m.id !== "map")];
            if (currentMemberId && !listFilterMembers.find(m => m.id === currentMemberId)) {
              listFilterMembers.unshift({
                id: currentMemberId,
                name: currentUser?.member?.name || currentUser?.displayName || currentMemberId,
                color: MEMBER_COLORS[0],
              });
            }
            return (
              <div style={{ display: "flex", gap: 8, marginBottom: 20, alignItems: "center", flexWrap: "wrap" }}>
                {/* Onglets de sous-vue */}
                <div style={{ display: "flex", gap: 4, background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 10, padding: 4 }}>
                  {subViews.map(sv => (
                    <button key={sv.id} onClick={() => setTaskSubView(sv.id)} style={{
                      ...btnStyle, padding: "6px 14px", fontSize: "0.78rem",
                      background: taskSubView === sv.id ? "var(--brand-primary, #e07b39)" : "transparent",
                      color: taskSubView === sv.id ? "#161a26" : "#888",
                      border: "none", fontWeight: taskSubView === sv.id ? 700 : 400,
                      borderRadius: 7,
                    }}>
                      <IC icon={sv.icon} size={13} style={{ marginRight: 5 }} />{sv.label}
                    </button>
                  ))}
                </div>

                <span style={{ flex: 1 }} />

                {/* Filtre personne (Mon board) */}
                {taskSubView === "myboard" && (() => {
                  const m = members.find(mb => mb.id === myBoardPerson);
                  return m ? (
                    <span style={{ fontSize: "0.78rem", color: m.color, fontWeight: 700, background: m.color + "22", border: `1px solid ${m.color}55`, borderRadius: 8, padding: "5px 12px" }}>
                      <IC icon={User} size={12} style={{ marginRight: 5 }} />{m.name}
                    </span>
                  ) : null;
                })()}

                {/* Filtre personne (Liste) */}
                {taskSubView === "list" && (
                  <div style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}>
                    <button onClick={() => setPersonFilter("all")} style={{ ...btnStyle, padding: "5px 12px", fontSize: "0.75rem", background: personFilter === "all" ? "var(--brand-primary, #e07b39)" : "transparent", color: personFilter === "all" ? "#161a26" : "#999", border: "1px solid rgba(255,255,255,0.1)", fontWeight: personFilter === "all" ? 700 : 400 }}>Tous</button>
                    {listFilterMembers.map(m => (
                      <button key={m.id} onClick={() => setPersonFilter(m.id)} style={{ ...btnStyle, padding: "5px 12px", fontSize: "0.75rem", background: personFilter === m.id ? m.color : "transparent", color: personFilter === m.id ? "#161a26" : m.color, border: `1px solid ${m.color}55`, fontWeight: personFilter === m.id ? 700 : 400 }}>{m.name}</button>
                    ))}
                  </div>
                )}

                <button onClick={() => setShowImport(true)} style={{ ...btnStyle, background: "transparent", color: "#888", border: "1px solid rgba(255,255,255,0.1)", padding: "6px 12px", display: "flex", alignItems: "center", gap: 5, fontSize: "0.78rem" }} title="Importer un fichier .md"><IC icon={FileUp} size={13} />Import .md</button>
              </div>
            );
          };

          return (<>
        {view === "dashboard" && <DashboardView tasks={ft} projects={fProjects} members={members} projectFilter={projectFilter} />}
        {view === "tasks" && <>
          <TasksToolbar />
          {taskSubView === "myboard" && <MyBoardView tasks={ft} members={members} myId={myBoardPerson} onEditTask={setEditingTask} onUpdateStatus={handleUpdateStatus} />}
          {taskSubView === "board"   && <BoardView tasks={ft} members={members} projectFilter="all" onEditTask={setEditingTask} onUpdateStatus={handleUpdateStatus} />}
          {taskSubView === "list"    && <ListView tasks={ft} members={members} milestones={milestones} projectFilter="all" personFilter={personFilter} onEditTask={setEditingTask} onAddInCategory={cat => { setNewTaskCategory(cat); setShowNewTask(true); }} onBulkUpdate={(taskIds, updates) => {
            // Applique les changements optimistiquement
            setTasks(prev => prev.map(t => {
              if (!taskIds.includes(t.id)) return t;
              const patched = { ...t, updatedAt: Date.now() };
              if (updates.status) patched.status = updates.status;
              if (updates.priority !== undefined) patched.priority = updates.priority;
              if (updates.category) patched.category = updates.category;
              if (updates.assignees) patched.assignees = updates.assignees;
              if (updates.addAssignee && !(t.assignees || []).includes(updates.addAssignee)) {
                patched.assignees = [...(t.assignees || []), updates.addAssignee];
              }
              return patched;
            }));
            if (updates.milestone) {
              setMilestones(prev => prev.map(m => {
                let tids = (m.taskIds || []).filter(id => !taskIds.includes(id));
                if (updates.milestone !== "__clear__" && m.id === updates.milestone) {
                  tids = [...tids, ...taskIds];
                }
                return { ...m, taskIds: tids };
              }));
            }
            // Sauvegarde via API
            if (updates.addAssignee) {
              // addAssignee est per-task (assignees diffèrent), on appelle updateTask individuellement
              tasks.filter(t => taskIds.includes(t.id)).forEach(t => {
                const newAssignees = (t.assignees || []).includes(updates.addAssignee)
                  ? t.assignees
                  : [...(t.assignees || []), updates.addAssignee];
                api.updateTask(t.id, { assignees: newAssignees }).catch(console.error);
              });
            } else {
              const changes = {};
              if (updates.status)    changes.status    = updates.status;
              if (updates.priority !== undefined) changes.priority = updates.priority;
              if (updates.category)  changes.category  = updates.category;
              if (updates.assignees) changes.assignees = updates.assignees;
              if (Object.keys(changes).length) api.bulkUpdateTasks({ ids: taskIds, changes }).catch(console.error);
            }
            queueDiscordNotif("task_edited", { task: { text: `Modification group\u00e9e de ${taskIds.length} t\u00e2ches`, status: updates.status || "todo", assignees: [] }, members, projects });
            logActivity("bulk", `Modification group\u00e9e: ${taskIds.length} t\u00e2ches${updates.status ? " \u2192 " + (STATUS_CONFIG[updates.status]?.label || updates.status) : ""}`, currentMemberId);
            // Log par tâche pour qu'on retrouve la modif dans l'historique de chaque carte
            for (const tid of taskIds) {
              const summary = updates.status ? `Statut \u2192 ${STATUS_CONFIG[updates.status]?.label || updates.status}` :
                              updates.priority !== undefined ? `Priorit\u00e9 \u2192 ${updates.priority == null ? "Non prioris\u00e9e" : PRIO_CONFIG[updates.priority]?.label || updates.priority}` :
                              updates.category ? `Cat\u00e9gorie \u2192 ${updates.category}` :
                              updates.addAssignee ? `Assign\u00e9 ajout\u00e9: ${members.find(m => m.id === updates.addAssignee)?.name || updates.addAssignee}` :
                              updates.assignees && updates.assignees.length === 0 ? "Assign\u00e9s vid\u00e9s" :
                              "Modification group\u00e9e";
              logActivity("edit", summary, currentMemberId, "task", tid);
            }
          }} />}
        </>}
        {view === "roadmap" && <RoadmapView milestones={milestones} setMilestones={setMilestones} tasks={ft} members={members} onEditTask={setEditingTask} />}
        {view === "whiteboard" && <WhiteboardView ideas={fi} projects={fProjects} members={members} onAddIdea={handleAddIdea} onDeleteIdea={handleDeleteIdea} onConvertIdea={handleConvertIdea} defaultAuthor={currentMemberId} onShowImport={() => setShowImport(true)} onUpdateIdea={(id, updates) => {
          setIdeas(prev => prev.map(i => i.id === id ? { ...i, ...updates } : i));
          api.updateIdea(id, updates).catch(console.error);
        }} />}
        {view === "activity" && <ActivityLogView log={activityLog} members={members} onNavigate={setView} />}
        {view === "mapview" && <MapCanvasView projects={fProjects} members={members} annotations={mapAnnotations} setAnnotations={setMapAnnotations} logActivity={logActivity} defaultAuthor={currentMemberId} tasks={ft} onCreateTask={(taskData, cb) => { const saved = { ...taskData, id: `task_${Date.now()}_${Math.random().toString(36).slice(2,6)}`, createdAt: Date.now(), updatedAt: Date.now() }; handleSaveTaskWithNotif(saved); cb?.(saved); setEditingTask(saved); }} onEditTask={setEditingTask} />}
        {view === "catalogue" && <AssetCatalogueView assets={catalogueAssets} setAssets={setCatalogueAssets} currentUser={currentUser} />}
        {view === "nextcloud" && <NextcloudView />}
          </>);
        })()}
      </div>

      {/* MODALS */}
      {searchOpen && <SearchOverlay tasks={tasks} ideas={ideas} milestones={milestones} catalogueAssets={catalogueAssets} projects={projects} members={members} onClose={() => setSearchOpen(false)} onEditTask={setEditingTask} onNavigate={setView} />}
      {(() => {
        const handleMilestoneChange = (taskId, oldMsId, newMsId) => {
          // Garde-fou : on n'attache jamais une tâche à un milestone archivé.
          // (Le détachement d'un milestone archivé reste autorisé — newMsId vide.)
          const targetMs = newMsId ? milestones.find(m => m.id === newMsId) : null;
          if (targetMs?.archived) {
            console.warn('[milestone] tentative d\'ajout dans un milestone archivé bloquée:', newMsId);
            return;
          }
          const updated = milestones.map(m => {
            const filtered = (m.taskIds || []).filter(id => id !== taskId);
            if (m.id === newMsId) return { ...m, taskIds: [...filtered, taskId] };
            return { ...m, taskIds: filtered };
          });
          setMilestones(updated);
          // Sauvegarde immédiate pour éviter la perte au refresh
          api.saveMisc({ milestones: updated, mapAnnotations }).catch(err => console.error('saveMisc (milestone change) failed:', err));
        };
        return (
          <>
            {editingTask && <TaskModal task={editingTask} members={members} tasks={tasks} milestones={milestones} onSave={handleSaveTaskWithNotif} onClose={() => setEditingTask(null)} onDelete={handleDeleteTask} onMilestoneChange={handleMilestoneChange} />}
            {showNewTask && <TaskModal task={{ projectId: projectFilter === "all" ? "sl-v1" : projectFilter, category: newTaskCategory, text: "", description: "", assignees: [], status: "todo", priority: null, deadline: null, notes: "" }} members={members} tasks={tasks} milestones={milestones} onSave={handleSaveTaskWithNotif} onClose={() => { setShowNewTask(false); setNewTaskCategory(""); }} onDelete={() => {}} onMilestoneChange={handleMilestoneChange} />}
          </>
        );
      })()}
      {showImport && <ImportModal onImport={handleImport} onClose={() => setShowImport(false)} projects={projects} />}
    </div>
  );
}