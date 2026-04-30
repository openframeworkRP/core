FROM node:20-slim

# Dépendances système : curl + ffmpeg + Chromium pour Puppeteer + libvips pour sharp
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    ffmpeg \
    chromium \
    ca-certificates \
    fonts-liberation \
    libnss3 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libgbm1 \
    libgtk-3-0 \
    libxcomposite1 \
    libxdamage1 \
    libxrandr2 \
    xdg-utils \
    libvips-dev \
    python3 \
    make \
    g++ \
    && rm -rf /var/lib/apt/lists/*

ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true \
    PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

WORKDIR /app

COPY package.json .
RUN npm install --production

COPY src ./src

VOLUME ["/app/data", "/app/uploads"]

EXPOSE 3001

CMD ["node", "src/index.js"]
