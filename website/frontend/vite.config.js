import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api':     { target: 'http://localhost:3001', changeOrigin: true },
      '/auth':    { target: 'http://localhost:3001', changeOrigin: true },
      '/uploads': { target: 'http://localhost:3001', changeOrigin: true },
      '/og':      { target: 'http://localhost:3001', changeOrigin: true },
      // Socket.io — WebSocket + polling
      '/socket.io': {
        target: 'http://localhost:3001',
        changeOrigin: true,
        ws: true, // active le proxy WebSocket
      },
    },
  },
  preview: {
    host: '0.0.0.0',
    port: 4173,
    allowedHosts: ['openframework.fr', 'www.openframework.fr'],
    proxy: {
      '/api':     { target: 'http://api:3001', changeOrigin: true, headers: { 'X-Forwarded-Proto': 'https' } },
      '/auth':    {
        target: 'http://api:3001',
        changeOrigin: true,
        cookieDomainRewrite: { '*': '' },
        headers: { 'X-Forwarded-Proto': 'https' },
      },
      '/uploads': { target: 'http://api:3001', changeOrigin: true },
      '/og':      { target: 'http://api:3001', changeOrigin: true },
      // Socket.io — WebSocket + polling
      '/socket.io': {
        target: 'http://api:3001',
        changeOrigin: true,
        ws: true,
        headers: { 'X-Forwarded-Proto': 'https' },
      },
    },
  },
})
