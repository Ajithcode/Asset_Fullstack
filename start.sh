#!/usr/bin/env bash
# Starts MongoDB + all A360 services + the ApiGateway inside a single container.
# Mirrors scripts/start-all.sh, adapted to run as the container's entrypoint.
set -euo pipefail

mkdir -p /data/db
mongod --dbpath /data/db --logpath /var/log/mongod.log --bind_ip 127.0.0.1 \
  --wiredTigerCacheSizeGB 0.5 &

for i in $(seq 1 30); do
  if (exec 3<>/dev/tcp/127.0.0.1/27017) 2>/dev/null; then
    exec 3<&- 3>&-
    break
  fi
  sleep 1
done

declare -A SERVICES=(
  ["media"]="/app/media:A360.Media.Api.dll:5300"
  ["asset"]="/app/asset:A360.Asset.Api.dll:5401"
  ["inspection"]="/app/inspection:A360.Inspection.Api.dll:5410"
  ["eventlog"]="/app/eventlog:A360.EventLog.Api.dll:5420"
)

for name in "${!SERVICES[@]}"; do
  IFS=':' read -r dir dll port <<< "${SERVICES[$name]}"
  ( cd "$dir" && ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="http://0.0.0.0:${port}" \
    dotnet "$dll" >> "/var/log/${name}.log" 2>&1 & )
  echo "started $name on port $port"
done

sleep 3

cd /app/apigateway
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS="http://0.0.0.0:5296"
exec dotnet A360.ApiGateway.dll
