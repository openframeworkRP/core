import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api } from './api.js'
import AdminPostEditor from './AdminPostEditor.jsx'

export default function AdminPostEditorRoute() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [games, setGames] = useState([])
  const [users, setUsers] = useState([])
  const [ready, setReady] = useState(false)

  useEffect(() => {
    Promise.all([api.getGames(), api.getUsers().catch(() => [])])
      .then(([g, u]) => { setGames(g); setUsers(u) })
      .finally(() => setReady(true))
  }, [])

  if (!ready) {
    return (
      <div className="adm__loader">
        <div className="adm__spinner" />
        <span>Chargement…</span>
      </div>
    )
  }

  return (
    <AdminPostEditor
      postId={id ? Number(id) : null}
      games={games}
      users={users}
      onSave={() => navigate('/admin')}
      onBack={() => navigate('/admin')}
    />
  )
}
