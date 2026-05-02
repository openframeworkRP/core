import { useState, useEffect, useCallback } from 'react'
import {
  Gamepad2, Users, ShieldAlert, ShieldCheck, AlertTriangle, Wallet,
  RefreshCcw, Search, X, ChevronRight, User as UserIcon, Package,
  MapPin, Landmark, Ban, Plus, Trash2, Check, Activity, Clock,
  MessageSquare, LogIn, LogOut, Timer, Globe, Terminal, Gift, Pencil,
} from 'lucide-react'
import { api } from './api.js'

const TAB_ICON = {
  dashboard:  <Gamepad2 size={14} />,
  live:       <Activity size={14} />,
  players:    <Users size={14} />,
  map:        <MapPin size={14} />,
  bans:       <Ban size={14} />,
  whitelist:  <ShieldCheck size={14} />,
  warns:      <AlertTriangle size={14} />,
  logs:       <Activity size={14} />,
  gameadmins: <ShieldAlert size={14} />,
}

const TABS = [
  { id: 'dashboard', label: 'Dashboard' },
  { id: 'live',      label: 'Live' },
  { id: 'players',   label: 'Joueurs' },
  { id: 'map',       label: 'Map' },
  { id: 'bans',      label: 'Bans' },
  { id: 'whitelist', label: 'Whitelist' },
  { id: 'warns',     label: 'Warnings' },
  { id: 'logs',      label: 'Activité' },
  { id: 'gameadmins', label: 'Admins Jeu' },
]

export default function GameAdminPanel() {
  const [tab, setTab] = useState('dashboard')
  const [selectedSteamId, setSelectedSteamId] = useState(null)
  const [selectedCharacterId, setSelectedCharacterId] = useState(null)
  const [globalView, setGlobalView] = useState(null) // 'items' | 'transactions' | null

  return (
    <div style={{ padding: '22px 28px', maxWidth: 1200, margin: '0 auto' }}>
      <Header />

      {/* Tabs */}
      <div style={{ display: 'flex', gap: 6, marginBottom: 22, flexWrap: 'wrap' }}>
        {TABS.map(t => (
          <button
            key={t.id}
            onClick={() => { setTab(t.id); setSelectedSteamId(null); setSelectedCharacterId(null); setGlobalView(null) }}
            className={`adm__btn${tab === t.id ? ' adm__btn--primary' : ' adm__btn--ghost'}`}
          >
            {TAB_ICON[t.id]} {t.label}
          </button>
        ))}
      </div>

      {tab === 'dashboard' && (
        globalView === 'items' ? <AllItemsView onBack={() => setGlobalView(null)} /> :
        globalView === 'transactions' ? <AllTxView onBack={() => setGlobalView(null)} /> :
        <DashboardTab onOpenView={setGlobalView} />
      )}
      {tab === 'players' && (
        selectedCharacterId ? (
          <CharacterDetail id={selectedCharacterId} onBack={() => setSelectedCharacterId(null)} />
        ) : selectedSteamId ? (
          <PlayerDetail
            steamId={selectedSteamId}
            onBack={() => setSelectedSteamId(null)}
            onOpenCharacter={(id) => setSelectedCharacterId(id)}
          />
        ) : (
          <PlayersTab onOpen={(sid) => setSelectedSteamId(sid)} />
        )
      )}
      {tab === 'live'      && <LiveTab onOpenPlayer={(sid) => { setTab('players'); setSelectedSteamId(sid) }} />}
      {tab === 'map'       && <MapTab />}
      {tab === 'bans'      && <BansTab />}
      {tab === 'whitelist' && <WhitelistTab />}
      {tab === 'warns'      && <WarnsTab />}
      {tab === 'logs'       && <LogsTab />}
      {tab === 'gameadmins' && <GameAdminsTab />}
    </div>
  )
}

