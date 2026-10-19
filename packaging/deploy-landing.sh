#!/usr/bin/env bash
# Publishes the project's own landing (a public-site instance) to the operator's server:
# the built frontend shells, the published API, a service restart, and a check that the
# deployment actually serves what it was supposed to. The host details live in
# packaging/deploy-landing.env (gitignored, see deploy-landing.env.example).
#
# The two failure modes this script exists to catch - both shipped once, in October 2026:
#
#   - a publish from a mac carries only the darwin Playwright node driver (MSBuild picks the
#     driver of the build platform), so the deployed renderer cannot start Chromium on a
#     Linux server and every link preview degrades to the static brand card. The publish
#     below passes -p:PlaywrightPlatform=linux-x64; the Dockerfile needs neither, it builds
#     inside the target image.
#
#   - the service's env file (systemd EnvironmentFile, outside the app directory) rewritten
#     without ChartImage__PageHtmlPath silently switches the renderer off - appsettings.json
#     ships it null, only the Dockerfile's ENV sets it. The script keeps the line and, after
#     the restart, requires the shared picture to be a real render rather than the brand
#     redirect.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
conf="$root/packaging/deploy-landing.env"

if [[ ! -f $conf ]]; then
  echo "deploy-landing: packaging/deploy-landing.env is missing - copy packaging/deploy-landing.env.example and fill in the host" >&2
  exit 1
fi
# shellcheck source=/dev/null
source "$conf"

for what in SI_DEPLOY_TARGET SI_DEPLOY_DIR SI_DEPLOY_SERVICE SI_SERVICE_ENV_FILE SI_SEO_BASE_URL; do
  if [[ -z ${!what:-} ]]; then
    echo "deploy-landing: $what is not set in packaging/deploy-landing.env" >&2
    exit 1
  fi
done

ssh_port=${SI_DEPLOY_SSH_PORT:-22}
ssh_cmd=(ssh -p "$ssh_port" -o BatchMode=yes "$SI_DEPLOY_TARGET")
publish="$root/artifacts/landing-publish"

echo "deploy-landing: building the frontend (SEO_BASE_URL=$SI_SEO_BASE_URL)"
[[ -d $root/web/node_modules ]] || (cd "$root/web" && npm ci --no-audit --no-fund)
# SITE_LANDING_ONLY: nginx serves these shells, so the backend's runtime stamp never reaches
# them - the landing build bakes landingOnly=true into every shell instead (gen-seo-pages).
(cd "$root/web" && SEO_BASE_URL="$SI_SEO_BASE_URL" SITE_LANDING_ONLY=1 npm run build >/dev/null)
(cd "$root/web" && SEO_BASE_URL="$SI_SEO_BASE_URL" npm run check:seo)

echo "deploy-landing: publishing the API for linux-x64"
dotnet publish "$root/src/StockIndicators.API" -c Release -o "$publish" -p:PlaywrightPlatform=linux-x64 -v q --nologo

driver="$publish/.playwright/node/linux-x64/node"
if [[ ! -f $driver ]]; then
  echo "deploy-landing: the publish carries no linux-x64 Playwright driver ($driver); the server's renderer would not start" >&2
  exit 1
fi

echo "deploy-landing: uploading to $SI_DEPLOY_TARGET:$SI_DEPLOY_DIR"
# --delete keeps the directory a mirror of this build (an old dll left behind is a dll that
# still loads); wwwroot is the frontend step below, the Local file is the server's own layer.
rsync -a --delete -e "ssh -p $ssh_port" \
  --exclude wwwroot/ --exclude appsettings.Local.json \
  "$publish/" "$SI_DEPLOY_TARGET:$SI_DEPLOY_DIR/"
# The frontend lives in two places on this host: nginx serves the static site from its own
# root (only /api/ reaches the service), while the service needs dist for its wwwroot and
# for the renderer page ChartImage__PageHtmlPath points at. Both get the same build; an
# install whose service serves the site itself leaves SI_WEB_ROOT unset (the wwwroot step
# is then the whole story - point it at the very same wwwroot and it stays a no-op).
rsync -a --delete -e "ssh -p $ssh_port" \
  "$root/web/dist/" "$SI_DEPLOY_TARGET:$SI_DEPLOY_DIR/wwwroot/"
if [[ -n ${SI_WEB_ROOT:-} && $SI_WEB_ROOT != "$SI_DEPLOY_DIR/wwwroot" ]]; then
  rsync -a --delete -e "ssh -p $ssh_port" \
    "$root/web/dist/" "$SI_DEPLOY_TARGET:$SI_WEB_ROOT/"
fi

echo "deploy-landing: restarting $SI_DEPLOY_SERVICE"
"${ssh_cmd[@]}" \
  "grep -q '^ChartImage__PageHtmlPath=' '$SI_SERVICE_ENV_FILE' || echo 'ChartImage__PageHtmlPath=$SI_DEPLOY_DIR/wwwroot/render/renderer.html' >> '$SI_SERVICE_ENV_FILE'"
"${ssh_cmd[@]}" "systemctl restart '$SI_DEPLOY_SERVICE' && sleep 6 && systemctl is-active '$SI_DEPLOY_SERVICE'"

echo "deploy-landing: verifying the public surface"
if [[ $(curl -s -m 20 -o /dev/null -w '%{http_code}' "$SI_SEO_BASE_URL/") != 200 ]]; then
  echo "deploy-landing: $SI_SEO_BASE_URL/ does not answer 200 after the restart" >&2
  exit 1
fi
# The picture this deployment exists to serve: a 302 here is the brand fallback, which means
# the renderer is off again (journalctl -u $SI_DEPLOY_SERVICE will say which of the causes).
if [[ $(curl -s -m 60 -o /dev/null -w '%{http_code}' "$SI_SEO_BASE_URL/api/share/landing/image.png?lang=en&deploy=$(date +%s)") != 200 ]]; then
  echo "deploy-landing: the shared picture answers the brand fallback, not a render - check: ssh -p $ssh_port $SI_DEPLOY_TARGET journalctl -u $SI_DEPLOY_SERVICE -n 50" >&2
  exit 1
fi

echo "deploy-landing: $SI_DEPLOY_SERVICE is live and the link-preview picture renders"
