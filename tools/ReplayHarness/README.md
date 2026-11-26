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
