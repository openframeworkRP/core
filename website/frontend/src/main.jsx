import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Routes, Route, useLocation, Outlet } from 'react-router-dom'
import { HelmetProvider } from 'react-helmet-async'
import { useEffect } from 'react'
import './index.css'
import App from './App.jsx'
import DevBlogLayout from './components/devblog/DevBlogLayout.jsx'
import DevBlogList from './components/devblog/DevBlogList.jsx'
import DevBlogPost from './components/devblog/DevBlogPost.jsx'
import AdminApp from './admin/AdminApp.jsx'
import AdminLogin from './admin/AdminLogin.jsx'
import AdminGuard from './admin/AdminGuard.jsx'
import AdminPostEditorRoute from './admin/AdminPostEditorRoute.jsx'
import ControlPanel from './admin/ControlPanel.jsx'
import GamePage from './components/GamePage.jsx'
import RulesLibrary from './components/RulesLibrary.jsx'
import RulesBook from './components/RulesBook.jsx'
import TeamPage from './components/TeamPage.jsx'
import MembersPage from './components/MembersPage.jsx'
import VideoPage from './components/VideoPage.jsx'
import ImagePage from './components/ImagePage.jsx'
import RoadmapPage from './components/RoadmapPage.jsx'
import { LanguageProvider } from './context/LanguageContext.jsx'
import { AuthProvider } from './context/AuthContext.jsx'
import { PostTitleProvider } from './context/PostTitleContext.jsx'
import MaintenancePage from './components/MaintenancePage.jsx'
import SetupGate from './setup/SetupGate.jsx'

const IS_MAINTENANCE = import.meta.env.VITE_MAINTENANCE === 'true'

function ScrollToTop() {
  const { pathname } = useLocation()
  useEffect(() => { window.scrollTo(0, 0) }, [pathname])
  return null
}

function renderApp() {
  createRoot(document.getElementById('root')).render(
  <StrictMode>
    <HelmetProvider>
      <LanguageProvider>
        <AuthProvider>
        <SetupGate>
        <BrowserRouter>
          <ScrollToTop />
          <PostTitleProvider>
          <Routes>
            {/* ── Login admin (toujours accessible) ── */}
            <Route path="/admin/login" element={<AdminLogin />} />

            {/* ── Admin protégé par Steam (toujours accessible) ── */}
            <Route path="/admin" element={<AdminGuard><Outlet /></AdminGuard>}>
              <Route index element={<AdminApp />} />
              <Route path="panel/:panelId" element={<AdminApp />} />
              <Route path="hub/:hubView" element={<AdminApp />} />
              <Route path="hub/:hubView/:taskId" element={<AdminApp />} />
              <Route path="new" element={<AdminPostEditorRoute />} />
              <Route path="edit/:id" element={<AdminPostEditorRoute />} />
              <Route path="control" element={<ControlPanel />} />
            </Route>

            {IS_MAINTENANCE ? (
              /* ── MODE MAINTENANCE : toutes les autres routes → MaintenancePage ── */
              <Route path="*" element={<MaintenancePage />} />
            ) : (
              <>
            {/* ── Page principale ── */}
            <Route path="/" element={<App />} />

            {/* ── Pages DevBlog ── */}
            <Route path="/devblog" element={<DevBlogLayout />}>
              <Route index element={<DevBlogList />} />
              <Route path=":slug" element={<DevBlogPost />} />
            </Route>

            {/* ── Pages Jeux ── */}
            <Route path="/game/:slug" element={<GamePage />} />

            {/* ── Règlements OpenFramework ── */}
            <Route path="/game/small-life/rules" element={<RulesLibrary />} />
            <Route path="/game/small-life/rules/:bookId" element={<RulesBook />} />
            <Route path="/game/small-life/rules/:bookId/:chapterId" element={<RulesBook />} />

            {/* ── Roadmap publique ── */}
            <Route path="/roadmap" element={<RoadmapPage />} />

            {/* ── Page équipe ── */}
            <Route path="/team" element={<TeamPage />} />

            {/* ── Page membres ── */}
            <Route path="/members" element={<MembersPage />} />

            {/* ── Pages vidéos ── */}
            <Route path="/v/:slug" element={<VideoPage />} />

            {/* ── Pages images ── */}
            <Route path="/i/:slug" element={<ImagePage />} />
              </>
            )}
          </Routes>
          </PostTitleProvider>
        </BrowserRouter>
        </SetupGate>
      </AuthProvider>
    </LanguageProvider>
    </HelmetProvider>
  </StrictMode>,
  )
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', renderApp)
} else {
  renderApp()
}
