FROM node:20-alpine AS builder

WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm install

COPY . .

# Variables Vite injectées au moment du build (pas disponibles à runtime)
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL

ARG VITE_MAINTENANCE=false
ENV VITE_MAINTENANCE=$VITE_MAINTENANCE

RUN npm run build

# ── Stage de production ──────────────────────────────────────────────────────
FROM node:20-alpine

WORKDIR /app

# Copier uniquement ce dont server.js a besoin à runtime
COPY package.json package-lock.json* ./
RUN npm install --omit=dev

# Copier le build Vite et le serveur
COPY --from=builder /app/dist ./dist
COPY server.js ./

EXPOSE 4173

CMD ["node", "server.js"]
