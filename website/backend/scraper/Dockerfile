# L'image officielle a le parser Scrapling + Python 3.12 + Debian Trixie
# On ajoute : curl_cffi (pour les fetchers), camoufox (Firefox anti-bot),
# les dépendances système Firefox, FastAPI et uvicorn.
FROM pyd4vinci/scrapling:latest

# Dépendances système pour Camoufox (Firefox headless) — noms Debian Trixie
RUN apt-get update && apt-get install -y --no-install-recommends \
    libx11-xcb1 \
    libxcomposite1 \
    libxcursor1 \
    libxdamage1 \
    libxi6 \
    libxtst6 \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libgbm1 \
    libxrandr2 \
    libxkbcommon0 \
    libasound2 \
    libdbus-1-3 \
    libglib2.0-0 \
    libpango-1.0-0 \
    libpangocairo-1.0-0 \
    libcairo-gobject2 \
    libcairo2 \
    libgdk-pixbuf-xlib-2.0-0 \
    libgtk-3-0 \
    libfontconfig1 \
    fonts-liberation \
    ca-certificates \
    wget \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Installer les dépendances Python manquantes :
# - scrapling[fetchers] pour curl_cffi + camoufox
# - fastapi + uvicorn pour l'API
RUN pip install --no-cache-dir \
    "scrapling[fetchers]" \
    camoufox \
    fastapi \
    "uvicorn[standard]"

# Télécharger le binaire Camoufox au build (évite le download au démarrage)
RUN scrapling install

COPY main.py .

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    CAMOUFOX_NO_SANDBOX=1

EXPOSE 8000

ENTRYPOINT []
CMD ["python3", "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "1"]
