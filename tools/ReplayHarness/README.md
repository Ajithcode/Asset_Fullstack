# ReplayHarness (offline, read-only)

Replays stored readings through the worker's real `TagLocationResolver` (same source file, no Redis, no writes) with
different Location settings, and analyses the real zone switches of the running worker.

```
URI=$(docker exec asset360-dev-administration-1 printenv MongoDb__ConnectionString | sed 's/host.docker.internal/127.0.0.1/')
docker run --rm --network host -e MONGO_URI="$URI" -e MONGO_DB=Asset360Dev -v /opt/asset360-dev/worker-service:/src \
  mcr.microsoft.com/dotnet/sdk:8.0 sh -c 'cp -r /src /b && cd /b/tools/ReplayHarness && dotnet run -c Release -- replay --from 2026-11-26T11:17:00Z'
... -- live --from 2026-11-26T11:50:30Z      # real switches of the running worker, with RSSI per window
```
Never prints the connection string. Reads GatewayTimeSeries (raw readings, tags only), NetworkTimeSeries (real out docs), maps_plots.
Reference "actual move" = first window where one zone's best device is >= 6 dB over every other zone's (carry-forward 30 s) and
that zone stays dominant for the next 45 s (>= 2 determined windows). Delay = switch window - that time.

## fast mode (fast-lane replay) and shadow comparison
`export-fast-lane.py > entries.jsonl` dumps the Redis fast lane (read-only, ~20k entries); `dotnet run -c Release -- fast --file entries.jsonl [--sets sets.json] [--truth truth.csv] [--per-tag]`
replays it through the real `FastLocationEngine` for a grid (tau 2/3 s x margin 6/8/10 dB x dwell 1/2 s) or your sets (JSON array of FastParams) and prints
switches, flips (A->B->A within 60 s), signal loss and, with a ground-truth CSV (tag,zoneId,tapMs), the delay tap -> switch.
`shadow-compare.js` (mongosh) compares the shadow log of every variant with the 15 s resolver per trolley (switches/flips, seconds earlier).
