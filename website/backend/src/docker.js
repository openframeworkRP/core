// ============================================================
// Helper pour parler au daemon Docker via le socket Unix.
// Pas de dependance externe : http natif sur /var/run/docker.sock.
// Le socket doit etre monte dans le container (cf. docker-compose.yml).
// ============================================================

import http from 'http'

const SOCKET = '/var/run/docker.sock'

export function dockerRequest(path, method = 'GET', body = null) {
  return new Promise((resolve, reject) => {
    const options = {
      socketPath: SOCKET,
      path,
      method,
      headers: { 'Content-Type': 'application/json' },
    }
    const req = http.request(options, (res) => {
      let data = ''
      res.on('data', (chunk) => (data += chunk))
      res.on('end', () => resolve({ status: res.statusCode, body: data }))
    })
    req.on('error', reject)
    if (body) req.write(JSON.stringify(body))
    req.end()
  })
}

export async function dockerJson(path) {
  const r = await dockerRequest(path)
  try { return { status: r.status, data: JSON.parse(r.body) } }
  catch { return { status: r.status, data: null } }
}

export async function inspectContainer(containerName) {
  const { status, data } = await dockerJson(`/containers/${containerName}/json`)
  if (status !== 200 || !data) return null
  return {
    state:     data.State?.Status     || 'unknown',
    running:   data.State?.Running    || false,
    startedAt: data.State?.StartedAt  || null,
    health:    data.State?.Health?.Status || null,
    image:     data.Config?.Image     || null,
    restartCount: data.RestartCount   || 0,
  }
}

export async function restartContainer(containerName, timeoutSec = 10) {
  return dockerRequest(`/containers/${containerName}/restart?t=${timeoutSec}`, 'POST')
}

export async function startContainer(containerName) {
  return dockerRequest(`/containers/${containerName}/start`, 'POST')
}

export async function stopContainer(containerName, timeoutSec = 10) {
  return dockerRequest(`/containers/${containerName}/stop?t=${timeoutSec}`, 'POST')
}

export async function containerLogs(containerName, tail = 100) {
  // Endpoint logs renvoie un stream multiplexe (stdout/stderr) avec un
  // header binaire de 8 octets par chunk. Pour la simplicite, on demande
  // sans le multiplexage via tty=false + stdout=true&stderr=true et on
  // strippe le header. Pour usage admin uniquement.
  const r = await dockerRequest(
    `/containers/${containerName}/logs?stdout=true&stderr=true&tail=${tail}&timestamps=true`,
    'GET',
  )
  // Strip Docker stream headers (8 bytes : type|0|0|0|size_be32) si presents
  const cleaned = r.body
    .split('\n')
    .map(line => {
      // Si le 1er char est non-imprimable (header binaire), on saute les 8 premiers bytes
      if (line.length > 8 && line.charCodeAt(0) < 32 && line.charCodeAt(0) !== 9) {
        return line.substring(8)
      }
      return line
    })
    .join('\n')
  return { status: r.status, body: cleaned }
}
