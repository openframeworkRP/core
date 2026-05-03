// URL de base de l'API — en dev Vite proxifie, en prod adapter
export const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3001'

// Sérialise un objet de filtres en querystring, en sautant les valeurs vides
function qs(params) {
  const parts = []
  for (const [k, v] of Object.entries(params || {})) {
    if (v === undefined || v === null || v === '') continue
    parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
  }
  return parts.length ? `?${parts.join('&')}` : ''
}

async function req(method, path, body) {
  const opts = {
    method,
    credentials: 'include',
    headers: body instanceof FormData ? {} : { 'Content-Type': 'application/json' },
    body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined,
  }
  const res = await fetch(`${API_BASE}${path}`, opts)
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }))
    throw new Error(err.error || res.statusText)
  }
  return res.json()
}

export const api = {
  // Games
  getGames:   ()           => req('GET',    '/api/games'),
  createGame: (body)       => req('POST',   '/api/games', body),
  updateGame: (id, body)   => req('PUT',    `/api/games/${id}`, body),
  deleteGame: (id)         => req('DELETE', `/api/games/${id}`),

  // Posts
  getPosts:    ()           => req('GET',    '/api/posts?all=1'),
  getPost:     (slug)       => req('GET',    `/api/posts/${slug}`),
  createPost:  (body)       => req('POST',   '/api/posts', body),
  updatePost:  (id, body)   => req('PUT',    `/api/posts/${id}`, body),
  deletePost:  (id)         => req('DELETE', `/api/posts/${id}`),
  publishPost:   (id)       => req('POST',   `/api/posts/${id}/publish`),
  unpublishPost: (id)       => req('POST',   `/api/posts/${id}/unpublish`),

  // Blocks
  createBlock: (postId, body) => req('POST',   `/api/posts/${postId}/blocks`, body),
  updateBlock: (postId, blockId, body) => req('PUT', `/api/posts/${postId}/blocks/${blockId}`, body),
  deleteBlock: (postId, blockId)       => req('DELETE', `/api/posts/${postId}/blocks/${blockId}`),
  reorderBlocks: (postId, body)        => req('PUT',    `/api/posts/${postId}/blocks/reorder`, body),

  // Upload
  upload: (file) => {
    const fd = new FormData()
    fd.append('file', file)
    return req('POST', '/api/upload', fd)
  },

  // Export / Import archive .devblog
  exportPost: (id) => {
    // Retourne l'URL directe — le navigateur déclenchera le téléchargement
    return `${API_BASE}/api/posts/${id}/export`
  },
  importPost: (file) => {
    const fd = new FormData()
    fd.append('archive', file)
    return req('POST', '/api/posts/import', fd)
  },

  // Traduction automatique FR → EN
  translate: (body) => req('POST', '/api/translate', body),

  // Jobs
  getJobs:       ()           => req('GET',    '/api/jobs'),
  getAdminJobs:  ()           => req('GET',    '/api/jobs/admin'),
  createJob:     (body)       => req('POST',   '/api/jobs', body),
  updateJob:     (id, body)   => req('PUT',    `/api/jobs/${id}`, body),
  deleteJob:     (id)         => req('DELETE', `/api/jobs/${id}`),
  toggleJob:     (id)         => req('POST',   `/api/jobs/${id}/toggle`),

  // Users / rôles
  getUsers:      ()           => req('GET',    '/api/users'),
  createUser:    (body)       => req('POST',   '/api/users', body),
  updateUser:    (id, body)   => req('PUT',    `/api/users/${id}`, body),
  deleteUser:    (id)         => req('DELETE', `/api/users/${id}`),
  migrateHubId:  (body)       => req('POST',   '/api/users/migrate-hub-id', body),

  // Membres (page équipe publique)
  getMembers:    ()           => req('GET',    '/api/members'),
  createMember:  (body)       => req('POST',   '/api/members', body),
  updateMember:  (id, body)   => req('PUT',    `/api/members/${id}`, body),
  deleteMember:  (id)         => req('DELETE', `/api/members/${id}`),

  // Tags membres
  getMemberTags:    ()              => req('GET',    '/api/members/tags'),
  createMemberTag:  (body)          => req('POST',   '/api/members/tags', body),
  updateMemberTag:  (id, body)      => req('PUT',    `/api/members/tags/${id}`, body),
  deleteMemberTag:  (id)            => req('DELETE', `/api/members/tags/${id}`),
  addMemberTag:     (mId, tId)      => req('POST',   `/api/members/${mId}/tags/${tId}`),
  removeMemberTag:  (mId, tId)      => req('DELETE', `/api/members/${mId}/tags/${tId}`),

  // Hub — lecture unifiée
  getHub:      ()      => req('GET', '/api/hub'),
  saveHub:     (body)  => req('PUT', '/api/hub', body),  // legacy

  // Hub — tasks (granulaire)
  createTask:       (body)         => req('POST',   '/api/hub/tasks', body),
  updateTask:       (id, body)     => req('PATCH',  `/api/hub/tasks/${id}`, body),
  deleteTask:       (id)           => req('DELETE', `/api/hub/tasks/${id}`),
  bulkCreateTasks:  (body)         => req('POST',   '/api/hub/tasks/bulk', body),
  bulkUpdateTasks:  (body)         => req('PATCH',  '/api/hub/tasks/bulk', body),

  // Hub — ideas (granulaire)
  createIdea:       (body)         => req('POST',   '/api/hub/ideas', body),
  updateIdea:       (id, body)     => req('PATCH',  `/api/hub/ideas/${id}`, body),
  deleteIdea:       (id)           => req('DELETE', `/api/hub/ideas/${id}`),
  bulkCreateIdeas:  (body)         => req('POST',   '/api/hub/ideas/bulk', body),

  // Hub — misc blob (milestones, mapAnnotations, fabAssets, fabStudios)
  saveMisc:         (body)         => req('PUT',    '/api/hub/misc', body),

  // Activité hub
  getActivity:  (limit) => req('GET', `/api/hub/activity${limit ? `?limit=${limit}` : ''}`),
  getTaskActivity: (taskId, limit) => req('GET', `/api/hub/activity?targetType=task&targetId=${encodeURIComponent(taskId)}${limit ? `&limit=${limit}` : ''}`),
  addActivity:  (body)  => req('POST', '/api/hub/activity', body),

  // Brand kit (charte graphique)
  getBrandKit:  ()     => req('GET', '/api/hub/brand-kit'),
  saveBrandKit: (body) => req('PUT', '/api/hub/brand-kit', body),

  // Fab proxy (contourne le CORS navigateur)
  getFabListings: (username) => req('GET', `/api/fab/listings?username=${encodeURIComponent(username)}`),
  getFabPreview:  (url)      => req('GET', `/api/fab/preview?url=${encodeURIComponent(url)}`),
  getFabAsset:    (url)      => req('GET', `/api/fab/asset?url=${encodeURIComponent(url)}`),

  // Videos
  getVideos:         ()           => req('GET',    '/api/videos'),
  getVideo:          (slug)       => req('GET',    `/api/videos/${slug}`),
  getVideoProgress:  (slug)       => req('GET',    `/api/videos/${slug}/progress`),
  renameVideo:       (slug, body) => req('PATCH',  `/api/videos/${slug}`, body),
  deleteVideo:  (slug)       => req('DELETE', `/api/videos/${slug}`),
  uploadVideo:  (file, title, onProgress) => {
    return new Promise((resolve, reject) => {
      const fd = new FormData()
      fd.append('file', file)
      if (title) fd.append('title', title)
      const xhr = new XMLHttpRequest()
      xhr.open('POST', `${API_BASE}/api/videos`)
      xhr.withCredentials = true
      if (onProgress) xhr.upload.onprogress = (e) => onProgress(e.loaded, e.total)
      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          try { resolve(JSON.parse(xhr.responseText)) }
          catch { reject(new Error('Invalid JSON')) }
        } else {
          try { reject(new Error(JSON.parse(xhr.responseText).error || xhr.statusText)) }
          catch { reject(new Error(xhr.statusText)) }
        }
      }
      xhr.onerror = () => reject(new Error('Network error'))
      xhr.send(fd)
    })
  },

  // Images
  getImages:    ()           => req('GET',    '/api/images'),
  getImage:     (slug)       => req('GET',    `/api/images/${slug}`),
  renameImage:  (slug, body) => req('PATCH',  `/api/images/${slug}`, body),
  deleteImage:  (slug)       => req('DELETE', `/api/images/${slug}`),
  uploadImage:  (file, title, onProgress) => {
    return new Promise((resolve, reject) => {
      const fd = new FormData()
      fd.append('file', file)
      if (title) fd.append('title', title)
      const xhr = new XMLHttpRequest()
      xhr.open('POST', `${API_BASE}/api/images`)
      xhr.withCredentials = true
      if (onProgress) xhr.upload.onprogress = (e) => onProgress(e.loaded, e.total)
      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          try { resolve(JSON.parse(xhr.responseText)) }
          catch { reject(new Error('Invalid JSON')) }
        } else {
          try { reject(new Error(JSON.parse(xhr.responseText).error || xhr.statusText)) }
          catch { reject(new Error(xhr.statusText)) }
        }
      }
      xhr.onerror = () => reject(new Error('Network error'))
      xhr.send(fd)
    })
  },

  // Rules
  getRules:           ()              => req('GET',    '/api/rules'),
  getRuleHistory:     ()              => req('GET',    '/api/rules/history'),
  getRuleItemHistory: (id)            => req('GET',    `/api/rules/${id}/history`),
  createRuleCategory: (body)          => req('POST',   '/api/rules/categories', body),
  updateRuleCategory: (id, body)      => req('PUT',    `/api/rules/categories/${id}`, body),
  deleteRuleCategory: (id)            => req('DELETE', `/api/rules/categories/${id}`),
  createRule:         (catId, body)   => req('POST',   `/api/rules/categories/${catId}/rules`, body),
  updateRule:         (id, body)      => req('PUT',    `/api/rules/${id}`, body),
  deleteRule:         (id)            => req('DELETE', `/api/rules/${id}`),

  // OpenFramework Books (livres de règles)
  getSlBooks:           ()                        => req('GET',    '/api/rules/sl-books'),
  getSlBooksPublic:     ()                        => req('GET',    '/api/rules/sl-books/public'),
  createSlBook:         (body)                    => req('POST',   '/api/rules/sl-books', body),
  updateSlBook:         (bookId, body)            => req('PUT',    `/api/rules/sl-books/${bookId}`, body),
  createSlChapter:      (bookId, body)            => req('POST',   `/api/rules/sl-books/${bookId}/chapters`, body),
  updateSlBook_Chapter: (bookId, chId, body)      => req('PUT',    `/api/rules/sl-books/${bookId}/chapters/${chId}`, body),
  deleteSlChapter:      (bookId, chId)            => req('DELETE', `/api/rules/sl-books/${bookId}/chapters/${chId}`),
  createSlBlock:        (chId, body)              => req('POST',   `/api/rules/sl-chapters/${chId}/blocks`, body),
  updateSlBlock:        (blockId, body)           => req('PUT',    `/api/rules/sl-blocks/${blockId}`, body),
  deleteSlBlock:        (blockId)                 => req('DELETE', `/api/rules/sl-blocks/${blockId}`),
  reorderSlBlocks:      (chId, order)             => req('PUT',    `/api/rules/sl-chapters/${chId}/blocks/reorder`, { order }),

  // Docs (documentation interne arborescente)
  getDocsTree:     ()            => req('GET',    '/api/docs/tree'),
  getDocsPage:     (id)          => req('GET',    `/api/docs/${id}`),
  createDocsPage:  (body)        => req('POST',   '/api/docs', body),
  updateDocsPage:  (id, body)    => req('PUT',    `/api/docs/${id}`, body),
  deleteDocsPage:  (id)          => req('DELETE', `/api/docs/${id}`),
  moveDocsPage:    (id, body)    => req('POST',   `/api/docs/${id}/move`, body),
  toggleDocsPage:  (id)          => req('POST',   `/api/docs/${id}/toggle`),
  searchDocs:      (q)           => req('GET',    `/api/docs/search?q=${encodeURIComponent(q)}`),
  getDocsHistory:  (id)          => req('GET',    `/api/docs/${id}/history`),
  exportDocsUrl:   (id, deep=false) => `${API_BASE}/api/docs/${id}/export${deep ? '?deep=1' : ''}`,
  importDocsMarkdown: ({ parent_id = null, markdown, mode = 'append' }) =>
    req('POST', '/api/docs/import', { parent_id, markdown, mode }),
  importDocsFile: (file, { parent_id = null, mode = 'append' } = {}) => {
    const fd = new FormData()
    fd.append('file', file)
    if (parent_id != null) fd.append('parent_id', String(parent_id))
    fd.append('mode', mode)
    return req('POST', '/api/docs/import', fd)
  },

  // Wiki
  getWiki:              ()              => req('GET',    '/api/wiki'),
  getWikiHistory:       ()              => req('GET',    '/api/wiki/history'),
  getWikiArticleHistory:(id)            => req('GET',    `/api/wiki/articles/${id}/history`),
  createWikiCategory:   (body)          => req('POST',   '/api/wiki/categories', body),
  updateWikiCategory:   (id, body)      => req('PUT',    `/api/wiki/categories/${id}`, body),
  deleteWikiCategory:   (id)            => req('DELETE', `/api/wiki/categories/${id}`),
  createWikiArticle:    (catId, body)   => req('POST',   `/api/wiki/categories/${catId}/articles`, body),
  updateWikiArticle:    (id, body)      => req('PUT',    `/api/wiki/articles/${id}`, body),
  toggleWikiArticle:    (id)            => req('POST',   `/api/wiki/articles/${id}/toggle`),
  deleteWikiArticle:    (id)            => req('DELETE', `/api/wiki/articles/${id}`),

  // Nextcloud file browser
  ncBrowse:         (path) => req('GET', `/api/nextcloud/browse?path=${encodeURIComponent(path)}`),
  ncDownload:       (path) => `${API_BASE}/api/nextcloud/download?path=${encodeURIComponent(path)}`,
  ncScanAssets:     ()     => req('GET', '/api/nextcloud/scan-assets'),
  ncCreateRequest:  (filePath, fileName) => req('POST', '/api/nextcloud/requests', { filePath, fileName }),
  ncGetRequests:    (status) => req('GET', `/api/nextcloud/requests${status ? `?status=${status}` : ''}`),
  ncGetMyRequests:  ()     => req('GET', '/api/nextcloud/my-requests'),
  ncPatchRequest:   (id, status) => req('PATCH', `/api/nextcloud/requests/${id}`, { status }),

  // Asset catalogue
  getAssets:       ()           => req('GET',    '/api/assets'),
  createAsset:     (body)       => req('POST',   '/api/assets', body),
  updateAsset:     (id, body)   => req('PUT',    `/api/assets/${id}`, body),
  deleteAsset:     (id)         => req('DELETE', `/api/assets/${id}`),
  bulkImportAssets:(body)       => req('POST',   '/api/assets/bulk-import', body),
  searchFab:       (q, first)   => req('GET',    `/api/fab/search?q=${encodeURIComponent(q)}&first=${first || 6}`),
  searchFabSeller: (seller, q, first) => req('GET', `/api/fab/search-seller?seller=${encodeURIComponent(seller)}&q=${encodeURIComponent(q)}&first=${first || 5}`),
  fabPreview:      (url)        => req('GET',    `/api/fab/preview?url=${encodeURIComponent(url)}`),
  fabGlobalSearch: (q, limit)   => req('GET',    `/api/fab/global-search?q=${encodeURIComponent(q)}&limit=${limit || 5}`),

  // Stats dashboard
  getStats: () => req('GET', '/api/stats'),

  // Game Admin (proxy vers l'API OpenFramework)
  gameAdminStats:         ()          => req('GET',    '/api/gameadmin/stats'),
  gameAdminUsers:         ()          => req('GET',    '/api/gameadmin/users'),
  gameAdminUser:          (steamId)   => req('GET',    `/api/gameadmin/users/${encodeURIComponent(steamId)}`),
  gameAdminCharacters:    ()          => req('GET',    '/api/gameadmin/characters'),
  gameAdminPositions:     ()          => req('GET',    '/api/gameadmin/positions'),
  gameAdminAllItems:      (page = 1, pageSize = 200) => req('GET', `/api/gameadmin/items?page=${page}&pageSize=${pageSize}`),
  gameAdminAllTx:         (page = 1, pageSize = 200) => req('GET', `/api/gameadmin/transactions?page=${page}&pageSize=${pageSize}`),
  gameAdminCharacter:     (id)        => req('GET',    `/api/gameadmin/characters/${encodeURIComponent(id)}`),
  gameAdminCharacterInv:  (id)        => req('GET',    `/api/gameadmin/characters/${encodeURIComponent(id)}/inventory`),
  gameAdminCharacterAcc:  (id)        => req('GET',    `/api/gameadmin/characters/${encodeURIComponent(id)}/accounts`),
  gameAdminUpdateCharacter: (id, body) => req('PATCH',  `/api/gameadmin/characters/${encodeURIComponent(id)}`, body),
  gameAdminDeleteCharacter: (id, body) => req('DELETE', `/api/gameadmin/characters/${encodeURIComponent(id)}`, body),
  gameAdminAccountTx:     (accountId, page = 1, pageSize = 50) =>
    req('GET', `/api/gameadmin/accounts/${encodeURIComponent(accountId)}/transactions?page=${page}&pageSize=${pageSize}`),
  gameAdminBans:          ()          => req('GET',    '/api/gameadmin/bans'),
  gameAdminBan:           (body)      => req('POST',   '/api/gameadmin/bans', body),
  gameAdminUnban:         (steamId, body) => req('DELETE', `/api/gameadmin/bans/${encodeURIComponent(steamId)}`, body),
  gameAdminWhitelist:     ()          => req('GET',    '/api/gameadmin/whitelist'),
  gameAdminAddWhitelist:  (body)      => req('POST',   '/api/gameadmin/whitelist', body),
  gameAdminRemoveWhitelist:(steamId)  => req('DELETE', `/api/gameadmin/whitelist/${encodeURIComponent(steamId)}`),
  gameAdminWarns:         ()          => req('GET',    '/api/gameadmin/warns'),
  gameAdminGameAdmins:        ()          => req('GET',    '/api/gameadmin/game-admins'),
  gameAdminAddGameAdmin:      (body)      => req('POST',   '/api/gameadmin/game-admins', body),
  gameAdminRemoveGameAdmin:   (steamId)   => req('DELETE', `/api/gameadmin/game-admins/${encodeURIComponent(steamId)}`),
  gameAdminLogs:          (limit)     => req('GET',    `/api/gameadmin/logs${limit ? `?limit=${limit}` : ''}`),

  // Audit centralisé (sessions, chat, actions admin) — proxie vers l'API jeu
  gameAdminSessions:        (params = {}) => req('GET', `/api/gameadmin/sessions${qs(params)}`),
  gameAdminSessionsActive:  ()            => req('GET', '/api/gameadmin/sessions/active'),
  gameAdminPlaytime:        (params = {}) => req('GET', `/api/gameadmin/sessions/playtime${qs(params)}`),
  gameAdminChat:            (params = {}) => req('GET', `/api/gameadmin/chat${qs(params)}`),
  gameAdminAdminActions:    (params = {}) => req('GET', `/api/gameadmin/admin-actions${qs(params)}`),
  gameAdminInventoryLogs:   (params = {}) => req('GET', `/api/gameadmin/inventory-logs${qs(params)}`),
  gameAdminInventoryGive:   (body)                    => req('POST',   '/api/gameadmin/inventory/give', body),
  gameAdminInventoryModify: (itemId, body, charId)    => req('PATCH',  `/api/gameadmin/inventory/item/${encodeURIComponent(itemId)}`, charId ? { ...body, characterId: charId } : body),
  gameAdminInventoryDelete: (itemId, charId)          => req('DELETE', `/api/gameadmin/inventory/item/${encodeURIComponent(itemId)}${charId ? `?characterId=${encodeURIComponent(charId)}` : ''}`),
  gameAdminQueueCommand:    (body)        => req('POST', '/api/gameadmin/commands', body),
  gameAdminListCommands:    (params = {}) => req('GET', `/api/gameadmin/commands${qs(params)}`),
  gameAdminGetCommand:      (id)          => req('GET', `/api/gameadmin/commands/${encodeURIComponent(id)}`),
  gameAdminCriminalRecord:     (charId)        => req('GET',    `/api/gameadmin/criminal-record/${encodeURIComponent(charId)}`),
  gameAdminCriminalRecordAdd:  (charId, body)  => req('POST',   `/api/gameadmin/criminal-record/${encodeURIComponent(charId)}`, body),
  gameAdminCriminalRecordDel:  (charId, entryId) => req('DELETE', `/api/gameadmin/criminal-record/${encodeURIComponent(charId)}/${encodeURIComponent(entryId)}`),

  // Permissions
  getRoles:           ()                  => req('GET',    '/api/permissions/roles'),
  getPages:           ()                  => req('GET',    '/api/permissions/pages'),
  getPermissionsMatrix: ()                => req('GET',    '/api/permissions/matrix'),
  savePermissionsMatrix: (changes)        => req('PUT',    '/api/permissions/matrix', { changes }),
  createRole:         (body)              => req('POST',   '/api/permissions/roles', body),
  updateRole:         (key, body)         => req('PUT',    `/api/permissions/roles/${encodeURIComponent(key)}`, body),
  deleteRole:         (key, reassignTo)   => req('DELETE', `/api/permissions/roles/${encodeURIComponent(key)}`,
                                                 reassignTo ? { reassign_to: reassignTo } : undefined),
  getMyPermissions:   ()                  => req('GET',    '/api/permissions/me'),

  // Bugs
  getPublicBugs:  (game)      => req('GET',    `/api/bugs?game=${game}`),
  getAdminBugs:   (game)      => req('GET',    `/api/bugs/admin${game ? `?game=${game}` : ''}`),
  reportBug:      (body)      => req('POST',   '/api/bugs', body),
  patchBug:       (id, body)  => req('PATCH',  `/api/bugs/${id}`, body),
  deleteBug:      (id)        => req('DELETE', `/api/bugs/${id}`),
  addBugComment:  (id, body)  => req('POST',   `/api/bugs/${id}/comments`, body),
  deleteBugComment:(id, cid)  => req('DELETE', `/api/bugs/${id}/comments/${cid}`),
}
