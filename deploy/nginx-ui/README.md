# Exposition via nginx-ui

1. Enregistrer un DNS `prediction.<domaine>` vers le VPS (ou utiliser le certificat wildcard existant).
2. Rattacher le conteneur au réseau Docker de nginx-ui (le `resolver 127.0.0.11` résout les noms de conteneurs).
   Trouver le réseau : `docker inspect nginx-ui --format '{{json .NetworkSettings.Networks}}'`
   Dans `docker-compose.yml` :
   ```yaml
   services:
     web:
       networks: [default, proxy]
       ports: []          # ne plus publier 7575 sur l'hôte
   networks:
     proxy:
       external: true
       name: <réseau_de_nginx-ui>
   ```
3. Copier `prediction.conf` dans `/opt/nginx-ui/nginx/conf.d/`, puis recharger (`nginx -t && nginx -s reload` dans le conteneur, ou via l'UI).
4. Vérifier : `curl -I https://prediction.<domaine>/api/health`

## Fail2ban
Voir `../fail2ban/` : deux filtres (`prediction-auth`, `prediction-scan`) et un jail.
`sudo fail2ban-regex /opt/nginx-ui/nginx/logs/prediction-access.log prediction-auth` permet de tester les filtres sur les logs réels.
