#!/usr/bin/env bash
# Déploie l'application derrière nginx-ui (Docker) et installe fail2ban.
# À lancer sur le VPS, depuis le dépôt :   sudo ./deploy/deploy.sh --domain prediction.exemple.com
#
# Options (ou variables d'environnement équivalentes) :
#   --domain D          nom de domaine public (obligatoire)                          DOMAIN
#   --cert-domain D     dossier Let's Encrypt (défaut : domaine parent de --domain)  CERT_DOMAIN
#   --nginx-container C conteneur nginx-ui (défaut : nginx-ui)                       NGINX_CONTAINER
#   --conf-dir P        conf.d de nginx-ui sur l'hôte (défaut : /opt/nginx-ui/nginx/conf.d)  CONF_DIR
#   --log-dir P         logs nginx sur l'hôte (défaut : /opt/nginx-ui/nginx/logs)    LOG_DIR
#   --ignoreip "IPS"    IP jamais bannies par fail2ban (facultatif)                  IGNORE_IP
#   --skip-app | --skip-nginx | --skip-fail2ban
set -euo pipefail

DOMAIN="${DOMAIN:-}"
CERT_DOMAIN="${CERT_DOMAIN:-}"
NGINX_CONTAINER="${NGINX_CONTAINER:-nginx-ui}"
CONF_DIR="${CONF_DIR:-/opt/nginx-ui/nginx/conf.d}"
LOG_DIR="${LOG_DIR:-/opt/nginx-ui/nginx/logs}"
IGNORE_IP="${IGNORE_IP:-}"
SKIP_APP=0; SKIP_NGINX=0; SKIP_F2B=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --domain) DOMAIN="$2"; shift 2;;
    --cert-domain) CERT_DOMAIN="$2"; shift 2;;
    --nginx-container) NGINX_CONTAINER="$2"; shift 2;;
    --conf-dir) CONF_DIR="$2"; shift 2;;
    --log-dir) LOG_DIR="$2"; shift 2;;
    --ignoreip) IGNORE_IP="$2"; shift 2;;
    --skip-app) SKIP_APP=1; shift;;
    --skip-nginx) SKIP_NGINX=1; shift;;
    --skip-fail2ban) SKIP_F2B=1; shift;;
    -h|--help) sed -n '2,15p' "$0"; exit 0;;
    *) echo "Option inconnue : $1" >&2; exit 2;;
  esac
done

[[ -n "$DOMAIN" ]] || { echo "--domain est obligatoire" >&2; exit 2; }
[[ $EUID -eq 0 ]] || { echo "À lancer avec sudo (écriture dans /etc et $CONF_DIR)." >&2; exit 1; }
CERT_DOMAIN="${CERT_DOMAIN:-${DOMAIN#*.}}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
SERVICE=web            # service du docker-compose.yml
CONTAINER=football-prediction
PORT=7575
LOGFILE="$LOG_DIR/prediction-access.log"

step() { printf '\n==> %s\n' "$*"; }

# ── 1. Application ───────────────────────────────────────────
if [[ $SKIP_APP -eq 0 ]]; then
  step "Réseau Docker de $NGINX_CONTAINER"
  docker inspect "$NGINX_CONTAINER" >/dev/null 2>&1 || { echo "Conteneur $NGINX_CONTAINER introuvable." >&2; exit 1; }
  NETWORK="$(docker inspect "$NGINX_CONTAINER" --format '{{range $k,$v := .NetworkSettings.Networks}}{{$k}}{{"\n"}}{{end}}' | head -n1)"
  echo "Réseau : $NETWORK"

  step "docker-compose.override.yml (rattachement au réseau, port 7575 non publié)"
  [[ -f docker-compose.override.yml ]] && cp docker-compose.override.yml "docker-compose.override.yml.bak.$(date +%s)"
  cat > docker-compose.override.yml <<YML
# Généré par deploy/deploy.sh
services:
  $SERVICE:
    ports: !reset []
    networks: [default, proxy]
