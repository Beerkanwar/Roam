# Roam web app: builds the React client and serves it with nginx, which also proxies the API
# (/api, /hubs and the sign-in endpoints) so browser and API share one origin (no CORS, one domain).
FROM node:22-alpine AS build
WORKDIR /app

COPY src/Roam.Web/package.json src/Roam.Web/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY src/Roam.Web/ ./
# Empty API base URL = call the API on the same origin. A MapTiler key is optional (OpenFreeMap otherwise).
ARG VITE_MAP_API_KEY=
ENV VITE_API_BASE_URL="" \
    VITE_MAP_API_KEY=${VITE_MAP_API_KEY}
RUN npm run build

FROM nginx:1.27.5-alpine
COPY infrastructure/nginx/web.conf /etc/nginx/nginx.conf
COPY infrastructure/nginx/roam_proxy_params /etc/nginx/roam_proxy_params
COPY --chmod=755 infrastructure/nginx/40-legal-placeholders.sh /docker-entrypoint.d/40-legal-placeholders.sh
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1