function Header() {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 18 }}>
      <span style={{
        width: 36, height: 36, borderRadius: 10,
        background: 'linear-gradient(135deg, var(--brand-primary, #e07b39) 0%, #a84820 100%)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        boxShadow: '0 4px 12px rgba(60, 173, 217,0.3)',
      }}>
        <Gamepad2 size={18} color="#fff" />
      </span>
      <div>
        <h2 style={{ fontSize: '1.15rem', fontWeight: 700, color: '#e8e8e8', margin: 0 }}>
          Administration du serveur de jeu
        </h2>
        <p style={{ fontSize: '0.78rem', color: '#71717a', margin: 0, marginTop: 2 }}>
          Contrôle en temps réel des joueurs, bans, whitelist et économie
        </p>
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  DASHBOARD
// ─────────────────────────────────────────────────────────────────────
function DashboardTab({ onOpenView }) {
  const [stats,   setStats]   = useState(null)
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setStats(await api.gameAdminStats()) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  if (loading) return <Loader />
  if (error)   return <ErrorBanner msg={error} onRetry={load} />

  const KPIS = [
    { label: 'Joueurs',      value: stats.users,        color: '#a78bfa', icon: <Users size={18} /> },
    { label: 'Personnages',  value: stats.characters,   color: '#60a5fa', icon: <UserIcon size={18} /> },
    { label: 'Bans',         value: stats.bans,         color: '#f87171', icon: <Ban size={18} /> },
    { label: 'Whitelist',    value: stats.whitelists,   color: '#4ade80', icon: <ShieldCheck size={18} /> },
    { label: 'Warnings',     value: stats.warns,        color: '#facc15', icon: <AlertTriangle size={18} /> },
    { label: 'Comptes',      value: stats.accounts,     color: '#34d399', icon: <Landmark size={18} /> },
    { label: 'Transactions', value: stats.transactions, color: '#fbbf24', icon: <Wallet size={18} />, view: 'transactions' },
    { label: 'Items',        value: stats.items,        color: '#f472b6', icon: <Package size={18} />, view: 'items' },
  ]

  return (
    <>
      <div style={{
        display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))',
        gap: 12, marginBottom: 20,
      }}>
        {KPIS.map(k => {
          const clickable = !!k.view
          const Tag = clickable ? 'button' : 'div'
          return (
            <Tag key={k.label}
              onClick={clickable ? () => onOpenView(k.view) : undefined}
              style={{
                background: '#1a1f2c', border: '1px solid rgba(255,255,255,0.06)',
                borderRadius: 10, padding: '14px 16px',
                display: 'flex', alignItems: 'center', gap: 12,
                cursor: clickable ? 'pointer' : 'default',
                textAlign: 'left', width: '100%', fontFamily: 'inherit',
                transition: 'border-color 0.15s, transform 0.12s',
              }}
              onMouseEnter={clickable ? e => { e.currentTarget.style.borderColor = k.color + '88'; e.currentTarget.style.transform = 'translateY(-1px)' } : undefined}
              onMouseLeave={clickable ? e => { e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)'; e.currentTarget.style.transform = 'none' } : undefined}
            >
              <span style={{
                width: 36, height: 36, borderRadius: 8,
                background: k.color + '22', color: k.color,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
              }}>{k.icon}</span>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: '1.4rem', fontWeight: 700, color: '#e8e8e8', lineHeight: 1 }}>
                  {k.value}
                </div>
                <div style={{ fontSize: '0.72rem', color: '#71717a', marginTop: 4, display: 'flex', alignItems: 'center', gap: 4 }}>
                  {k.label}
                  {clickable && <ChevronRight size={11} style={{ color: '#52525b' }} />}
                </div>
              </div>
            </Tag>
          )
        })}
      </div>

      {/* Richesse totale */}
      <div style={{
        background: 'linear-gradient(135deg, rgba(52,211,153,0.08), rgba(52,211,153,0.02))',
        border: '1px solid rgba(52,211,153,0.2)', borderRadius: 12,
        padding: '18px 22px', display: 'flex', alignItems: 'center', gap: 16,
      }}>
        <Wallet size={28} style={{ color: '#34d399' }} />
        <div style={{ flex: 1 }}>
          <div style={{ color: '#71717a', fontSize: '0.78rem', marginBottom: 2 }}>Masse monétaire totale</div>
          <div style={{ color: '#34d399', fontSize: '1.6rem', fontWeight: 700 }}>
            {formatMoney(stats.totalMoney)}
          </div>
        </div>
        <button onClick={load} style={iconBtn()} title="Rafraîchir"><RefreshCcw size={14} /></button>
      </div>
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  PLAYERS — liste
// ─────────────────────────────────────────────────────────────────────
function PlayersTab({ onOpen }) {
  const [users,   setUsers]   = useState([])
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [search,  setSearch]  = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setUsers(await api.gameAdminUsers()) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  const filtered = users.filter(u => {
    if (!search) return true
    const q = search.trim().toLowerCase()
    return u.steamId.includes(q) || (u.displayName || '').toLowerCase().includes(q)
  })

  if (loading) return <Loader />
  if (error)   return <ErrorBanner msg={error} onRetry={load} />

  return (
    <>
      <div style={{ display: 'flex', gap: 8, marginBottom: 14, alignItems: 'center' }}>
        <div style={{
          flex: 1, display: 'flex', alignItems: 'center', gap: 8,
          background: '#161a26', border: '1px solid rgba(255,255,255,0.08)',
          borderRadius: 8, padding: '7px 12px',
        }}>
          <Search size={14} style={{ color: '#52525b' }} />
          <input
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Rechercher par nom ou SteamID…"
            style={{
              flex: 1, background: 'none', border: 'none', outline: 'none',
              color: '#e8e8e8', fontSize: '0.85rem', fontFamily: 'inherit',
            }}
          />
          {search && (
            <button onClick={() => setSearch('')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#52525b' }}>
              <X size={14} />
            </button>
          )}
        </div>
        <button onClick={load} style={iconBtn()} title="Rafraîchir"><RefreshCcw size={14} /></button>
      </div>

      {filtered.length === 0 ? (
        <Empty msg={users.length === 0 ? 'Aucun joueur en base.' : 'Aucun résultat.'} />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {filtered.map(u => (
            <button key={u.steamId} onClick={() => onOpen(u.steamId)} style={{
              background: '#161a26', border: '1px solid rgba(255,255,255,0.06)',
              borderRadius: 10, padding: '12px 14px', cursor: 'pointer',
              display: 'flex', alignItems: 'center', gap: 12, textAlign: 'left',
              transition: 'border-color 0.15s',
            }} onMouseEnter={e => e.currentTarget.style.borderColor = 'rgba(60, 173, 217,0.35)'}
               onMouseLeave={e => e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)'}>
              {u.avatar ? (
                <img src={u.avatar} alt="" style={{ width: 34, height: 34, borderRadius: '50%', objectFit: 'cover', border: '1px solid rgba(255,255,255,0.1)' }} />
              ) : (
                <span style={{
                  width: 34, height: 34, borderRadius: '50%', background: '#2a2f3e',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  color: '#71717a',
                }}>
                  <UserIcon size={16} />
                </span>
              )}
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontWeight: 600, color: '#e8e8e8', fontSize: '0.88rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {u.displayName || u.steamId}
                </div>
                <div style={{ fontSize: '0.7rem', color: '#52525b', marginTop: 2, fontFamily: 'monospace', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {u.steamId}
                </div>
                <div style={{ fontSize: '0.72rem', color: '#71717a', marginTop: 3, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  <span>{u.characters} personnage{u.characters > 1 ? 's' : ''}</span>
                  {u.isBanned      && <Pill color="#f87171" label="Banni" />}
                  {u.isWhitelisted && <Pill color="#4ade80" label="Whitelist" />}
                  {u.warnCount > 0 && <Pill color="#facc15" label={`${u.warnCount} warn${u.warnCount > 1 ? 's' : ''}`} />}
                </div>
              </div>
              <ChevronRight size={16} style={{ color: '#52525b' }} />
            </button>
          ))}
        </div>
      )}
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  PLAYER DETAIL
// ─────────────────────────────────────────────────────────────────────
// Barre d'actions admin in-game pour un joueur — déposent une commande dans
// la queue API, exécutée par le gamemode dans les 5 secondes (poller).
const ADMIN_QUICK_ACTIONS = [
  { id: 'givemoney', label: 'Donner argent', color: '#4ade80', fields: [{ key: 'amount', label: 'Montant (€)', type: 'number', defaultValue: 1000 }] },
  { id: 'givebank',  label: 'Donner banque', color: '#34d399', fields: [{ key: 'amount', label: 'Montant (€)', type: 'number', defaultValue: 1000 }] },
  { id: 'heal',      label: 'Soigner',       color: '#60a5fa', fields: [{ key: 'amount', label: 'HP (-1 = full)', type: 'number', defaultValue: -1 }] },
  { id: 'giveitem',  label: 'Donner item',   color: '#a78bfa', fields: [
      { key: 'itemid',   label: 'ResourceName', type: 'text',   defaultValue: '' },
      { key: 'quantity', label: 'Quantité',     type: 'number', defaultValue: 1 },
  ] },
  { id: 'kick',      label: 'Kick',          color: '#fb923c', fields: [{ key: 'reason', label: 'Raison', type: 'text', defaultValue: 'Kicked by admin' }] },
  { id: 'ban',       label: 'Ban',           color: '#f87171', fields: [
      { key: 'duration', label: 'Durée (min, 0=perma)', type: 'number', defaultValue: 0 },
      { key: 'reason',   label: 'Raison',               type: 'text',   defaultValue: 'Banned by admin' },
  ] },
]

function AdminActionsBar({ steamId }) {
  const [openAction, setOpenAction] = useState(null)
  const [feedback, setFeedback] = useState(null) // { ok, msg }

  return (
    <>
      <SectionTitle icon={<ShieldAlert size={14} />} title="Actions admin in-game" />
      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 16 }}>
        {ADMIN_QUICK_ACTIONS.map(a => (
          <button key={a.id} onClick={() => setOpenAction(a)} style={{
            background: a.color + '18', border: `1px solid ${a.color}40`,
            color: a.color, borderRadius: 8, padding: '7px 14px',
            fontSize: '0.8rem', fontFamily: 'inherit', fontWeight: 600, cursor: 'pointer',
          }}>
            {a.label}
          </button>
        ))}
      </div>
      {feedback && (
        <div style={{
          padding: '8px 12px', borderRadius: 8, marginBottom: 14,
          background: feedback.ok ? 'rgba(74,222,128,0.12)' : 'rgba(248,113,113,0.12)',
          color: feedback.ok ? '#4ade80' : '#f87171',
          fontSize: '0.8rem', border: `1px solid ${feedback.ok ? 'rgba(74,222,128,0.3)' : 'rgba(248,113,113,0.3)'}`,
        }}>
          {feedback.msg}
        </div>
      )}
      {openAction && (
        <CommandModal
          action={openAction}
          steamId={steamId}
          onClose={() => setOpenAction(null)}
          onResult={(ok, msg) => { setFeedback({ ok, msg }); setTimeout(() => setFeedback(null), 6000) }}
        />
      )}
    </>
  )
}

// Statuts terminaux renvoyés par l'API jeu (success / failure)
const TERMINAL_OK   = new Set(['executed', 'completed', 'success', 'processed'])
const TERMINAL_FAIL = new Set(['failed', 'error'])

// Poll le statut d'une commande déjà queueée jusqu'à terminal ou timeout (~15s).
// Utilisé après une action admin DB-directe (rename/delete character) qui a
// enqueué un ack côté gamemode, ET par sendCommandAndTrack ci-dessous.
async function pollCommandStatus(commandId, { timeoutMs = 15000, onProgress } = {}) {
  if (!commandId) {
    onProgress?.({ phase: 'no-tracking' })
    return { status: 'no-tracking' }
  }
  const start = Date.now()
  const pollEvery = 1500
  while (Date.now() - start < timeoutMs) {
    await new Promise(r => setTimeout(r, pollEvery))
    try {
      const cmd = await api.gameAdminGetCommand(commandId)
      const status = (cmd?.status || cmd?.Status || '').toLowerCase()
      if (status) {
        onProgress?.({ phase: status, command: cmd })
        if (TERMINAL_OK.has(status) || TERMINAL_FAIL.has(status)) {
          return { status, command: cmd }
        }
      }
    } catch { /* on continue à poller */ }
  }
  onProgress?.({ phase: 'timeout' })
  return { status: 'timeout' }
}

// Envoie une commande via la queue et poll son statut jusqu'à exécution ou timeout (~15s)
async function sendCommandAndTrack({ command, targetSteamId, args, timeoutMs = 15000, onProgress }) {
  const queued = await api.gameAdminQueueCommand({ command, targetSteamId, args })
  const queuedId = queued?.id || queued?.commandId || queued?.Id
  onProgress?.({ phase: 'queued', queuedId })
  return await pollCommandStatus(queuedId, { timeoutMs, onProgress })
}

function StatusBadge({ phase }) {
  const map = {
    queued:       { label: 'En queue',     color: '#a1a1aa' },
    pending:      { label: 'En attente',   color: '#a1a1aa' },
    processing:   { label: 'En cours',     color: '#facc15' },
    running:      { label: 'En cours',     color: '#facc15' },
    executed:     { label: 'Exécutée',     color: '#4ade80' },
    completed:    { label: 'Exécutée',     color: '#4ade80' },
    success:      { label: 'Exécutée',     color: '#4ade80' },
    processed:    { label: 'Confirmée in-game', color: '#4ade80' },
    failed:       { label: 'Échec côté jeu', color: '#f87171' },
    error:        { label: 'Erreur',       color: '#f87171' },
    timeout:      { label: 'Timeout — pas confirmée', color: '#fb923c' },
    'no-tracking':{ label: 'Pas de tracking', color: '#71717a' },
  }
  const m = map[phase] || { label: phase, color: '#71717a' }
  return (
    <span style={{
      padding: '3px 9px', borderRadius: 99, fontSize: '0.72rem', fontWeight: 700,
      background: m.color + '22', color: m.color, border: `1px solid ${m.color}44`,
    }}>{m.label}</span>
  )
}

function CommandModal({ action, steamId, onClose, onResult }) {
  const [values, setValues] = useState(
    Object.fromEntries(action.fields.map(f => [f.key, f.defaultValue]))
  )
  const [submitting, setSubmitting] = useState(false)
  const [trackPhase, setTrackPhase] = useState(null)

  async function submit() {
    setSubmitting(true); setTrackPhase('queued')
    try {
      const args = { ...values }
      for (const f of action.fields) {
        if (f.type === 'number') args[f.key] = Number(args[f.key])
      }
      const { status } = await sendCommandAndTrack({
        command: action.id, targetSteamId: steamId, args,
        onProgress: ({ phase }) => setTrackPhase(phase),
      })
      if (TERMINAL_OK.has(status)) {
        onResult(true, `"${action.label}" confirmée in-game.`)
        onClose()
      } else if (status === 'timeout') {
        onResult(false, `"${action.label}" envoyée mais non confirmée (timeout 15s) — gamemode actif ?`)
      } else {
        onResult(false, `"${action.label}" : ${status}`)
      }
    } catch (e) {
      onResult(false, `Échec : ${e.message}`)
    } finally { setSubmitting(false) }
  }

  return (
    <Modal title={action.label} onClose={onClose}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginBottom: 16 }}>
        {action.fields.map(f => (
          <div key={f.key}>
            <label style={{ display: 'block', fontSize: '0.72rem', color: '#a1a1aa', marginBottom: 4, fontWeight: 600 }}>
              {f.label}
            </label>
            <input
              type={f.type}
              value={values[f.key]}
              onChange={e => setValues(v => ({ ...v, [f.key]: e.target.value }))}
              style={inputStyle({ width: '100%', boxSizing: 'border-box' })}
            />
          </div>
        ))}
      </div>
      {trackPhase && submitting && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', marginBottom: 12, background: '#11151f', borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)' }}>
          <span style={{ fontSize: '0.78rem', color: '#a1a1aa' }}>Statut :</span>
          <StatusBadge phase={trackPhase} />
        </div>
      )}
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
        <button onClick={onClose} disabled={submitting} style={{ ...inputStyle(), cursor: submitting ? 'not-allowed' : 'pointer', opacity: submitting ? 0.5 : 1 }}>Annuler</button>
        <button onClick={submit} disabled={submitting} style={{
          background: action.color, color: '#0a0e18', border: 'none',
          borderRadius: 6, padding: '8px 16px', fontWeight: 700, cursor: 'pointer',
          fontFamily: 'inherit', opacity: submitting ? 0.5 : 1,
        }}>{submitting ? 'Envoi…' : 'Confirmer'}</button>
      </div>
    </Modal>
  )
}

function PlayerDetail({ steamId, onBack, onOpenCharacter }) {
  const [data,    setData]    = useState(null)
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setData(await api.gameAdminUser(steamId)) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [steamId])

  useEffect(() => { load() }, [load])

  return (
    <>
      <BackBar onBack={onBack} label={`SteamID ${steamId}`} onRefresh={load} />
      {loading ? <Loader /> : error ? <ErrorBanner msg={error} onRetry={load} /> : (
        <>
          {/* statuts */}
          <div style={{ display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
            {data.ban       && <Pill color="#f87171" label={`Banni — ${data.ban.reason || 'sans raison'}`} big />}
            {data.whitelist && <Pill color="#4ade80" label="Whitelist" big />}
            {data.warns?.length > 0 && <Pill color="#facc15" label={`${data.warns.length} warning${data.warns.length > 1 ? 's' : ''}`} big />}
            {!data.ban && !data.whitelist && (!data.warns || data.warns.length === 0) && (
              <Pill color="#71717a" label="Aucun flag" big />
            )}
          </div>

          {/* Actions admin in-game (queue → exécutées par le gamemode dans 5s) */}
          <AdminActionsBar steamId={steamId} />


          {/* characters */}
          <SectionTitle icon={<UserIcon size={14} />} title={`Personnages (${data.characters.length})`} />
          {data.characters.length === 0 ? (
            <Empty msg="Aucun personnage créé." />
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 20 }}>
              {data.characters.map(c => (
                <button key={c.id} onClick={() => onOpenCharacter(c.id)} style={{
                  background: '#161a26', border: '1px solid rgba(255,255,255,0.06)',
                  borderRadius: 10, padding: '11px 14px', cursor: 'pointer',
                  display: 'flex', alignItems: 'center', gap: 12, textAlign: 'left',
                }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 600, color: '#e8e8e8', fontSize: '0.88rem', display: 'flex', alignItems: 'center', gap: 8 }}>
                      {c.firstName} {c.lastName}
                      {c.isSelected && <span style={{ fontSize: '0.65rem', background: 'var(--brand-primary, #e07b39)22', color: 'var(--brand-primary, #e07b39)', padding: '2px 7px', borderRadius: 99, fontWeight: 700 }}>ACTIF</span>}
                    </div>
                    <div style={{ fontSize: '0.72rem', color: '#71717a', marginTop: 2 }}>
                      {c.age} ans · {c.gender === 0 ? 'Homme' : 'Femme'} · {c.height}cm · {c.weight}kg
                    </div>
                  </div>
                  <ChevronRight size={16} style={{ color: '#52525b' }} />
                </button>
              ))}
            </div>
          )}

          {/* warns */}
          {data.warns?.length > 0 && (
            <>
              <SectionTitle icon={<AlertTriangle size={14} />} title="Warnings" />
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {data.warns.map(w => (
                  <div key={w.id} style={cardStyle()}>
                    <div style={{ fontSize: '0.82rem', color: '#e8e8e8' }}>{w.reason || '(sans raison)'}</div>
                    <div style={{ fontSize: '0.7rem', color: '#71717a', marginTop: 3 }}>Par {w.fromAdminSteamId}</div>
                  </div>
                ))}
              </div>
            </>
          )}
        </>
      )}
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  CHARACTER DETAIL
// ─────────────────────────────────────────────────────────────────────
function CharacterDetail({ id, onBack }) {
  const [data,       setData]       = useState(null)
  const [txs,        setTxs]        = useState({})  // { [accountId]: [transactions] }
  const [loading,    setLoading]    = useState(true)
  const [error,      setError]      = useState('')
  const [openItem,   setOpenItem]   = useState(null)   // item complet (metadata)
  const [openTx,     setOpenTx]     = useState(null)   // transaction complète
  const [showGive,   setShowGive]   = useState(false)  // modal donner un item
  const [showRename, setShowRename] = useState(false)  // modal renommer le perso
  const [showDelete, setShowDelete] = useState(false)  // modal confirmer suppression
  const [live,       setLive]       = useState(true)   // auto-refresh
  const [lastRefresh, setLastRefresh] = useState(null) // timestamp dernier refresh OK

  // Pause l'auto-refresh quand une modal est ouverte ou que l'item courant
  // est en cours d'édition — sinon le refresh remplace `data` et fait sauter
  // les sélections de l'admin.
  const paused = !!openItem || !!openTx || showGive || showRename || showDelete

  const load = useCallback(async (opts) => {
    const silent = !!(opts && opts.silent === true)
    if (!silent) setLoading(true)
    setError('')
    try {
      const d = await api.gameAdminCharacter(id)
      setData(d)
      setLastRefresh(Date.now())
    }
    catch (e) { setError(e.message) }
    finally { if (!silent) setLoading(false) }
  }, [id])

  useEffect(() => { load() }, [load])

  // Auto-refresh toutes les 3s, silencieusement, tant que `live` est ON,
  // que la fenêtre est visible, et qu'aucune modal n'est ouverte.
  useEffect(() => {
    if (!live || paused) return
    let cancelled = false
    const tick = () => {
      if (cancelled) return
      if (document.visibilityState !== 'visible') return
      load({ silent: true })
    }
    const intv = setInterval(tick, 3000)
    return () => { cancelled = true; clearInterval(intv) }
  }, [live, paused, load])

  async function loadTx(accountId) {
    if (txs[accountId]) return
    try {
      const data = await api.gameAdminAccountTx(accountId)
      setTxs(t => ({ ...t, [accountId]: data }))
    } catch (e) {
      setTxs(t => ({ ...t, [accountId]: { error: e.message } }))
    }
  }

  return (
    <>
      <BackBar
        onBack={onBack}
        label={data ? `${data.character.firstName} ${data.character.lastName}` : `Character ${id.slice(0, 8)}…`}
        onRefresh={() => load()}
        live={live}
        livePaused={paused}
        onToggleLive={() => setLive(v => !v)}
        lastRefresh={lastRefresh}
      />
      {loading ? <Loader /> : error ? <ErrorBanner msg={error} onRetry={load} /> : (
        <>
          {/* Identité */}
          <div style={{
            background: 'linear-gradient(135deg, rgba(60, 173, 217,0.08), rgba(60, 173, 217,0.02))',
            border: '1px solid rgba(60, 173, 217,0.2)', borderRadius: 12,
            padding: '16px 20px', marginBottom: 18,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
              <span style={{ width: 44, height: 44, borderRadius: '50%', background: 'var(--brand-primary, #e07b39)22', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--brand-primary, #e07b39)' }}>
                <UserIcon size={20} />
              </span>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: '1.1rem', fontWeight: 700, color: '#e8e8e8' }}>
                  {data.character.firstName} {data.character.lastName}
                  {data.character.isSelected && <span style={{ marginLeft: 10, fontSize: '0.65rem', background: 'var(--brand-primary, #e07b39)22', color: 'var(--brand-primary, #e07b39)', padding: '2px 8px', borderRadius: 99, fontWeight: 700 }}>ACTIF</span>}
                </div>
                <div style={{ fontSize: '0.78rem', color: '#71717a', marginTop: 3 }}>
                  {data.character.age} ans · {data.character.gender === 0 ? 'Homme' : 'Femme'} · {data.character.height}cm · {data.character.weight}kg
                </div>
                <div style={{ fontSize: '0.7rem', color: '#52525b', marginTop: 4, fontFamily: 'monospace' }}>
                  {data.character.id}
                </div>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                <button
                  onClick={() => setShowRename(true)}
                  className="adm__btn adm__btn--ghost"
                  style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: '0.74rem', padding: '6px 10px' }}
                  title="Modifier le prénom / nom RP du personnage"
                >
                  <Pencil size={12} /> Renommer
                </button>
                <button
                  onClick={() => setShowDelete(true)}
                  style={{
                    background: '#f8717118', border: '1px solid #f8717140', color: '#f87171',
                    borderRadius: 7, padding: '6px 10px', fontSize: '0.74rem', fontWeight: 600,
                    cursor: 'pointer', fontFamily: 'inherit',
                    display: 'inline-flex', alignItems: 'center', gap: 5,
                  }}
                  title="Supprimer définitivement ce personnage"
                >
                  <Trash2 size={12} /> Supprimer
                </button>
              </div>
            </div>
          </div>

          {/* Détails RP — toutes les infos d'apparence/identité que renvoie l'API,
               pour pouvoir comparer avec ce qui est affiché en jeu. */}
          <SectionTitle icon={<UserIcon size={14} />} title="Détails RP" />
          <CharacterRpDetails character={data.character} />

          {/* Tenue équipée — détecte parmi les items d'inventaire ceux qui
               ressemblent à des vêtements et les groupe par slot, pour pouvoir
               comparer avec ce que porte le perso en jeu. */}
          <SectionTitle icon={<Package size={14} />} title="Tenue équipée" />
          <CharacterOutfit items={data.items} />

          {/* Position */}
          {data.position && (
            <>
              <SectionTitle icon={<MapPin size={14} />} title="Dernière position" />
              <div style={cardStyle({ marginBottom: 18 })}>
                <span style={{ fontFamily: 'monospace', fontSize: '0.82rem', color: '#a1a1aa' }}>
                  X: {data.position.x.toFixed(1)} · Y: {data.position.y.toFixed(1)} · Z: {data.position.z.toFixed(1)}
                </span>
              </div>
            </>
          )}

          {/* Banque */}
          <SectionTitle icon={<Wallet size={14} />} title={`Comptes bancaires (${data.accounts.length})`} />
          {data.accounts.length === 0 ? (
            <Empty msg="Aucun compte bancaire." />
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 18 }}>
              {data.accounts.map(a => (
                <div key={a.id} style={cardStyle()}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                    <div style={{ flex: 1 }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <span style={{ fontWeight: 600, color: '#e8e8e8', fontSize: '0.88rem' }}>{a.accountName}</span>
                        <span style={{ fontSize: '0.68rem', color: '#71717a', fontFamily: 'monospace' }}>{a.accountNumber}</span>
                        <Pill color={a.accountType === 0 ? '#60a5fa' : '#a78bfa'} label={a.accountType === 0 ? 'Personnel' : 'Partagé'} />
                      </div>
                    </div>
                    <div style={{ fontSize: '1.1rem', fontWeight: 700, color: '#34d399' }}>
                      {formatMoney(a.balance)}
                    </div>
                    <button onClick={() => loadTx(a.id)} style={iconBtn()} title="Voir transactions">
                      <ChevronRight size={14} />
                    </button>
                  </div>
                  {txs[a.id] && (
                    <div style={{ marginTop: 10, paddingTop: 10, borderTop: '1px solid rgba(255,255,255,0.06)' }}>
                      {txs[a.id].error ? (
                        <div style={{ color: '#f87171', fontSize: '0.78rem' }}>Erreur : {txs[a.id].error}</div>
                      ) : txs[a.id].length === 0 ? (
                        <div style={{ fontSize: '0.78rem', color: '#71717a' }}>Aucune transaction.</div>
                      ) : (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                          {txs[a.id].slice(0, 20).map(t => (
                            <button key={t.id} onClick={() => setOpenTx({ ...t, _accountId: a.id })} style={{
                              display: 'flex', alignItems: 'center', gap: 10, fontSize: '0.76rem',
                              padding: '6px 8px', color: '#a1a1aa', cursor: 'pointer',
                              background: 'transparent', border: '1px solid transparent', borderRadius: 6,
                              textAlign: 'left', width: '100%', fontFamily: 'inherit',
                            }} onMouseEnter={e => { e.currentTarget.style.background = 'rgba(255,255,255,0.03)'; e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)' }}
                               onMouseLeave={e => { e.currentTarget.style.background = 'transparent'; e.currentTarget.style.borderColor = 'transparent' }}>
                              <span style={{ fontSize: '0.65rem', padding: '2px 6px', borderRadius: 4, background: '#2a2f3e', color: '#71717a', minWidth: 80, textAlign: 'center' }}>
                                {txTypeLabel(t.type)}
                              </span>
                              <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{t.comment || '—'}</span>
                              <span style={{ fontWeight: 600, color: t.toAccountId === a.id ? '#4ade80' : '#f87171' }}>
                                {t.toAccountId === a.id ? '+' : '−'}{formatMoney(t.amount)}
                              </span>
                              <span style={{ fontSize: '0.68rem', color: '#52525b' }}>
                                {new Date(t.createdAt).toLocaleDateString('fr-FR')}
                              </span>
                            </button>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {/* Historique inventaire (transferts) */}
          <CharacterInventoryHistory characterId={id} ownerSteamId={data.character.ownerId} />

          {/* Inventaire */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
            <SectionTitle icon={<Package size={14} />} title={`Inventaire (${data.items.length} items)`} noMargin />
            <button
              onClick={() => setShowGive(true)}
              className="adm__btn adm__btn--ghost"
              style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: '0.75rem' }}
            >
              <Gift size={12} /> Donner un item
            </button>
          </div>
          {data.items.length === 0 ? (
            <Empty msg="Inventaire vide." />
          ) : (
            <div style={{
              display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: 8,
            }}>
              {data.items.map(it => (
                <button key={it.id} onClick={() => setOpenItem(it)} style={{
                  ...cardStyle(), cursor: 'pointer', textAlign: 'left', width: '100%',
                  fontFamily: 'inherit', color: 'inherit', transition: 'border-color 0.15s',
                }} onMouseEnter={e => e.currentTarget.style.borderColor = 'rgba(60, 173, 217,0.35)'}
                   onMouseLeave={e => e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)'}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span style={{
                      width: 30, height: 30, borderRadius: 6, background: '#2a2f3e',
                      display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#71717a',
                    }}>
                      <Package size={14} />
                    </span>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: '0.82rem', fontWeight: 600, color: '#e8e8e8', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {it.itemGameId}
                      </div>
                      <div style={{ fontSize: '0.68rem', color: '#71717a', marginTop: 2 }}>
                        ×{it.count} · {it.mass}kg · case [{it.line},{it.collum}]
                      </div>
                    </div>
                    <ChevronRight size={14} style={{ color: '#52525b' }} />
                  </div>
                </button>
              ))}
            </div>
          )}
        </>
      )}

      {openItem && (
        <ItemModal
          item={openItem}
          owner={data?.character ? { firstName: data.character.firstName, lastName: data.character.lastName, id: data.character.id, ownerId: data.character.ownerId } : null}
          onClose={() => setOpenItem(null)}
          onChanged={() => { load(); setOpenItem(null) }}
        />
      )}
      {openTx && <TxModal tx={openTx} onClose={() => setOpenTx(null)} />}
      {showGive && data?.character && (
        <GiveItemModal
          characterId={data.character.id}
          onClose={() => setShowGive(false)}
          onDone={() => { load(); setShowGive(false) }}
        />
      )}
      {showRename && data?.character && (
        <RenameCharacterModal
          character={data.character}
          onClose={() => setShowRename(false)}
          onDone={() => { load(); setShowRename(false) }}
        />
      )}
      {showDelete && data?.character && (
        <DeleteCharacterModal
          character={data.character}
          onClose={() => setShowDelete(false)}
          onDone={() => { setShowDelete(false); onBack() }}
        />
      )}
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  CHARACTER RP DETAILS — vue détaillée des champs d'apparence/identité
//  pour comparer avec ce qui est affiché en jeu.
// ─────────────────────────────────────────────────────────────────────
// Le payload exact renvoyé par l'API du gamemode peut varier ; on essaie
// plusieurs noms de champs plausibles avant d'afficher "—".
function pickField(obj, ...keys) {
  for (const k of keys) {
    if (obj && obj[k] !== undefined && obj[k] !== null && obj[k] !== '') return obj[k]
  }
  return null
}

function formatGender(v) {
  if (v === 0 || v === '0' || v === 'male' || v === 'M') return 'Homme'
  if (v === 1 || v === '1' || v === 'female' || v === 'F') return 'Femme'
  if (v == null) return null
  return String(v)
}

function CharacterRpDetails({ character: c }) {
  if (!c) return null

  const firstName = pickField(c, 'firstName', 'FirstName', 'first_name')
  const lastName  = pickField(c, 'lastName',  'LastName',  'last_name')
  const age       = pickField(c, 'age', 'Age')
  const gender    = formatGender(pickField(c, 'gender', 'Gender', 'sex'))
  const height    = pickField(c, 'height', 'Height')
  const weight    = pickField(c, 'weight', 'Weight')
  const city      = pickField(c, 'city', 'City', 'cityOfBirth', 'CityOfBirth', 'town', 'Town', 'placeOfBirth', 'PlaceOfBirth', 'birthPlace', 'BirthPlace', 'hometown', 'Hometown', 'originCity')
  const skin      = pickField(c, 'skinColor', 'SkinColor', 'skin', 'Skin', 'skinTone', 'SkinTone', 'skin_color')
  const morph     = pickField(c, 'morph', 'Morph', 'morphology', 'Morphology', 'bodyMorph', 'BodyMorph', 'bodyType', 'BodyType')

  // Champs déjà couverts par les libellés ci-dessus (toutes variantes confondues) :
  // on les exclut du dump "autres champs" pour ne montrer que ce qui n'a pas
  // encore de label dédié — utile pour repérer un champ renommé côté gamemode.
  const KNOWN = new Set([
    'id', 'isSelected', 'ownerId',
    'firstName', 'FirstName', 'first_name',
    'lastName',  'LastName',  'last_name',
    'age', 'Age',
    'gender', 'Gender', 'sex',
    'height', 'Height',
    'weight', 'Weight',
    'city', 'City', 'cityOfBirth', 'CityOfBirth', 'town', 'Town',
    'placeOfBirth', 'PlaceOfBirth', 'birthPlace', 'BirthPlace',
    'hometown', 'Hometown', 'originCity',
    'skinColor', 'SkinColor', 'skin', 'Skin', 'skinTone', 'SkinTone', 'skin_color',
    'morph', 'Morph', 'morphology', 'Morphology', 'bodyMorph', 'BodyMorph',
    'bodyType', 'BodyType',
  ])
  const extras = Object.entries(c).filter(([k, v]) =>
    !KNOWN.has(k) && v !== null && v !== undefined && typeof v !== 'function'
  )

  const rows = [
    { label: 'Prénom',           value: firstName },
    { label: 'Nom',              value: lastName },
    { label: 'Âge',              value: age != null ? `${age} ans` : null },
    { label: 'Genre',            value: gender },
    { label: 'Taille',           value: height != null ? `${height} cm` : null },
    { label: 'Poids',            value: weight != null ? `${weight} kg` : null },
    { label: 'Ville',            value: city },
    { label: 'Couleur de peau',  value: skin },
    { label: 'Morphologie',      value: morph },
  ]

  return (
    <div style={cardStyle({ marginBottom: 18, padding: '14px 16px' })}>
      <div style={{
        display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
        gap: '10px 18px',
      }}>
        {rows.map(r => (
          <div key={r.label} style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <span style={{ fontSize: '0.66rem', color: '#71717a', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 700 }}>
              {r.label}
            </span>
            <span style={{ fontSize: '0.86rem', color: r.value != null ? '#e8e8e8' : '#52525b' }}>
              {r.value != null ? String(r.value) : '—'}
            </span>
          </div>
        ))}
      </div>
      {extras.length > 0 && (
        <details style={{ marginTop: 14, paddingTop: 12, borderTop: '1px solid rgba(255,255,255,0.06)' }}>
          <summary style={{ cursor: 'pointer', fontSize: '0.72rem', color: '#a1a1aa', fontWeight: 600 }}>
            Autres champs renvoyés par l'API ({extras.length})
          </summary>
          <div style={{
            marginTop: 10, fontFamily: 'monospace', fontSize: '0.74rem',
            color: '#a1a1aa', background: '#11151f', border: '1px solid rgba(255,255,255,0.05)',
            borderRadius: 6, padding: '10px 12px', whiteSpace: 'pre-wrap', wordBreak: 'break-word',
          }}>
            {extras.map(([k, v]) => (
              <div key={k}><span style={{ color: 'var(--brand-primary, #e07b39)' }}>{k}</span>: {typeof v === 'object' ? JSON.stringify(v) : String(v)}</div>
            ))}
          </div>
        </details>
      )}
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  CHARACTER OUTFIT — repère parmi les items d'inventaire ceux qui sont
//  équipés (ou qui ressemblent à des vêtements) et les liste par slot.
//  Le gamemode peut varier sur la façon de marquer un item équipé : on
//  essaie plusieurs conventions (flag dans metadata, champ dédié, ou
//  pattern sur l'itemGameId en dernier recours).
// ─────────────────────────────────────────────────────────────────────
const CLOTHING_PATTERNS = [
  { slot: 'Chapeau',    re: /(^|_)(hat|cap|helmet|beanie|chapeau|casque)(_|$)/i },
  { slot: 'Lunettes',   re: /(^|_)(glasses|sunglasses|lunettes)(_|$)/i },
  { slot: 'Masque',     re: /(^|_)(mask|masque|bandana)(_|$)/i },
  { slot: 'Collier',    re: /(^|_)(necklace|collier|chain)(_|$)/i },
  { slot: 'Veste',      re: /(^|_)(jacket|coat|veste|manteau|hoodie|blazer)(_|$)/i },
  { slot: 'Haut',       re: /(^|_)(shirt|tshirt|top|haut|tee)(_|$)/i },
  { slot: 'Sac',        re: /(^|_)(backpack|bag|sac)(_|$)/i },
  { slot: 'Ceinture',   re: /(^|_)(belt|ceinture)(_|$)/i },
  { slot: 'Pantalon',   re: /(^|_)(pants|trousers|jeans|shorts|pantalon|short)(_|$)/i },
  { slot: 'Chaussures', re: /(^|_)(shoes|boots|sneakers|chaussures|bottes|baskets)(_|$)/i },
  { slot: 'Gants',      re: /(^|_)(gloves|gants|mitten)(_|$)/i },
  { slot: 'Montre',     re: /(^|_)(watch|montre|bracelet)(_|$)/i },
  { slot: 'Bague',      re: /(^|_)(ring|bague)(_|$)/i },
]

function detectSlot(item) {
  const meta = item.metadata || {}
  const explicit = meta.slot ?? meta.Slot ?? meta.equipSlot ?? meta.EquipSlot
    ?? meta.wearSlot ?? meta.WearSlot ?? meta.bodyPart ?? meta.BodyPart
    ?? meta.bodySlot ?? meta.BodySlot ?? null
  if (explicit) return String(explicit)
  for (const { slot, re } of CLOTHING_PATTERNS) {
    if (re.test(item.itemGameId || '')) return slot
  }
  return null
}

function isEquipped(item) {
  const meta = item.metadata || {}
  const flag = meta.equipped ?? meta.isEquipped ?? meta.Equipped ?? meta.IsEquipped
    ?? meta.worn ?? meta.Worn ?? meta.isWorn ?? meta.IsWorn
  if (flag === true || flag === 1 || flag === '1' || flag === 'true') return true
  return false
}

function CharacterOutfit({ items }) {
  if (!items || items.length === 0) {
    return <Empty msg="Aucun item dans l'inventaire." />
  }

  // Un item compte comme "tenue" si :
  //  - flag équipé explicite dans la metadata, OU
  //  - on détecte un slot (explicite ou par pattern sur l'itemGameId).
  const outfit = items
    .map(it => ({ item: it, slot: detectSlot(it), equipped: isEquipped(it) }))
    .filter(x => x.equipped || x.slot)

  if (outfit.length === 0) {
    return (
      <div style={cardStyle({ marginBottom: 18, color: '#71717a', fontSize: '0.82rem' })}>
        Aucun vêtement détecté. (Aucun item ne porte de flag <code style={{ color: '#a1a1aa' }}>equipped</code> ni
        ne correspond aux patterns de vêtements connus — vérifiez le dump metadata d'un item pour ajuster la détection.)
      </div>
    )
  }

  // Groupe par slot pour pouvoir voir d'un coup d'œil quel slot a quoi.
  const bySlot = new Map()
  for (const x of outfit) {
    const key = x.slot || 'Slot inconnu'
    if (!bySlot.has(key)) bySlot.set(key, [])
    bySlot.get(key).push(x)
  }

  return (
    <div style={cardStyle({ marginBottom: 18, padding: '12px 14px' })}>
      <div style={{
        display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 10,
      }}>
        {Array.from(bySlot.entries()).map(([slot, list]) => (
          <div key={slot} style={{
            background: '#11151f', border: '1px solid rgba(255,255,255,0.05)',
            borderRadius: 8, padding: '10px 12px',
          }}>
            <div style={{
              fontSize: '0.66rem', color: 'var(--brand-primary, #e07b39)', textTransform: 'uppercase',
              letterSpacing: '0.06em', fontWeight: 700, marginBottom: 6,
              display: 'flex', alignItems: 'center', gap: 6,
            }}>
              {slot}
              {list.length > 1 && (
                <span style={{ color: '#71717a', fontWeight: 600 }}>×{list.length}</span>
              )}
            </div>
            {list.map(({ item, equipped }) => (
              <div key={item.id} style={{ marginBottom: 6 }}>
                <div style={{ fontSize: '0.84rem', color: '#e8e8e8', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{item.itemGameId}</span>
                  {equipped && <Pill color="#34d399" label="Équipé" />}
                </div>
                <div style={{ fontSize: '0.7rem', color: '#71717a', marginTop: 2 }}>
                  ×{item.count} · case [{item.line},{item.collum}]
                </div>
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  RENAME / DELETE CHARACTER — modals admin (cas des noms RP troll)
// ─────────────────────────────────────────────────────────────────────
function RenameCharacterModal({ character, onClose, onDone }) {
  const [firstName, setFirstName] = useState(character.firstName || '')
  const [lastName,  setLastName]  = useState(character.lastName  || '')
  const [busy,      setBusy]      = useState(false)
  const [feedback,  setFeedback]  = useState(null)
  const [trackPhase, setTrackPhase] = useState(null)

  const fn = firstName.trim()
  const ln = lastName.trim()
  const unchanged = fn === (character.firstName || '').trim() && ln === (character.lastName || '').trim()
  const invalid = fn.length === 0 || fn.length > 64 || ln.length === 0 || ln.length > 64

  async function handleSubmit(e) {
    e.preventDefault()
    if (invalid || unchanged) return
    setBusy(true); setFeedback(null); setTrackPhase('queued')
    try {
      const resp = await api.gameAdminUpdateCharacter(character.id, { firstName: fn, lastName: ln })
      // Le backend a aussi enqueué un ack côté gamemode — on poll son statut
      const { status } = await pollCommandStatus(resp?.commandId, {
        onProgress: ({ phase }) => setTrackPhase(phase),
      })
      if (TERMINAL_OK.has(status)) {
        setFeedback({ ok: true, msg: 'Personnage renommé et confirmé in-game.' })
      } else if (status === 'no-tracking') {
        setFeedback({ ok: true, msg: 'Personnage renommé en DB (gamemode non joignable pour confirmer).' })
      } else if (status === 'timeout') {
        setFeedback({ ok: true, msg: 'Renommé en DB. Confirmation gamemode en timeout — vérifier que le serveur tourne.' })
      } else {
        setFeedback({ ok: false, msg: `Renommé en DB mais gamemode a renvoyé : ${status}.` })
      }
      onDone?.()
    } catch (e) {
      setFeedback({ ok: false, msg: `Erreur : ${e.message}` })
    } finally { setBusy(false) }
  }

  return (
    <Modal title="Renommer le personnage" onClose={onClose}>
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div style={{ fontSize: '0.78rem', color: '#71717a' }}>
          ID : <span style={{ fontFamily: 'monospace', color: '#a1a1aa' }}>{character.id}</span>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
          <div>
            <label style={labelStyle}>Prénom *</label>
            <input
              value={firstName} onChange={e => setFirstName(e.target.value)}
              maxLength={64} disabled={busy}
              style={inputStyle({ width: '100%' })}
            />
          </div>
          <div>
            <label style={labelStyle}>Nom *</label>
            <input
              value={lastName} onChange={e => setLastName(e.target.value)}
              maxLength={64} disabled={busy}
              style={inputStyle({ width: '100%' })}
            />
          </div>
        </div>
        {trackPhase && busy && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: '#11151f', borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)' }}>
            <span style={{ fontSize: '0.78rem', color: '#a1a1aa' }}>Statut gamemode :</span>
            <StatusBadge phase={trackPhase} />
          </div>
        )}
        {feedback && (
          <div style={{
            padding: '7px 11px', borderRadius: 8,
            background: feedback.ok ? 'rgba(74,222,128,0.1)' : 'rgba(248,113,113,0.1)',
            border: `1px solid ${feedback.ok ? 'rgba(74,222,128,0.3)' : 'rgba(248,113,113,0.3)'}`,
            color: feedback.ok ? '#4ade80' : '#f87171', fontSize: '0.78rem',
          }}>{feedback.msg}</div>
        )}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 4 }}>
          <button type="button" onClick={onClose} disabled={busy} className="adm__btn adm__btn--ghost">
            Annuler
          </button>
          <button type="submit" disabled={busy || invalid || unchanged} style={{
            background: '#60a5fa18', border: '1px solid #60a5fa40', color: '#60a5fa',
            borderRadius: 7, padding: '7px 16px', fontSize: '0.82rem', fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit',
            opacity: (busy || invalid || unchanged) ? 0.5 : 1,
            display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>
            <Pencil size={13} /> {busy ? 'Envoi…' : 'Enregistrer'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

function DeleteCharacterModal({ character, onClose, onDone }) {
  const [reason,   setReason]   = useState('')
  const [confirm,  setConfirm]  = useState('')
  const [busy,     setBusy]     = useState(false)
  const [feedback, setFeedback] = useState(null)
  const [trackPhase, setTrackPhase] = useState(null)

  const fullName = `${character.firstName || ''} ${character.lastName || ''}`.trim()
  const ready = confirm.trim() === fullName

  async function handleSubmit(e) {
    e.preventDefault()
    if (!ready) return
    setBusy(true); setFeedback(null); setTrackPhase('queued')
    try {
      const resp = await api.gameAdminDeleteCharacter(character.id, { reason: reason.trim() || null })
      const { status } = await pollCommandStatus(resp?.commandId, {
        onProgress: ({ phase }) => setTrackPhase(phase),
      })
      if (TERMINAL_OK.has(status)) {
        setFeedback({ ok: true, msg: 'Personnage supprimé et confirmé in-game.' })
      } else if (status === 'no-tracking') {
        setFeedback({ ok: true, msg: 'Personnage supprimé en DB (gamemode non joignable pour confirmer).' })
      } else if (status === 'timeout') {
        setFeedback({ ok: true, msg: 'Supprimé en DB. Confirmation gamemode en timeout — vérifier que le serveur tourne.' })
      } else {
        setFeedback({ ok: false, msg: `Supprimé en DB mais gamemode a renvoyé : ${status}.` })
      }
      // On laisse 600ms le temps à l'utilisateur de voir le statut, puis ferme
      setTimeout(() => onDone?.(), 600)
    } catch (e) {
      setFeedback({ ok: false, msg: `Erreur : ${e.message}` })
    } finally { setBusy(false) }
  }

  return (
    <Modal title="Supprimer le personnage" onClose={onClose}>
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div style={{
          padding: '10px 12px', borderRadius: 8,
          background: 'rgba(248,113,113,0.08)', border: '1px solid rgba(248,113,113,0.25)',
          color: '#fca5a5', fontSize: '0.78rem', lineHeight: 1.45,
        }}>
          Action <b>définitive</b>. Le personnage <b>{fullName || character.id}</b> et toutes
          ses données (banque, inventaire, position) seront supprimés.
        </div>

        <div>
          <label style={labelStyle}>Raison (loggée)</label>
          <input
            value={reason} onChange={e => setReason(e.target.value)}
            placeholder="ex : nom RP troll / inapproprié"
            maxLength={500} disabled={busy}
            style={inputStyle({ width: '100%' })}
          />
        </div>

        <div>
          <label style={labelStyle}>
            Tape <span style={{ color: '#f87171', fontFamily: 'monospace' }}>{fullName}</span> pour confirmer
          </label>
          <input
            value={confirm} onChange={e => setConfirm(e.target.value)}
            disabled={busy}
            style={inputStyle({ width: '100%' })}
          />
        </div>

        {trackPhase && busy && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: '#11151f', borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)' }}>
            <span style={{ fontSize: '0.78rem', color: '#a1a1aa' }}>Statut gamemode :</span>
            <StatusBadge phase={trackPhase} />
          </div>
        )}
        {feedback && (
          <div style={{
            padding: '7px 11px', borderRadius: 8,
            background: feedback.ok ? 'rgba(74,222,128,0.1)' : 'rgba(248,113,113,0.1)',
            border: `1px solid ${feedback.ok ? 'rgba(74,222,128,0.3)' : 'rgba(248,113,113,0.3)'}`,
            color: feedback.ok ? '#4ade80' : '#f87171', fontSize: '0.78rem',
          }}>{feedback.msg}</div>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 4 }}>
          <button type="button" onClick={onClose} disabled={busy} className="adm__btn adm__btn--ghost">
            Annuler
          </button>
          <button type="submit" disabled={busy || !ready} style={{
            background: '#f8717118', border: '1px solid #f8717140', color: '#f87171',
            borderRadius: 7, padding: '7px 16px', fontSize: '0.82rem', fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit',
            opacity: (busy || !ready) ? 0.5 : 1,
            display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>
            <Trash2 size={13} /> {busy ? 'Suppression…' : 'Supprimer'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  HISTORIQUE INVENTAIRE — transferts d'items pour un character donné
// ─────────────────────────────────────────────────────────────────────
const INV_ACTION_META = {
  add_persist:    { label: 'Save+',    color: '#4ade80' },
  remove_persist: { label: 'Save−',    color: '#fbbf24' },
  save_clear:     { label: 'Clear',    color: '#71717a' },
  move:           { label: 'Move',     color: '#60a5fa' },
  drop:           { label: 'Drop',     color: '#fb923c' },
  pickup:         { label: 'Pickup',   color: '#a78bfa' },
  use:            { label: 'Use',      color: '#f472b6' },
}

function CharacterInventoryHistory({ characterId, ownerSteamId }) {
  // On charge en priorité les logs filtrés par characterId (renseigné par le gamemode
  // quand le perso actif est connu) ; en fallback on filtre par steamId du propriétaire.
  const [data,    setData]    = useState({ logs: [], total: 0 })
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [filterItem, setFilterItem] = useState('')
  const [filterAction, setFilterAction] = useState('')
  const [showAll, setShowAll] = useState(false)

  const load = useCallback(async (opts) => {
    const silent = !!(opts && opts.silent === true)
    if (!silent) setLoading(true)
    setError('')
    try {
      // Tente d'abord par characterId (plus précis), sinon par steamId
      let resp = await api.gameAdminInventoryLogs({
        characterId,
        itemGameId: filterItem,
        action: filterAction,
        pageSize: showAll ? 500 : 50,
      })
      if ((resp.total ?? 0) === 0 && ownerSteamId) {
        resp = await api.gameAdminInventoryLogs({
          steamId: ownerSteamId,
          itemGameId: filterItem,
          action: filterAction,
          pageSize: showAll ? 500 : 50,
        })
      }
      setData(resp)
    } catch (e) { setError(e.message) }
    finally { if (!silent) setLoading(false) }
  }, [characterId, ownerSteamId, filterItem, filterAction, showAll])

  useEffect(() => { load() }, [load])

  // Auto-refresh silencieux toutes les 3s, calé sur le même rythme que
  // l'inventaire du parent. Pause si l'onglet n'est pas visible.
  useEffect(() => {
    const intv = setInterval(() => {
      if (document.visibilityState === 'visible') load({ silent: true })
    }, 3000)
    return () => clearInterval(intv)
  }, [load])

  return (
    <>
      <SectionTitle icon={<Activity size={14} />} title={`Historique inventaire ${data.total ? `(${data.total})` : ''}`} />
      <div style={{ display: 'flex', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
        <input
          value={filterItem} onChange={e => setFilterItem(e.target.value)}
          placeholder="Filtre item (ResourceName)…"
          style={inputStyle({ flex: 1, minWidth: 200 })}
        />
        <select value={filterAction} onChange={e => setFilterAction(e.target.value)} style={selectStyle()}>
          <option value="">Toutes actions</option>
          {Object.entries(INV_ACTION_META).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
        </select>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.78rem', color: '#a1a1aa', cursor: 'pointer' }}>
          <input type="checkbox" checked={showAll} onChange={e => setShowAll(e.target.checked)} />
          500 derniers
        </label>
        <button onClick={load} style={iconBtn()} title="Rafraîchir"><RefreshCcw size={14} /></button>
      </div>

      {loading ? <Loader /> : error ? <ErrorBanner msg={error} onRetry={load} /> :
       data.logs.length === 0 ? <Empty msg="Aucun transfert enregistré pour ce personnage." /> : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginBottom: 18, maxHeight: 480, overflow: 'auto', border: '1px solid rgba(255,255,255,0.05)', borderRadius: 8, padding: 4 }}>
          {data.logs.map(l => {
            const meta = INV_ACTION_META[l.action] || { label: l.action, color: '#a1a1aa' }
            return (
              <div key={l.id} style={cardStyle({ padding: '7px 10px' })}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <span style={{ minWidth: 70, fontFamily: 'monospace', fontSize: '0.65rem', color: '#52525b' }}>
                    {fmtDateTime(l.at)}
                  </span>
                  <span style={{
                    padding: '1px 7px', borderRadius: 4, fontSize: '0.65rem', fontWeight: 700,
                    background: meta.color + '22', color: meta.color, minWidth: 70, textAlign: 'center',
                  }}>
                    {meta.label}
                  </span>
                  <span style={{ flex: 1, fontSize: '0.78rem', color: '#e8e8e8', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {l.itemGameId || <span style={{ color: '#52525b' }}>—</span>}
                    {l.count > 1 && <span style={{ color: '#71717a', marginLeft: 6 }}>×{l.count}</span>}
                  </span>
                  <span style={{ fontSize: '0.65rem', color: '#71717a', fontFamily: 'monospace' }}>
                    {l.sourceType || '?'} → {l.targetType || '?'}
                  </span>
                </div>
                {l.metadataJson && (
                  <div style={{ marginTop: 3, fontSize: '0.65rem', color: '#52525b', fontFamily: 'monospace', paddingLeft: 80 }}>
                    {l.metadataJson}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  MODALS — détail d'un item / d'une transaction
// ─────────────────────────────────────────────────────────────────────
function Modal({ title, onClose, children }) {
  return (
    <div onClick={onClose} style={{
      position: 'fixed', inset: 0, zIndex: 9999, background: 'rgba(0,0,0,0.7)',
      display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 20,
    }}>
      <div onClick={e => e.stopPropagation()} style={{
        background: '#161a26', border: '1px solid rgba(255,255,255,0.1)',
        borderRadius: 12, padding: 22, width: 540, maxWidth: '100%',
        maxHeight: '90vh', overflow: 'auto',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', marginBottom: 16 }}>
          <span style={{ flex: 1, fontWeight: 700, color: '#e8e8e8', fontSize: '1rem' }}>{title}</span>
          <button onClick={onClose} style={iconBtn()}><X size={14} /></button>
        </div>
        {children}
      </div>
    </div>
  )
}

function DetailRow({ label, value, mono, children }) {
  return (
    <div style={{ display: 'flex', padding: '8px 0', borderBottom: '1px solid rgba(255,255,255,0.05)', gap: 12 }}>
      <span style={{ minWidth: 130, fontSize: '0.75rem', color: '#71717a', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>
        {label}
      </span>
      <span style={{ flex: 1, fontSize: '0.85rem', color: '#e8e8e8', fontFamily: mono ? 'monospace' : 'inherit', wordBreak: 'break-all' }}>
        {children ?? (value === null || value === undefined || value === '' ? <span style={{ color: '#52525b' }}>—</span> : String(value))}
      </span>
    </div>
  )
}

function ItemModal({ item, owner, onClose, onChanged }) {
  const meta = item.metadata || {}
  const metaEntries = Object.entries(meta)

  const [busy,     setBusy]     = useState(null)
  const [feedback, setFeedback] = useState(null)
  const [newCount, setNewCount] = useState(item.count)

  async function handleRemove() {
    if (!confirm(`Supprimer cet item de l'inventaire ?\n${item.itemGameId} ×${item.count}`)) return
    setBusy('remove'); setFeedback(null)
    try {
      await api.gameAdminInventoryDelete(item.id, owner?.id)
      setFeedback({ ok: true, msg: 'Item supprimé.' })
      onChanged?.()
    } catch (e) {
      setFeedback({ ok: false, msg: `Erreur : ${e.message}` })
    } finally { setBusy(null) }
  }

  async function handleSetCount() {
    const n = Number(newCount)
    if (!Number.isFinite(n) || n < 0) { setFeedback({ ok: false, msg: 'Quantité invalide.' }); return }
    if (n === item.count) return
    setBusy('count'); setFeedback(null)
    try {
      await api.gameAdminInventoryModify(item.id, { count: n }, owner?.id)
      setFeedback({ ok: true, msg: `Quantité mise à jour → ${n}.` })
      onChanged?.()
    } catch (e) {
      setFeedback({ ok: false, msg: `Erreur : ${e.message}` })
    } finally { setBusy(null) }
  }

  return (
    <Modal title={`Item — ${item.itemGameId}`} onClose={onClose}>
      <DetailRow label="Item ID" value={item.itemGameId} mono />
      <DetailRow label="Quantité" value={`×${item.count}`} />
      <DetailRow label="Masse" value={`${item.mass} kg`} />
      <DetailRow label="Case grille" value={`ligne ${item.line}, colonne ${item.collum}`} />
      {owner && (
        <DetailRow label="Propriétaire" value={null}>
          {owner.firstName} {owner.lastName}
          <div style={{ fontSize: '0.7rem', color: '#71717a', marginTop: 2, fontFamily: 'monospace' }}>
            Character : {owner.id}
          </div>
          {owner.ownerId && (
            <div style={{ fontSize: '0.7rem', color: '#71717a', marginTop: 2, fontFamily: 'monospace' }}>
              SteamID : {owner.ownerId}
            </div>
          )}
        </DetailRow>
      )}
      <DetailRow label="Internal ID" value={item.id} mono />
      <DetailRow label="Inventory ID" value={item.inventoryId} mono />
      {metaEntries.length > 0 && (
        <div style={{ marginTop: 14 }}>
          <div style={{ fontSize: '0.75rem', color: '#71717a', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600, marginBottom: 6 }}>
            Metadata ({metaEntries.length})
          </div>
          <div style={{ background: '#11151f', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 8, padding: 10, fontFamily: 'monospace', fontSize: '0.76rem', color: '#a1a1aa' }}>
            {metaEntries.map(([k, v]) => (
              <div key={k} style={{ display: 'flex', gap: 10, padding: '3px 0' }}>
                <span style={{ color: '#60a5fa', minWidth: 120 }}>{k}</span>
                <span style={{ flex: 1, wordBreak: 'break-all' }}>{String(v)}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Actions admin */}
      <div style={{ marginTop: 16, paddingTop: 14, borderTop: '1px solid rgba(255,255,255,0.07)' }}>
        <div style={{ fontSize: '0.72rem', color: '#a1a1aa', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 700, marginBottom: 10 }}>
          Actions admin
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
          <label style={{ fontSize: '0.78rem', color: '#a1a1aa' }}>Quantité :</label>
          <input
            type="number" min={0} value={newCount}
            onChange={e => setNewCount(e.target.value)}
            disabled={!!busy}
            style={inputStyle({ width: 90, padding: '6px 10px' })}
          />
          <button onClick={handleSetCount} disabled={!!busy || Number(newCount) === item.count} style={{
            background: '#60a5fa18', border: '1px solid #60a5fa40', color: '#60a5fa',
            borderRadius: 7, padding: '6px 12px', fontSize: '0.78rem', fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit', opacity: (busy || Number(newCount) === item.count) ? 0.5 : 1,
          }}>{busy === 'count' ? 'Envoi…' : 'Modifier'}</button>

          <span style={{ flex: 1 }} />

          <button onClick={handleRemove} disabled={!!busy} style={{
            background: '#f8717118', border: '1px solid #f8717140', color: '#f87171',
            borderRadius: 7, padding: '6px 12px', fontSize: '0.78rem', fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit', opacity: busy ? 0.5 : 1,
            display: 'inline-flex', alignItems: 'center', gap: 5,
          }}>
            <Trash2 size={12} /> {busy === 'remove' ? 'Envoi…' : 'Supprimer'}
          </button>
        </div>

        {feedback && (
          <div style={{
            padding: '7px 11px', borderRadius: 8, marginTop: 8,
            background: feedback.ok ? 'rgba(74,222,128,0.1)' : 'rgba(248,113,113,0.1)',
            border: `1px solid ${feedback.ok ? 'rgba(74,222,128,0.3)' : 'rgba(248,113,113,0.3)'}`,
            color: feedback.ok ? '#4ade80' : '#f87171', fontSize: '0.78rem',
          }}>{feedback.msg}</div>
        )}
      </div>
    </Modal>
  )
}

function GiveItemModal({ characterId, onClose, onDone }) {
  const [form, setForm] = useState({
    itemGameId: '', count: 1, mass: 1.0, line: 0, collum: 0, metadata: '',
  })
  const [busy,     setBusy]     = useState(false)
  const [feedback, setFeedback] = useState(null)

  function set(k, v) { setForm(f => ({ ...f, [k]: v })) }

  async function handleSubmit(e) {
    e.preventDefault()
    if (!form.itemGameId.trim()) { setFeedback({ ok: false, msg: 'itemGameId requis.' }); return }
    let metadata = undefined
    if (form.metadata.trim()) {
      try { metadata = JSON.parse(form.metadata) }
      catch { setFeedback({ ok: false, msg: 'Metadata : JSON invalide.' }); return }
    }
    setBusy(true); setFeedback(null)
    try {
      await api.gameAdminInventoryGive({
        characterId,
        itemGameId: form.itemGameId.trim(),
        count: Number(form.count),
        mass: Number(form.mass),
        line: Number(form.line),
        collum: Number(form.collum),
        metadata,
      })
      setFeedback({ ok: true, msg: `Item "${form.itemGameId}" donné avec succès.` })
      onDone?.()
    } catch (e) {
      setFeedback({ ok: false, msg: `Erreur : ${e.message}` })
    } finally { setBusy(false) }
  }

  return (
    <Modal title="Donner un item" onClose={onClose}>
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div>
          <label style={labelStyle}>Item Game ID *</label>
          <input
            value={form.itemGameId} onChange={e => set('itemGameId', e.target.value)}
            placeholder="ex : weapon_pistol"
            style={inputStyle({ width: '100%' })} disabled={busy}
          />
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
          <div>
            <label style={labelStyle}>Quantité</label>
            <input type="number" min={1} value={form.count} onChange={e => set('count', e.target.value)}
              style={inputStyle({ width: '100%' })} disabled={busy} />
          </div>
          <div>
            <label style={labelStyle}>Masse (kg)</label>
            <input type="number" min={0} step={0.1} value={form.mass} onChange={e => set('mass', e.target.value)}
              style={inputStyle({ width: '100%' })} disabled={busy} />
          </div>
          <div>
            <label style={labelStyle}>Ligne (grille)</label>
            <input type="number" min={0} value={form.line} onChange={e => set('line', e.target.value)}
              style={inputStyle({ width: '100%' })} disabled={busy} />
          </div>
          <div>
            <label style={labelStyle}>Colonne (grille)</label>
            <input type="number" min={0} value={form.collum} onChange={e => set('collum', e.target.value)}
              style={inputStyle({ width: '100%' })} disabled={busy} />
          </div>
        </div>

        <div>
          <label style={labelStyle}>Metadata (JSON optionnel)</label>
          <textarea
            value={form.metadata} onChange={e => set('metadata', e.target.value)}
            placeholder={'{ "ammo": "12" }'}
            rows={3}
            style={{ ...inputStyle({ width: '100%' }), resize: 'vertical', fontFamily: 'monospace', fontSize: '0.78rem' }}
            disabled={busy}
          />
        </div>

        {feedback && (
          <div style={{
            padding: '7px 11px', borderRadius: 8,
            background: feedback.ok ? 'rgba(74,222,128,0.1)' : 'rgba(248,113,113,0.1)',
            border: `1px solid ${feedback.ok ? 'rgba(74,222,128,0.3)' : 'rgba(248,113,113,0.3)'}`,
            color: feedback.ok ? '#4ade80' : '#f87171', fontSize: '0.78rem',
          }}>{feedback.msg}</div>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 4 }}>
          <button type="button" onClick={onClose} disabled={busy} className="adm__btn adm__btn--ghost">
            Annuler
          </button>
          <button type="submit" disabled={busy || !form.itemGameId.trim()} style={{
            background: '#a78bfa18', border: '1px solid #a78bfa40', color: '#a78bfa',
            borderRadius: 7, padding: '7px 16px', fontSize: '0.82rem', fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit',
            opacity: (busy || !form.itemGameId.trim()) ? 0.5 : 1,
            display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>
            <Gift size={13} /> {busy ? 'Envoi…' : 'Donner'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

function TxModal({ tx, onClose }) {
  const isCredit = tx.toAccountId === tx._accountId
  return (
    <Modal title={`Transaction — ${txTypeLabel(tx.type)}`} onClose={onClose}>
      <div style={{
        textAlign: 'center', padding: '14px 0', marginBottom: 8,
        background: isCredit ? 'rgba(74,222,128,0.08)' : 'rgba(248,113,113,0.08)',
        border: `1px solid ${isCredit ? 'rgba(74,222,128,0.2)' : 'rgba(248,113,113,0.2)'}`,
        borderRadius: 10,
      }}>
        <div style={{ color: isCredit ? '#4ade80' : '#f87171', fontSize: '1.6rem', fontWeight: 700 }}>
          {isCredit ? '+' : '−'}{formatMoney(tx.amount)}
        </div>
        <div style={{ color: '#71717a', fontSize: '0.75rem', marginTop: 2 }}>
          {isCredit ? 'Reçu' : 'Envoyé'}
        </div>
      </div>
      <DetailRow label="Type" value={txTypeLabel(tx.type)} />
      <DetailRow label="Statut" value={txStatusLabel(tx.status)} />
      <DetailRow label="Montant" value={formatMoney(tx.amount)} />
      <DetailRow label="Commentaire" value={tx.comment} />
      <DetailRow label="Date" value={new Date(tx.createdAt).toLocaleString('fr-FR')} />
      <DetailRow
        label="Compte source"
        value={tx.fromAccountName
          ? `${tx.fromAccountName}${tx.fromAccountOwner ? ` — ${tx.fromAccountOwner}` : ''}`
          : tx.fromAccountId}
        mono={!tx.fromAccountName}
      />
      <DetailRow
        label="Compte cible"
        value={tx.toAccountName
          ? `${tx.toAccountName}${tx.toAccountOwner ? ` — ${tx.toAccountOwner}` : ''}`
          : tx.toAccountId}
        mono={!tx.toAccountName}
      />
      <DetailRow
        label="Initiateur"
        value={tx.initiatorName || tx.initiatorCharacterId}
        mono={!tx.initiatorName}
      />
      <DetailRow label="ATM" value={tx.atmId} mono />
      <DetailRow label="Transaction ID" value={tx.id} mono />
    </Modal>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  GLOBAL LIST VIEWS — tous les items / toutes les transactions du serveur
// ─────────────────────────────────────────────────────────────────────
function AllItemsView({ onBack }) {
  const [data,    setData]    = useState(null)
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [search,  setSearch]  = useState('')
  const [openItem, setOpenItem] = useState(null)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setData(await api.gameAdminAllItems(1, 500)) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  const items = data?.items || []
  const filtered = items.filter(i => {
    if (!search) return true
    const q = search.trim().toLowerCase()
    return (i.itemGameId || '').toLowerCase().includes(q)
        || (i.characterName || '').toLowerCase().includes(q)
        || (i.ownerSteamId || '').includes(q)
  })

  return (
    <>
      <BackBar onBack={onBack} label={`Tous les items — ${data?.total ?? '…'}`} onRefresh={load} />
      <div style={{ display: 'flex', gap: 8, marginBottom: 14 }}>
        <div style={{
          flex: 1, display: 'flex', alignItems: 'center', gap: 8,
          background: '#161a26', border: '1px solid rgba(255,255,255,0.08)',
          borderRadius: 8, padding: '7px 12px',
        }}>
          <Search size={14} style={{ color: '#52525b' }} />
          <input
            value={search} onChange={e => setSearch(e.target.value)}
            placeholder="Rechercher par item, joueur ou SteamID…"
            style={{ flex: 1, background: 'none', border: 'none', outline: 'none', color: '#e8e8e8', fontSize: '0.85rem', fontFamily: 'inherit' }}
          />
          {search && <button onClick={() => setSearch('')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#52525b' }}><X size={14} /></button>}
        </div>
      </div>
      {loading ? <Loader /> : error ? <ErrorBanner msg={error} onRetry={load} /> : filtered.length === 0 ? (
        <Empty msg={items.length === 0 ? 'Aucun item en base.' : 'Aucun résultat.'} />
      ) : (
        <div style={{
          display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 8,
        }}>
          {filtered.map(it => (
            <button key={it.id} onClick={() => setOpenItem(it)} style={{
              ...cardStyle(), cursor: 'pointer', textAlign: 'left', width: '100%', fontFamily: 'inherit',
              transition: 'border-color 0.15s',
            }} onMouseEnter={e => e.currentTarget.style.borderColor = 'rgba(244,114,182,0.35)'}
               onMouseLeave={e => e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)'}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <span style={{
                  width: 30, height: 30, borderRadius: 6, background: '#2a2f3e',
                  display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#f472b6',
                }}>
                  <Package size={14} />
                </span>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: '0.82rem', fontWeight: 600, color: '#e8e8e8', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {it.itemGameId} <span style={{ color: '#71717a', fontWeight: 400 }}>×{it.count}</span>
                  </div>
                  <div style={{ fontSize: '0.7rem', color: '#71717a', marginTop: 2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {it.characterName || <span style={{ color: '#52525b' }}>Propriétaire inconnu</span>}
                  </div>
                  {it.ownerSteamId && (
                    <div style={{ fontSize: '0.66rem', color: '#52525b', marginTop: 2, fontFamily: 'monospace' }}>{it.ownerSteamId}</div>
                  )}
                </div>
                <ChevronRight size={14} style={{ color: '#52525b' }} />
              </div>
            </button>
          ))}
        </div>
      )}
      {openItem && (
        <ItemModal
          item={openItem}
          owner={openItem.characterId ? {
            id: openItem.characterId,
            ownerId: openItem.ownerSteamId,
            firstName: openItem.characterName?.split(' ')[0],
            lastName:  openItem.characterName?.split(' ').slice(1).join(' '),
          } : null}
          onClose={() => setOpenItem(null)}
          onChanged={() => { load(); setOpenItem(null) }}
        />
      )}
    </>
  )
}

function AllTxView({ onBack }) {
  const [data,    setData]    = useState(null)
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [search,  setSearch]  = useState('')
  const [openTx,  setOpenTx]  = useState(null)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setData(await api.gameAdminAllTx(1, 500)) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  const txs = data?.transactions || []
  const filtered = txs.filter(t => {
    if (!search) return true
    const q = search.trim().toLowerCase()
    return (t.comment || '').toLowerCase().includes(q)
        || (t.fromAccountId || '').includes(q)
        || (t.toAccountId || '').includes(q)
        || (t.initiatorCharacterId || '').includes(q)
        || (t.initiatorName || '').toLowerCase().includes(q)
        || (t.fromAccountOwner || '').toLowerCase().includes(q)
        || (t.toAccountOwner || '').toLowerCase().includes(q)
  })

  return (
    <>
      <BackBar onBack={onBack} label={`Toutes les transactions — ${data?.total ?? '…'}`} onRefresh={load} />
      <div style={{ display: 'flex', gap: 8, marginBottom: 14 }}>
        <div style={{
          flex: 1, display: 'flex', alignItems: 'center', gap: 8,
          background: '#161a26', border: '1px solid rgba(255,255,255,0.08)',
          borderRadius: 8, padding: '7px 12px',
        }}>
          <Search size={14} style={{ color: '#52525b' }} />
          <input
            value={search} onChange={e => setSearch(e.target.value)}
            placeholder="Rechercher par commentaire, compte ou initiateur…"
            style={{ flex: 1, background: 'none', border: 'none', outline: 'none', color: '#e8e8e8', fontSize: '0.85rem', fontFamily: 'inherit' }}
          />
          {search && <button onClick={() => setSearch('')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#52525b' }}><X size={14} /></button>}
        </div>
      </div>
      {loading ? <Loader /> : error ? <ErrorBanner msg={error} onRetry={load} /> : filtered.length === 0 ? (
        <Empty msg={txs.length === 0 ? 'Aucune transaction.' : 'Aucun résultat.'} />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          {filtered.map(t => (
            <button key={t.id} onClick={() => setOpenTx(t)} style={{
              ...cardStyle({ padding: '8px 14px' }), cursor: 'pointer', textAlign: 'left',
              width: '100%', fontFamily: 'inherit',
              display: 'flex', alignItems: 'center', gap: 10,
              transition: 'border-color 0.15s',
            }} onMouseEnter={e => e.currentTarget.style.borderColor = 'rgba(251,191,36,0.35)'}
               onMouseLeave={e => e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)'}>
              <span style={{ fontSize: '0.65rem', padding: '3px 8px', borderRadius: 4, background: '#2a2f3e', color: '#fbbf24', minWidth: 80, textAlign: 'center', fontWeight: 700 }}>
                {txTypeLabel(t.type)}
              </span>
              <span style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 2, overflow: 'hidden' }}>
                <span style={{ fontSize: '0.8rem', color: '#e8e8e8', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {t.initiatorName || <span style={{ color: '#52525b' }}>Initiateur inconnu</span>}
                  {t.toAccountOwner && t.toAccountOwner !== t.initiatorName && (
                    <span style={{ color: '#52525b' }}> → {t.toAccountOwner}</span>
                  )}
                </span>
                <span style={{ fontSize: '0.7rem', color: '#71717a', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {t.comment || <span style={{ color: '#52525b' }}>—</span>}
                </span>
              </span>
              <span style={{ fontSize: '0.85rem', fontWeight: 700, color: '#34d399' }}>
                {formatMoney(t.amount)}
              </span>
              <span style={{ fontSize: '0.7rem', color: '#52525b', minWidth: 95, textAlign: 'right' }}>
                {new Date(t.createdAt).toLocaleDateString('fr-FR')} {new Date(t.createdAt).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}
              </span>
              <ChevronRight size={13} style={{ color: '#52525b' }} />
            </button>
          ))}
        </div>
      )}
      {openTx && <TxModal tx={openTx} onClose={() => setOpenTx(null)} />}
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  MAP — scatter plot des positions joueurs (plan XY vu du dessus)
// ─────────────────────────────────────────────────────────────────────
const MAP_BG_URL = 'https://service.openframework.fr/uploads/img_1776792966049_ng2cr3qf.webp'

function MapTab() {
  const [positions, setPositions] = useState([])
  const [loading,   setLoading]   = useState(true)
  const [error,     setError]     = useState('')
  const [hover,     setHover]     = useState(null)
  const [onlyActive,setOnlyActive]= useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setPositions(await api.gameAdminPositions()) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  const filtered = onlyActive ? positions.filter(p => p.isSelected) : positions

  // Bornes
  const bounds = filtered.reduce((b, p) => ({
    minX: Math.min(b.minX, p.x), maxX: Math.max(b.maxX, p.x),
    minY: Math.min(b.minY, p.y), maxY: Math.max(b.maxY, p.y),
  }), { minX: Infinity, maxX: -Infinity, minY: Infinity, maxY: -Infinity })

  const pad = 40
  const width  = 800
  const height = 560
  const rangeX = Math.max(bounds.maxX - bounds.minX, 1)
  const rangeY = Math.max(bounds.maxY - bounds.minY, 1)
  const scale  = Math.min((width - 2 * pad) / rangeX, (height - 2 * pad) / rangeY)

  const project = (p) => ({
    px: pad + (p.x - bounds.minX) * scale,
    py: height - pad - (p.y - bounds.minY) * scale, // Y inversé pour un plan vu du dessus
  })

  if (loading) return <Loader />
  if (error)   return <ErrorBanner msg={error} onRetry={load} />

  return (
    <>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 14 }}>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', color: '#a1a1aa', fontSize: '0.82rem' }}>
          <input type="checkbox" checked={onlyActive} onChange={e => setOnlyActive(e.target.checked)} />
          Characters actifs uniquement
        </label>
        <span style={{ flex: 1, fontSize: '0.78rem', color: '#71717a' }}>
          {filtered.length} position{filtered.length > 1 ? 's' : ''} affichée{filtered.length > 1 ? 's' : ''}
        </span>
        <button onClick={load} style={iconBtn()}><RefreshCcw size={14} /></button>
      </div>

      {filtered.length === 0 ? (
        <Empty msg="Aucune position enregistrée." />
      ) : (
        <div style={{
          position: 'relative', background: '#0a0e18',
          border: '1px solid rgba(255,255,255,0.08)', borderRadius: 12,
          padding: 0, overflow: 'hidden',
        }}>
          <svg viewBox={`0 0 ${width} ${height}`} style={{ width: '100%', height: 'auto', display: 'block' }}>
            {/* Grille */}
            <defs>
              <pattern id="smallgrid" width="20" height="20" patternUnits="userSpaceOnUse">
                <path d="M 20 0 L 0 0 0 20" fill="none" stroke="rgba(255,255,255,0.03)" strokeWidth="1" />
              </pattern>
              <pattern id="biggrid" width="100" height="100" patternUnits="userSpaceOnUse">
                <rect width="100" height="100" fill="url(#smallgrid)" />
                <path d="M 100 0 L 0 0 0 100" fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="1" />
              </pattern>
            </defs>
            <>
              <image href={MAP_BG_URL} x={pad} y={pad} width={width - 2 * pad} height={height - 2 * pad} preserveAspectRatio="xMidYMid slice" opacity={0.7} />
              <rect width={width} height={height} fill="url(#biggrid)" opacity={0.4} />
            </>

            {/* Axes de référence */}
            <line x1={pad} y1={height - pad} x2={width - pad} y2={height - pad} stroke="rgba(255,255,255,0.15)" strokeWidth="1" />
            <line x1={pad} y1={pad}          x2={pad}         y2={height - pad} stroke="rgba(255,255,255,0.15)" strokeWidth="1" />
            <text x={width - pad} y={height - pad - 6} fill="#52525b" fontSize="10" textAnchor="end">X →</text>
            <text x={pad + 6}     y={pad + 12}         fill="#52525b" fontSize="10">↑ Y</text>

            {/* Points */}
            {filtered.map(p => {
              const { px, py } = project(p)
              const color = p.isSelected ? 'var(--brand-primary, #e07b39)' : '#60a5fa'
              return (
                <g key={p.characterId} onMouseEnter={() => setHover(p)} onMouseLeave={() => setHover(null)} style={{ cursor: 'pointer' }}>
                  {p.isSelected && (
                    <circle cx={px} cy={py} r={11} fill={color} opacity={0.15}>
                      <animate attributeName="r" values="11;17;11" dur="2s" repeatCount="indefinite" />
                      <animate attributeName="opacity" values="0.15;0;0.15" dur="2s" repeatCount="indefinite" />
                    </circle>
                  )}
                  <circle cx={px} cy={py} r={hover?.characterId === p.characterId ? 8 : 6}
                          fill={color} stroke="#0a0e18" strokeWidth={2}
                          style={{ transition: 'r 0.15s' }} />
                </g>
              )
            })}
          </svg>

          {/* Tooltip */}
          {hover && (
            <div style={{
              position: 'absolute', top: 12, left: 12,
              background: 'rgba(26,26,26,0.95)', border: '1px solid rgba(60, 173, 217,0.3)',
              borderRadius: 8, padding: '10px 14px', pointerEvents: 'none',
              boxShadow: '0 4px 12px rgba(0,0,0,0.4)',
            }}>
              <div style={{ fontWeight: 700, color: '#e8e8e8', fontSize: '0.85rem' }}>
                {hover.firstName} {hover.lastName}
                {hover.isSelected && <span style={{ marginLeft: 8, fontSize: '0.6rem', background: 'var(--brand-primary, #e07b39)22', color: 'var(--brand-primary, #e07b39)', padding: '1px 6px', borderRadius: 99, fontWeight: 700 }}>ACTIF</span>}
              </div>
              <div style={{ fontSize: '0.7rem', color: '#71717a', marginTop: 3, fontFamily: 'monospace' }}>
                SteamID : {hover.ownerId}
              </div>
              <div style={{ fontSize: '0.72rem', color: '#a1a1aa', marginTop: 5, fontFamily: 'monospace' }}>
                X: {hover.x.toFixed(1)} · Y: {hover.y.toFixed(1)} · Z: {hover.z.toFixed(1)}
              </div>
            </div>
          )}

          {/* Légende */}
          <div style={{
            position: 'absolute', bottom: 12, right: 12,
            background: 'rgba(26,26,26,0.9)', border: '1px solid rgba(255,255,255,0.08)',
            borderRadius: 8, padding: '8px 12px', display: 'flex', flexDirection: 'column', gap: 6,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.72rem', color: '#a1a1aa' }}>
              <span style={{ width: 10, height: 10, borderRadius: '50%', background: 'var(--brand-primary, #e07b39)' }} />
              Actif
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.72rem', color: '#a1a1aa' }}>
              <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#60a5fa' }} />
              Inactif
            </div>
          </div>
        </div>
      )}
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  BANS
// ─────────────────────────────────────────────────────────────────────
function BansTab() {
  const [bans,    setBans]    = useState([])
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [form,    setForm]    = useState({ UserSteamId: '', Reason: '' })
  const [busy,    setBusy]    = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setBans(await api.gameAdminBans()) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  async function add(e) {
    e.preventDefault()
    if (!form.UserSteamId.trim()) return
    setBusy(true); setError('')
    try {
      await api.gameAdminBan({ UserSteamId: form.UserSteamId.trim(), Reason: form.Reason })
      setForm({ UserSteamId: '', Reason: '' })
      load()
    } catch (e) { setError(e.message) }
    finally { setBusy(false) }
  }

  async function remove(steamId) {
    if (!confirm(`Débannir ${steamId} ?`)) return
    try { await api.gameAdminUnban(steamId, { Reason: '' }); load() }
    catch (e) { setError(e.message) }
  }

  return (
    <>
      <form onSubmit={add} style={formRowStyle()}>
        <input
          value={form.UserSteamId}
          onChange={e => setForm(f => ({ ...f, UserSteamId: e.target.value }))}
          placeholder="SteamID64"
          required
          style={inputStyle({ minWidth: 180 })}
        />
        <input
          value={form.Reason}
          onChange={e => setForm(f => ({ ...f, Reason: e.target.value }))}
          placeholder="Raison"
          style={{ ...inputStyle(), flex: 1 }}
        />
        <button type="submit" disabled={busy} style={primaryBtn()}>
          <Plus size={13} /> Bannir
        </button>
      </form>
      {error && <ErrorBanner msg={error} onRetry={load} />}

      {loading ? <Loader /> : bans.length === 0 ? (
        <Empty msg="Aucun banni." />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {bans.map(b => (
            <div key={b.id} style={cardStyle()}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <ShieldAlert size={16} style={{ color: '#f87171' }} />
                <div style={{ flex: 1 }}>
                  <div style={{ fontFamily: 'monospace', fontWeight: 600, color: '#e8e8e8', fontSize: '0.85rem' }}>{b.steamId}</div>
                  <div style={{ fontSize: '0.72rem', color: '#71717a', marginTop: 2 }}>
                    {b.reason || '(sans raison)'} — par {b.fromAdminSteamId || 'inconnu'}
                  </div>
                </div>
                <button onClick={() => remove(b.steamId)} style={dangerBtn()} title="Débannir">
                  <Trash2 size={13} />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  WHITELIST
// ─────────────────────────────────────────────────────────────────────
function WhitelistTab() {
  const [list,    setList]    = useState([])
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [steamId, setSteamId] = useState('')
  const [busy,    setBusy]    = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setList(await api.gameAdminWhitelist()) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  async function add(e) {
    e.preventDefault()
    if (!steamId.trim()) return
    setBusy(true); setError('')
    try {
      await api.gameAdminAddWhitelist({ UserSteamId: steamId.trim() })
      setSteamId('')
      load()
    } catch (e) { setError(e.message) }
    finally { setBusy(false) }
  }

  async function remove(sid) {
    if (!confirm(`Retirer ${sid} de la whitelist ?`)) return
    try { await api.gameAdminRemoveWhitelist(sid); load() }
    catch (e) { setError(e.message) }
  }

  return (
    <>
      <form onSubmit={add} style={formRowStyle()}>
        <input
          value={steamId}
          onChange={e => setSteamId(e.target.value)}
          placeholder="SteamID64"
          required
          style={{ ...inputStyle(), flex: 1 }}
        />
        <button type="submit" disabled={busy} style={primaryBtn()}>
          <Plus size={13} /> Ajouter
        </button>
      </form>
      {error && <ErrorBanner msg={error} onRetry={load} />}

      {loading ? <Loader /> : list.length === 0 ? (
        <Empty msg="Aucun joueur en whitelist." />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {list.map(w => (
            <div key={w.id} style={cardStyle()}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <ShieldCheck size={16} style={{ color: '#4ade80' }} />
                <div style={{ flex: 1 }}>
                  <div style={{ fontFamily: 'monospace', fontWeight: 600, color: '#e8e8e8', fontSize: '0.85rem' }}>{w.steamId}</div>
                  <div style={{ fontSize: '0.72rem', color: '#71717a', marginTop: 2 }}>
                    Ajouté par {w.fromAdminSteamId || 'inconnu'}
                  </div>
                </div>
                <button onClick={() => remove(w.steamId)} style={dangerBtn()} title="Retirer">
                  <Trash2 size={13} />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  WARNS (lecture seule pour l'instant)
// ─────────────────────────────────────────────────────────────────────
function WarnsTab() {
  const [list,    setList]    = useState([])
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setList(await api.gameAdminWarns()) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  if (loading) return <Loader />
  if (error)   return <ErrorBanner msg={error} onRetry={load} />

  return (
    <>
      <div style={{
        background: 'rgba(250,204,21,0.08)', border: '1px solid rgba(250,204,21,0.2)',
        borderRadius: 8, padding: '10px 14px', fontSize: '0.78rem', color: '#facc15', marginBottom: 12,
      }}>
        Les warnings sont en lecture seule — l'API ne propose pas encore d'endpoint pour en créer depuis le web.
      </div>
      {list.length === 0 ? (
        <Empty msg="Aucun warning." />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {list.map(w => (
            <div key={w.id} style={cardStyle()}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <AlertTriangle size={16} style={{ color: '#facc15' }} />
                <div style={{ flex: 1 }}>
                  <div style={{ fontFamily: 'monospace', fontWeight: 600, color: '#e8e8e8', fontSize: '0.85rem' }}>{w.steamId}</div>
                  <div style={{ fontSize: '0.78rem', color: '#a1a1aa', marginTop: 2 }}>{w.reason || '(sans raison)'}</div>
                  <div style={{ fontSize: '0.72rem', color: '#71717a', marginTop: 2 }}>par {w.fromAdminSteamId || 'inconnu'}</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  LIVE — joueurs actuellement connectés (auto-refresh toutes les 10s)
// ─────────────────────────────────────────────────────────────────────
function LiveTab({ onOpenPlayer }) {
  const [sessions, setSessions] = useState([])
  const [positions, setPositions] = useState([])
  const [loading,  setLoading]  = useState(true)
  const [error,    setError]    = useState('')
  const [autoRefresh, setAutoRefresh] = useState(true)
  const [now, setNow] = useState(Date.now())
  const [hover, setHover] = useState(null)

  const load = useCallback(async () => {
    try {
      const [s, p] = await Promise.all([
        api.gameAdminSessionsActive(),
        api.gameAdminPositions().catch(() => []), // map facultative
      ])
      setSessions(s); setPositions(p); setError('')
    } catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  // Refresh data toutes les 10s, refresh "elapsed" toutes les secondes
  useEffect(() => {
    if (!autoRefresh) return
    const dataTimer = setInterval(load, 10_000)
    const tickTimer = setInterval(() => setNow(Date.now()), 1_000)
    return () => { clearInterval(dataTimer); clearInterval(tickTimer) }
  }, [autoRefresh, load])

  if (loading) return <Loader />
  if (error)   return <ErrorBanner msg={error} onRetry={load} />

  const sorted = [...sessions].sort((a, b) => new Date(a.joinedAt) - new Date(b.joinedAt))

  // Map : un seul point par joueur en ligne (le perso actif). Sans ce filtre,
  // les anciens persos d'un même Steam ID apparaissent aussi car leur position
  // est conservée en DB et leur ownerId matche la session active.
  const activeSteamIds = new Set(sessions.map(s => s.steamId))
  const livePositions = positions.filter(p => activeSteamIds.has(p.ownerId) && p.isSelected)
  const sessionBySteamId = Object.fromEntries(sessions.map(s => [s.steamId, s]))

  return (
    <>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 16 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{
            width: 10, height: 10, borderRadius: '50%',
            background: autoRefresh ? '#4ade80' : '#71717a',
            boxShadow: autoRefresh ? '0 0 8px #4ade80' : 'none',
            animation: autoRefresh ? 'pulse 2s infinite' : 'none',
          }} />
          <span style={{ fontSize: '0.95rem', fontWeight: 700, color: '#e8e8e8' }}>
            {sessions.length} joueur{sessions.length !== 1 ? 's' : ''} en ligne
          </span>
        </div>
        <div style={{ flex: 1 }} />
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.78rem', color: '#a1a1aa', cursor: 'pointer' }}>
          <input type="checkbox" checked={autoRefresh} onChange={e => setAutoRefresh(e.target.checked)} />
          Auto-refresh 10s
        </label>
        <button onClick={load} style={iconBtn()} title="Rafraîchir maintenant"><RefreshCcw size={14} /></button>
      </div>

      {/* Mini-map live (positions mises à jour côté gamemode toutes les 10s) */}
      {livePositions.length > 0 && (
        <LiveMap positions={livePositions} sessionBySteamId={sessionBySteamId} hover={hover} setHover={setHover} onOpenPlayer={onOpenPlayer} />
      )}

      {sessions.length === 0 ? <Empty msg="Aucun joueur connecté actuellement." /> : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))', gap: 8 }}>
          {sorted.map(s => {
            const elapsedSec = Math.max(0, Math.floor((now - new Date(s.joinedAt).getTime()) / 1000))
            return (
              <button
                key={s.id}
                onClick={() => onOpenPlayer(s.steamId)}
                style={{
                  ...cardStyle({ padding: '12px 14px' }),
                  cursor: 'pointer', textAlign: 'left', width: '100%',
                  fontFamily: 'inherit', color: 'inherit', transition: 'border-color 0.15s, transform 0.1s',
                }}
                onMouseEnter={e => { e.currentTarget.style.borderColor = 'rgba(74,222,128,0.4)'; e.currentTarget.style.transform = 'translateY(-1px)' }}
                onMouseLeave={e => { e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)'; e.currentTarget.style.transform = 'none' }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  {s.steamProfile?.avatar
                    ? <img src={s.steamProfile.avatar} alt="" style={{ width: 40, height: 40, borderRadius: 6, flexShrink: 0 }} />
                    : <div style={{ width: 40, height: 40, borderRadius: 6, background: '#27272a', flexShrink: 0 }} />}
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: '0.92rem', fontWeight: 600, color: '#e8e8e8', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {s.steamProfile?.name || s.displayName || s.steamId}
                    </div>
                    <div style={{ fontFamily: 'monospace', fontSize: '0.7rem', color: '#52525b', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {s.steamId}
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 4 }}>
                      <Clock size={11} style={{ color: '#4ade80' }} />
                      <span style={{ fontSize: '0.74rem', color: '#4ade80', fontWeight: 600 }}>{fmtDuration(elapsedSec)}</span>
                      <span style={{ fontSize: '0.66rem', color: '#52525b' }}>· depuis {fmtDateTime(s.joinedAt)}</span>
                    </div>
                  </div>
                  <ChevronRight size={14} style={{ color: '#52525b', flexShrink: 0 }} />
                </div>
              </button>
            )
          })}
        </div>
      )}

      <style>{`@keyframes pulse { 0%,100% { opacity: 1 } 50% { opacity: 0.4 } }`}</style>
    </>
  )
}

// Mini-carte live pour LiveTab — variante compacte de MapTab
function LiveMap({ positions, sessionBySteamId, hover, setHover, onOpenPlayer }) {
  const pad = 30
  const width  = 800
  const height = 380

  const bounds = positions.reduce((b, p) => ({
    minX: Math.min(b.minX, p.x), maxX: Math.max(b.maxX, p.x),
    minY: Math.min(b.minY, p.y), maxY: Math.max(b.maxY, p.y),
  }), { minX: Infinity, maxX: -Infinity, minY: Infinity, maxY: -Infinity })

  const rangeX = Math.max(bounds.maxX - bounds.minX, 1)
  const rangeY = Math.max(bounds.maxY - bounds.minY, 1)
  const scale  = Math.min((width - 2 * pad) / rangeX, (height - 2 * pad) / rangeY)
  const project = (p) => ({
    px: pad + (p.x - bounds.minX) * scale,
    py: height - pad - (p.y - bounds.minY) * scale,
  })

  return (
    <div style={{
      position: 'relative', background: '#0a0e18',
      border: '1px solid rgba(74,222,128,0.15)', borderRadius: 12,
      marginBottom: 16, overflow: 'hidden',
    }}>
      <svg viewBox={`0 0 ${width} ${height}`} style={{ width: '100%', height: 'auto', display: 'block' }}>
        <defs>
          <pattern id="livegrid_small" width="20" height="20" patternUnits="userSpaceOnUse">
            <path d="M 20 0 L 0 0 0 20" fill="none" stroke="rgba(255,255,255,0.03)" strokeWidth="1" />
          </pattern>
          <pattern id="livegrid" width="100" height="100" patternUnits="userSpaceOnUse">
            <rect width="100" height="100" fill="url(#livegrid_small)" />
            <path d="M 100 0 L 0 0 0 100" fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="1" />
          </pattern>
        </defs>
        <rect width={width} height={height} fill="url(#livegrid)" opacity={0.5} />

        {positions.map(p => {
          const { px, py } = project(p)
          const session = sessionBySteamId[p.ownerId]
          return (
            <g key={p.characterId}
               onMouseEnter={() => setHover(p)}
               onMouseLeave={() => setHover(null)}
               onClick={() => onOpenPlayer(p.ownerId)}
               style={{ cursor: 'pointer' }}>
              <circle cx={px} cy={py} r={12} fill="#4ade80" opacity={0.18}>
                <animate attributeName="r" values="12;18;12" dur="2s" repeatCount="indefinite" />
                <animate attributeName="opacity" values="0.18;0;0.18" dur="2s" repeatCount="indefinite" />
              </circle>
              <circle cx={px} cy={py} r={hover?.characterId === p.characterId ? 8 : 6}
                      fill="#4ade80" stroke="#0a0e18" strokeWidth={2}
                      style={{ transition: 'r 0.15s' }} />
              <text x={px} y={py - 12} fill="#e8e8e8" fontSize="9" textAnchor="middle"
                    style={{ pointerEvents: 'none', userSelect: 'none' }}>
                {session?.steamProfile?.name || session?.displayName || `${p.firstName ?? ''} ${p.lastName ?? ''}`.trim() || '?'}
              </text>
            </g>
          )
        })}
      </svg>

      {hover && (
        <div style={{
          position: 'absolute', top: 12, left: 12,
          background: 'rgba(26,26,26,0.95)', border: '1px solid rgba(74,222,128,0.3)',
          borderRadius: 8, padding: '8px 12px', pointerEvents: 'none',
          boxShadow: '0 4px 12px rgba(0,0,0,0.4)',
        }}>
          <div style={{ fontWeight: 700, color: '#e8e8e8', fontSize: '0.82rem' }}>
            {hover.firstName} {hover.lastName}
          </div>
          <div style={{ fontSize: '0.68rem', color: '#a1a1aa', marginTop: 3, fontFamily: 'monospace' }}>
            X {hover.x.toFixed(0)} · Y {hover.y.toFixed(0)} · Z {hover.z.toFixed(0)}
          </div>
        </div>
      )}
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────
//  LOGS — vue centralisée (sessions / chat / actions admin / temps de jeu)
// ─────────────────────────────────────────────────────────────────────
const ADMIN_ACTION_META = {
  ban:              { label: 'Ban',              color: '#f87171' },
  unban:            { label: 'Débannissement',   color: '#4ade80' },
  whitelist_add:    { label: 'Whitelist ajouté', color: '#4ade80' },
  whitelist_remove: { label: 'Whitelist retiré', color: '#facc15' },
  kick:             { label: 'Kick',             color: '#fb923c' },
  warn:             { label: 'Warning',          color: '#fbbf24' },
  character_delete: { label: 'Perso supprimé',   color: '#f87171' },
  character_update: { label: 'Perso renommé',    color: '#60a5fa' },
  inventory_give:   { label: 'Item donné',       color: '#a78bfa' },
  inventory_modify: { label: 'Item modifié',     color: '#60a5fa' },
  inventory_delete: { label: 'Item supprimé',    color: '#fb923c' },
}

const LOG_SUBTABS = [
  { id: 'sessions',  label: 'Connexions',     icon: <LogIn size={13} /> },
  { id: 'playtime',  label: 'Temps de jeu',   icon: <Timer size={13} /> },
  { id: 'chat',      label: 'Chat',           icon: <MessageSquare size={13} /> },
  { id: 'actions',   label: 'Actions admin',  icon: <ShieldAlert size={13} /> },
]

function LogsTab() {
  const [sub, setSub] = useState('sessions')

  return (
    <>
      <div style={{ display: 'flex', gap: 4, marginBottom: 16, borderBottom: '1px solid rgba(255,255,255,0.06)', paddingBottom: 8 }}>
        {LOG_SUBTABS.map(t => (
          <button
            key={t.id}
            onClick={() => setSub(t.id)}
            style={{
              display: 'flex', alignItems: 'center', gap: 6,
              padding: '6px 12px', borderRadius: 6, border: 'none', cursor: 'pointer',
              fontSize: '0.78rem', fontFamily: 'inherit', fontWeight: 600,
              background: sub === t.id ? 'rgba(60, 173, 217,0.18)' : 'transparent',
              color:      sub === t.id ? 'var(--brand-primary, #e07b39)' : '#a1a1aa',
            }}
          >
            {t.icon} {t.label}
          </button>
        ))}
      </div>

      {sub === 'sessions' && <SessionsSubTab />}
      {sub === 'playtime' && <PlaytimeSubTab />}
      {sub === 'chat'     && <ChatSubTab />}
      {sub === 'actions'  && <AdminActionsSubTab />}
    </>
  )
}

// Format helpers
function fmtDateTime(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return d.toLocaleDateString('fr-FR', { day: '2-digit', month: 'short' })
       + ' ' + d.toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}
function fmtDuration(seconds) {
  if (seconds == null) return '—'
  const s = Math.max(0, Math.floor(seconds))
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = s % 60
  if (h > 0) return `${h}h ${m.toString().padStart(2,'0')}m`
  if (m > 0) return `${m}m ${sec.toString().padStart(2,'0')}s`
  return `${sec}s`
}

function PlayerCell({ steamId, profile }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
      {profile?.avatar
        ? <img src={profile.avatar} alt="" style={{ width: 22, height: 22, borderRadius: 4, flexShrink: 0 }} />
        : <div style={{ width: 22, height: 22, borderRadius: 4, background: '#27272a', flexShrink: 0 }} />}
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: '0.82rem', color: '#e8e8e8', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {profile?.name || steamId}
        </div>
        {profile?.name && (
          <div style={{ fontFamily: 'monospace', fontSize: '0.65rem', color: '#52525b', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {steamId}
          </div>
        )}
      </div>
    </div>
  )
}

// ───────── Connexions ─────────
function SessionsSubTab() {
  const [data,    setData]    = useState({ sessions: [], total: 0 })
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [steamId, setSteamId] = useState('')
  const [activeOnly, setActiveOnly] = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setData(await api.gameAdminSessions({ steamId, activeOnly: activeOnly ? 'true' : '', pageSize: 200 })) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [steamId, activeOnly])

  useEffect(() => { load() }, [load])

  if (loading) return <Loader />
  if (error)   return <ErrorBanner msg={error} onRetry={load} />

  return (
    <>
      <FiltersRow onRefresh={load}>
        <input
          value={steamId} onChange={e => setSteamId(e.target.value)}
          placeholder="SteamID 64…"
          style={inputStyle()}
        />
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.78rem', color: '#a1a1aa', cursor: 'pointer' }}>
          <input type="checkbox" checked={activeOnly} onChange={e => setActiveOnly(e.target.checked)} />
          Sessions actives uniquement
        </label>
        <span style={{ fontSize: '0.72rem', color: '#71717a' }}>{data.total ?? 0} session(s)</span>
      </FiltersRow>

      {(!data.sessions || data.sessions.length === 0) ? <Empty msg="Aucune session." /> : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          {data.sessions.map(s => {
            const isActive = !s.leftAt
            return (
              <div key={s.id} style={cardStyle({ padding: '10px 14px' })}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  <span style={{
                    padding: '2px 9px', borderRadius: 6, fontSize: '0.7rem', fontWeight: 700, minWidth: 70, textAlign: 'center',
                    background: isActive ? 'rgba(74,222,128,0.18)' : 'rgba(113,113,122,0.18)',
                    color:      isActive ? '#4ade80' : '#a1a1aa',
                  }}>
                    {isActive ? '● ACTIVE' : 'CLOSE'}
                  </span>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <PlayerCell steamId={s.steamId} profile={s.steamProfile} />
                  </div>
                  <div style={{ fontSize: '0.72rem', color: '#a1a1aa', textAlign: 'right', minWidth: 220 }}>
                    <div>{fmtDateTime(s.joinedAt)} → {s.leftAt ? fmtDateTime(s.leftAt) : '…'}</div>
                    <div style={{ color: '#71717a', fontSize: '0.68rem' }}>{fmtDuration(s.durationSeconds)}</div>
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      )}
    </>
  )
}

// ───────── Temps de jeu agrégé ─────────
function PlaytimeSubTab() {
  const [rows,    setRows]    = useState([])
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setRows(await api.gameAdminPlaytime({})) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  if (loading) return <Loader />
  if (error)   return <ErrorBanner msg={error} onRetry={load} />

  return (
    <>
      <FiltersRow onRefresh={load}>
        <span style={{ fontSize: '0.72rem', color: '#71717a' }}>{rows.length} joueur(s)</span>
      </FiltersRow>

      {rows.length === 0 ? <Empty msg="Aucune donnée de temps de jeu." /> : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          {rows.map(r => (
            <div key={r.steamId} style={cardStyle({ padding: '10px 14px' })}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <PlayerCell steamId={r.steamId} profile={r.steamProfile} />
                </div>
                <div style={{ textAlign: 'right', minWidth: 200 }}>
                  <div style={{ fontSize: '0.95rem', color: 'var(--brand-primary, #e07b39)', fontWeight: 700 }}>{fmtDuration(r.totalSeconds)}</div>
                  <div style={{ fontSize: '0.68rem', color: '#71717a' }}>{r.sessionCount} session(s)</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  )
}

// ───────── Chat ─────────
function ChatSubTab() {
  const [data,    setData]    = useState({ messages: [], total: 0 })
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [steamId, setSteamId] = useState('')
  const [search,  setSearch]  = useState('')
  const [excludeCommands, setExcludeCommands] = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      setData(await api.gameAdminChat({
        steamId, search,
        excludeCommands: excludeCommands ? 'true' : '',
        pageSize: 300,
      }))
    } catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [steamId, search, excludeCommands])

  useEffect(() => { load() }, [load])

  if (loading) return <Loader />
  if (error)   return <ErrorBanner msg={error} onRetry={load} />

  return (
    <>
      <FiltersRow onRefresh={load}>
        <input value={steamId} onChange={e => setSteamId(e.target.value)} placeholder="SteamID 64…" style={inputStyle({ width: 180 })} />
        <input value={search}  onChange={e => setSearch(e.target.value)}  placeholder="Rechercher dans les messages…" style={inputStyle({ flex: 1 })} />
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.78rem', color: '#a1a1aa', cursor: 'pointer' }}>
          <input type="checkbox" checked={excludeCommands} onChange={e => setExcludeCommands(e.target.checked)} />
          Cacher commandes
        </label>
        <span style={{ fontSize: '0.72rem', color: '#71717a' }}>{data.total ?? 0} msg</span>
      </FiltersRow>

      {(!data.messages || data.messages.length === 0) ? <Empty msg="Aucun message." /> : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          {data.messages.map(m => (
            <div key={m.id} style={cardStyle({ padding: '8px 12px' })}>
              <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
                <div style={{ minWidth: 90, fontFamily: 'monospace', fontSize: '0.68rem', color: '#52525b', flexShrink: 0, marginTop: 2 }}>
                  {fmtDateTime(m.sentAt)}
                </div>
                <div style={{ minWidth: 160, flexShrink: 0 }}>
                  <PlayerCell steamId={m.steamId} profile={m.steamProfile} />
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  {m.isCommand && <span style={{ background: 'rgba(251,191,36,0.18)', color: '#fbbf24', padding: '1px 6px', borderRadius: 4, fontSize: '0.65rem', fontWeight: 700, marginRight: 6 }}><Terminal size={9} style={{ verticalAlign: 'middle' }} /> CMD</span>}
                  <span style={{ fontSize: '0.85rem', color: m.isCommand ? '#fbbf24' : '#e8e8e8', wordBreak: 'break-word' }}>
                    {m.message}
                  </span>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  )
}

// ───────── Actions admin ─────────
function AdminActionsSubTab() {
  const [data,    setData]    = useState({ actions: [], total: 0 })
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [adminSteamId, setAdminSteamId]  = useState('')
  const [targetSteamId, setTargetSteamId] = useState('')
  const [actionType, setActionType] = useState('')
  const [source, setSource] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      setData(await api.gameAdminAdminActions({
        adminSteamId, targetSteamId, action: actionType, source,
        pageSize: 300,
      }))
    } catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [adminSteamId, targetSteamId, actionType, source])

  useEffect(() => { load() }, [load])

  if (loading) return <Loader />
  if (error)   return <ErrorBanner msg={error} onRetry={load} />

  return (
    <>
      <FiltersRow onRefresh={load}>
        <input value={adminSteamId}  onChange={e => setAdminSteamId(e.target.value)}  placeholder="Admin SteamID…"  style={inputStyle({ width: 160 })} />
        <input value={targetSteamId} onChange={e => setTargetSteamId(e.target.value)} placeholder="Target SteamID…" style={inputStyle({ width: 160 })} />
        <select value={actionType} onChange={e => setActionType(e.target.value)} style={selectStyle()}>
          <option value="">Toutes actions</option>
          {Object.entries(ADMIN_ACTION_META).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
        </select>
        <select value={source} onChange={e => setSource(e.target.value)} style={selectStyle()}>
          <option value="">Toutes sources</option>
          <option value="web">Panel web</option>
          <option value="ingame">In-game</option>
        </select>
        <span style={{ fontSize: '0.72rem', color: '#71717a' }}>{data.total ?? 0}</span>
      </FiltersRow>

      {(!data.actions || data.actions.length === 0) ? <Empty msg="Aucune action." /> : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          {data.actions.map(a => {
            const meta = ADMIN_ACTION_META[a.action] || { label: a.action, color: '#a1a1aa' }
            const SourceIcon = a.source === 'ingame' ? Gamepad2 : Globe
            let payload = null
            if (a.payloadJson) {
              try { payload = typeof a.payloadJson === 'string' ? JSON.parse(a.payloadJson) : a.payloadJson } catch { /* ignore */ }
            }
            return (
              <div key={a.id} style={cardStyle({ padding: '10px 14px' })}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  <span style={{
                    padding: '2px 9px', borderRadius: 6, fontSize: '0.7rem', fontWeight: 700, minWidth: 130, textAlign: 'center',
                    background: meta.color + '22', color: meta.color,
                  }}>
                    {meta.label}
                  </span>
                  <SourceIcon size={12} color="#71717a" title={a.source} />
                  <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 2 }}>
                    {a.targetSteamId && (
                      <div style={{ fontSize: '0.78rem' }}>
                        <span style={{ color: '#71717a' }}>cible : </span>
                        <span style={{ color: '#e8e8e8' }}>{a.targetProfile?.name || a.targetSteamId}</span>
                      </div>
                    )}
                    {/* Détails inventaire depuis payloadJson */}
                    {payload?.characterId && (
                      <div style={{ fontSize: '0.75rem' }}>
                        <span style={{ color: '#71717a' }}>perso : </span>
                        <span style={{ color: '#e8e8e8', fontFamily: 'monospace' }}>{payload.characterId}</span>
                      </div>
                    )}
                    {payload?.itemGameId && (
                      <div style={{ fontSize: '0.75rem' }}>
                        <span style={{ color: '#71717a' }}>item : </span>
                        <span style={{ color: '#c4b5fd', fontWeight: 600 }}>{payload.itemGameId}</span>
                        {payload.count != null && <span style={{ color: '#71717a' }}> ×{payload.count}</span>}
                      </div>
                    )}
                    {payload?.itemId && !payload?.itemGameId && (
                      <div style={{ fontSize: '0.72rem', color: '#71717a', fontFamily: 'monospace' }}>
                        item id : {payload.itemId}
                      </div>
                    )}
                    {payload?.changes && (
                      <div style={{ fontSize: '0.72rem', color: '#a1a1aa' }}>
                        {Object.entries(payload.changes).map(([k, v]) => `${k} → ${JSON.stringify(v)}`).join(', ')}
                      </div>
                    )}
                    {a.reason && <div style={{ fontSize: '0.72rem', color: '#a1a1aa' }}>{a.reason}</div>}
                    <div style={{ fontSize: '0.68rem', color: '#52525b' }}>
                      par {a.adminProfile?.name || a.adminSteamId}
                    </div>
                  </div>
                  <div style={{ fontFamily: 'monospace', fontSize: '0.7rem', color: '#52525b', flexShrink: 0 }}>
                    {fmtDateTime(a.at)}
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      )}
    </>
  )
}

function FiltersRow({ children, onRefresh }) {
  return (
    <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12, flexWrap: 'wrap' }}>
      {children}
      <button onClick={onRefresh} style={iconBtn()} title="Rafraîchir"><RefreshCcw size={14} /></button>
    </div>
  )
}
function inputStyle(extra = {}) {
  return {
    background: '#161a26', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 6,
    padding: '6px 10px', color: '#e8e8e8', fontSize: '0.8rem', fontFamily: 'inherit', outline: 'none',
    ...extra,
  }
}
const labelStyle = { display: 'block', fontSize: '0.72rem', color: '#71717a', marginBottom: 4, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }
function selectStyle() {
  return { ...inputStyle(), padding: '5px 8px', cursor: 'pointer' }
}

// ─────────────────────────────────────────────────────────────────────
//  UI helpers
// ─────────────────────────────────────────────────────────────────────
function Loader() {
  return (
    <div style={{ padding: 40, textAlign: 'center', color: '#71717a', fontSize: '0.85rem' }}>
      Chargement…
    </div>
  )
}

function Empty({ msg }) {
  return (
    <div style={{ padding: '32px 20px', textAlign: 'center', color: '#52525b', fontSize: '0.85rem', background: '#171717', borderRadius: 10, border: '1px dashed rgba(255,255,255,0.06)' }}>
      {msg}
    </div>
  )
}

function ErrorBanner({ msg, onRetry }) {
  return (
    <div style={{
      background: 'rgba(248,113,113,0.08)', border: '1px solid rgba(248,113,113,0.25)',
      borderRadius: 8, padding: '10px 14px', color: '#f87171', fontSize: '0.8rem',
      display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12,
    }}>
      <span style={{ flex: 1 }}>Erreur : {msg}</span>
      {onRetry && <button onClick={onRetry} style={iconBtn()}><RefreshCcw size={13} /></button>}
    </div>
  )
}

function BackBar({ onBack, label, onRefresh, live, livePaused, onToggleLive, lastRefresh }) {
  const showLive = typeof onToggleLive === 'function'
  const dotColor = !live ? '#52525b' : livePaused ? '#fbbf24' : '#4ade80'
  const dotTitle = !live
    ? 'Live OFF — clique pour activer le rafraîchissement automatique'
    : livePaused
      ? 'Live en pause (modal ouverte)'
      : 'Live ON — données rafraîchies toutes les 3 s'
  const ago = lastRefresh ? Math.max(0, Math.round((Date.now() - lastRefresh) / 1000)) : null
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 16 }}>
      <button onClick={onBack} style={{
        background: '#161a26', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 8,
        padding: '7px 12px', cursor: 'pointer', color: '#a1a1aa', fontSize: '0.8rem',
        display: 'flex', alignItems: 'center', gap: 6,
      }}>
        <ChevronRight size={13} style={{ transform: 'rotate(180deg)' }} /> Retour
      </button>
      <span style={{ flex: 1, color: '#e8e8e8', fontSize: '0.9rem', fontWeight: 600 }}>{label}</span>
      {showLive && (
        <button
          onClick={onToggleLive}
          title={dotTitle}
          style={{
            display: 'flex', alignItems: 'center', gap: 6,
            background: '#161a26', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 8,
            padding: '6px 10px', cursor: 'pointer', color: '#a1a1aa', fontSize: '0.72rem',
            fontFamily: 'inherit',
          }}
        >
          <span style={{
            width: 8, height: 8, borderRadius: '50%', background: dotColor,
            boxShadow: live && !livePaused ? `0 0 6px ${dotColor}` : 'none',
            animation: live && !livePaused ? 'admLivePulse 1.4s ease-in-out infinite' : 'none',
          }} />
          {live ? (livePaused ? 'Live (pause)' : 'Live') : 'Live OFF'}
          {live && !livePaused && ago !== null && (
            <span style={{ color: '#52525b', fontSize: '0.65rem' }}>· {ago}s</span>
          )}
        </button>
      )}
      {onRefresh && <button onClick={onRefresh} style={iconBtn()} title="Rafraîchir maintenant"><RefreshCcw size={14} /></button>}
    </div>
  )
}

function SectionTitle({ icon, title, noMargin }) {
  return (
    <h3 style={{
      fontSize: '0.75rem', fontWeight: 800, color: '#a1a1aa', textTransform: 'uppercase',
      letterSpacing: '0.08em', margin: noMargin ? 0 : '0 0 10px', display: 'flex', alignItems: 'center', gap: 6,
    }}>
      {icon} {title}
    </h3>
  )
}

function Pill({ color, label, big }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center',
      padding: big ? '4px 10px' : '2px 7px',
      borderRadius: 99, fontSize: big ? '0.72rem' : '0.65rem',
      fontWeight: 700, background: color + '22', color,
      border: `1px solid ${color}44`,
    }}>{label}</span>
  )
}

function cardStyle(extra = {}) {
  return {
    background: '#161a26', border: '1px solid rgba(255,255,255,0.06)',
    borderRadius: 10, padding: '11px 14px', ...extra,
  }
}

function iconBtn() {
  return {
    background: '#161a26', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 8,
    padding: '7px 9px', cursor: 'pointer', color: '#a1a1aa',
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
  }
}

function primaryBtn() {
  return {
    background: 'var(--brand-primary, #e07b39)', border: 'none', borderRadius: 8, padding: '8px 16px',
    cursor: 'pointer', color: '#fff', fontWeight: 600, fontSize: '0.82rem',
    display: 'inline-flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
  }
}

function dangerBtn() {
  return {
    background: 'rgba(248,113,113,0.12)', border: '1px solid rgba(248,113,113,0.25)',
    borderRadius: 8, padding: '6px 9px', cursor: 'pointer', color: '#f87171',
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
  }
}

function formRowStyle() {
  return { display: 'flex', gap: 8, marginBottom: 14, flexWrap: 'wrap' }
}

function formatMoney(n) {
  if (n == null || isNaN(n)) return '—'
  return Number(n).toLocaleString('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' §'
}

function txTypeLabel(type) {
  const map = { 0: 'Dépôt', 1: 'Retrait', 2: 'Virement', 3: 'Salaire', 4: 'Admin' }
  if (typeof type === 'number') return map[type] ?? type
  return String(type)
}

function txStatusLabel(status) {
  const map = { 0: 'Complétée', 1: 'Échouée', 2: 'Annulée' }
  if (typeof status === 'number') return map[status] ?? status
  return String(status)
}

// ─────────────────────────────────────────────────────────────────────
//  ADMINS JEU — SteamIDs ayant IsAdmin=true dans le gamemode
// ─────────────────────────────────────────────────────────────────────
function GameAdminsTab() {
  const [list,    setList]    = useState([])
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')
  const [steamId, setSteamId] = useState('')
  const [label,   setLabel]   = useState('')
  const [busy,    setBusy]    = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setList(await api.gameAdminGameAdmins()) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  async function add(e) {
    e.preventDefault()
    if (!steamId.trim()) return
    setBusy(true); setError('')
    try {
      await api.gameAdminAddGameAdmin({ steam_id: steamId.trim(), label: label.trim() })
      setSteamId(''); setLabel('')
      load()
    } catch (e) { setError(e.message) }
    finally { setBusy(false) }
  }

  async function remove(sid) {
    if (!confirm(`Retirer ${sid} des admins jeu ?`)) return
    try { await api.gameAdminRemoveGameAdmin(sid); load() }
    catch (e) { setError(e.message) }
  }

  return (
    <>
      <div style={{
        background: 'rgba(248,113,113,0.08)', border: '1px solid rgba(248,113,113,0.2)',
        borderRadius: 8, padding: '10px 14px', fontSize: '0.78rem', color: '#fca5a5', marginBottom: 14,
      }}>
        Ces SteamIDs ont <strong>IsAdmin = true</strong> dans le gamemode. Le serveur les synchronise toutes les 60s via le panel web. Réservé aux owners/admins du site.
      </div>

      <form onSubmit={add} style={formRowStyle()}>
        <input
          value={steamId}
          onChange={e => setSteamId(e.target.value)}
          placeholder="SteamID64 (17 chiffres)"
          required
          pattern="\d{17}"
          title="SteamID64 — 17 chiffres"
          style={{ ...inputStyle(), flex: 2 }}
        />
        <input
          value={label}
          onChange={e => setLabel(e.target.value)}
          placeholder="Pseudo ou note (optionnel)"
          style={{ ...inputStyle(), flex: 2 }}
        />
        <button type="submit" disabled={busy} style={primaryBtn()}>
          <Plus size={13} /> Ajouter
        </button>
      </form>

      {error && <ErrorBanner msg={error} onRetry={load} />}

      {loading ? <Loader /> : list.length === 0 ? (
        <Empty msg="Aucun admin jeu configuré." />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {list.map(a => (
            <div key={a.steam_id} style={cardStyle()}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <ShieldAlert size={16} style={{ color: '#f87171' }} />
                <div style={{ flex: 1 }}>
                  <div style={{ fontFamily: 'monospace', fontWeight: 600, color: '#e8e8e8', fontSize: '0.85rem' }}>
                    {a.steam_id}
                    {a.label && <span style={{ marginLeft: 8, fontFamily: 'inherit', fontWeight: 400, color: '#a1a1aa', fontSize: '0.8rem' }}>{a.label}</span>}
                  </div>
                  <div style={{ fontSize: '0.72rem', color: '#71717a', marginTop: 2 }}>
                    Ajouté par {a.added_by || 'inconnu'} · {a.added_at ? new Date(a.added_at).toLocaleDateString('fr-FR') : ''}
                  </div>
                </div>
                <button onClick={() => remove(a.steam_id)} style={dangerBtn()} title="Retirer">
                  <Trash2 size={13} />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  )
}