networks:
  proxy:
    external: true
    name: $NETWORK
YML

  step "Build et démarrage"
  # Version affichée dans l'interface : commit court (+ -dirty si modifications locales) et date du commit
  export GIT_SHA="$(git rev-parse --short HEAD)$(git diff --quiet HEAD -- 2>/dev/null || echo -dirty)"
  export BUILD_DATE="$(git log -1 --format=%cd --date=short)"
  echo "Version : $GIT_SHA ($BUILD_DATE)"
  docker compose up -d --build
fi

# ── 2. nginx-ui ──────────────────────────────────────────────
if [[ $SKIP_NGINX -eq 0 ]]; then
  step "Configuration nginx pour $DOMAIN"
  mkdir -p "$CONF_DIR"
  TARGET="$CONF_DIR/prediction.conf"
  # Sauvegarde HORS de conf.d : nginx-ui y charge tous les fichiers, un .bak y dupliquerait les limit_req_zone.
  BACKUP_DIR=/var/backups/prediction-deploy
  BACKUP="$BACKUP_DIR/prediction.conf.bak"
  mkdir -p "$BACKUP_DIR"
  rm -f "$BACKUP"
  [[ -f "$TARGET" ]] && cp "$TARGET" "$BACKUP"
  sed -e "s#prediction\.dbdesignandcodereview\.com#$DOMAIN#g" \
      -e "s#/etc/letsencrypt/live/dbdesignandcodereview\.com/#/etc/letsencrypt/live/$CERT_DOMAIN/#g" \
      -e "s#http://football-prediction:7575#http://$CONTAINER:$PORT#" \
      deploy/nginx-ui/prediction.conf > "$TARGET"

  if docker exec "$NGINX_CONTAINER" nginx -t; then
    docker exec "$NGINX_CONTAINER" nginx -s reload
    echo "nginx rechargé."
  else
    echo "nginx -t a échoué : restauration de l'ancienne configuration." >&2
    if [[ -f "$BACKUP" ]]; then mv "$BACKUP" "$TARGET"; else rm -f "$TARGET"; fi
    exit 1
  fi

  step "Test de santé"
  sleep 3
  if curl -ksS --max-time 10 --resolve "$DOMAIN:443:127.0.0.1" -o /dev/null -w 'HTTP %{http_code}\n' "https://$DOMAIN/api/health"; then :; else
    echo "Pas de réponse (l'application démarre peut-être encore, ou le réseau Docker est incorrect)." >&2
  fi
fi

# ── 3. fail2ban ──────────────────────────────────────────────
if [[ $SKIP_F2B -eq 0 ]]; then
  step "fail2ban"
  command -v fail2ban-client >/dev/null || { apt-get update -qq && apt-get install -y -qq fail2ban; }
  install -m 644 deploy/fail2ban/filter-prediction-auth.conf /etc/fail2ban/filter.d/prediction-auth.conf
  install -m 644 deploy/fail2ban/filter-prediction-scan.conf /etc/fail2ban/filter.d/prediction-scan.conf
  sed -e "s#/opt/nginx-ui/nginx/logs/prediction-access.log#$LOGFILE#g" \
      deploy/fail2ban/jail-prediction.local > /etc/fail2ban/jail.d/prediction.local
  if [[ -n "$IGNORE_IP" ]]; then
    sed -i "s#^\# ignoreip = .*#ignoreip = 127.0.0.1/8 ::1 $IGNORE_IP#" /etc/fail2ban/jail.d/prediction.local
  fi
  # fail2ban refuse de démarrer un jail dont le log n'existe pas encore
  mkdir -p "$LOG_DIR"; touch "$LOGFILE"
  fail2ban-client -t >/dev/null
  systemctl enable --now fail2ban >/dev/null 2>&1 || true
  fail2ban-client reload
  fail2ban-client status prediction-auth || true
  fail2ban-client status prediction-scan || true
fi

step "Terminé : https://$DOMAIN"
