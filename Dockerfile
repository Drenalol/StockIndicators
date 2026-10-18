# syntax=docker/dockerfile:1

# Single image for a self-hosted instance: the .NET API and the built frontend behind it,
# plus the headless Chromium the alert chart images are rendered with.
# Build context is the repository root (compose: packaging/docker-compose.yml).

# --- frontend: SPA, per-locale SEO shells, and the chart renderer page (dist/render) ---
FROM node:24-alpine AS web
WORKDIR /web
# npm ci from the lockfile only - the layer rebuilds only when dependencies change
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
# SEO_BASE_URL bakes absolute canonical/og URLs into the shells; the default (relative to
# the copy.json baseUrl) is fine for a LAN instance, override at build time for a domain:
#   docker compose build --build-arg SEO_BASE_URL=https://your.domain
# Empty stays "not set" (the scripts fall back to copy.json), so an unset arg is harmless.
ARG SEO_BASE_URL
ENV SEO_BASE_URL=$SEO_BASE_URL
RUN npm run build

# --- backend: publish the API (the whole src/ tree, sibling project references) ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./
RUN dotnet publish StockIndicators.API/StockIndicators.API.csproj -c Release -o /app/publish

# --- runtime: aspnet on Ubuntu Noble + the Chromium build matching Microsoft.Playwright ---
# The browser registry is copied from the official Playwright image of the same version as
# the NuGet package (Infrastructure csproj); the apt list below is its chromium dependency
# set, so the binary starts on the plain aspnet runtime base.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      libasound2t64 \
      libatk-bridge2.0-0t64 \
      libatk1.0-0t64 \
      libatspi2.0-0t64 \
      libcairo2 \
      libcups2t64 \
      libdbus-1-3 \
      libdrm2 \
      libgbm1 \
      libglib2.0-0t64 \
      libnspr4 \
      libnss3 \
      libpango-1.0-0 \
      libx11-6 \
      libxcb1 \
      libxcomposite1 \
      libxdamage1 \
      libxext6 \
      libxfixes3 \
      libxkbcommon0 \
      libxrandr2 \
      # real fonts for the rendered chart labels (Cyrillic included)
      fonts-dejavu-core \
      fonts-liberation && \
    rm -rf /var/lib/apt/lists/*

COPY --from=mcr.microsoft.com/playwright/dotnet:v1.63.0-noble /ms-playwright /ms-playwright
RUN chmod -R a+rX /ms-playwright

WORKDIR /app
COPY --from=build /app/publish .
COPY --from=web /web/dist ./wwwroot

# Alert images render over the bundled renderer page; point ChartImage__PageHtmlPath
# somewhere else (or unset it) to move/disable the pictures
ENV ChartImage__PageHtmlPath=/app/wwwroot/render/renderer.html \
    ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "StockIndicators.API.dll"]
