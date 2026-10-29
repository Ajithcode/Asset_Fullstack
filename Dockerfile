# Builds and runs all 6 A360 services (5 backend services + ApiGateway) plus MongoDB
# inside a single Cloudflare Container instance. Mirrors scripts/start-all.sh.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src.sln

RUN dotnet publish ApiGateway/A360.ApiGateway/A360.ApiGateway.csproj -c Release -o /out/apigateway --no-restore && \
    dotnet publish Services/Media/Api/A360.Media.Api/A360.Media.Api.csproj -c Release -o /out/media --no-restore && \
    dotnet publish Services/Asset/Api/A360.Asset.Api/A360.Asset.Api.csproj -c Release -o /out/asset --no-restore && \
    dotnet publish Services/Inspection/Api/A360.Inspection.Api/A360.Inspection.Api.csproj -c Release -o /out/inspection --no-restore && \
    dotnet publish Services/EventLog/Api/A360.EventLog.Api/A360.EventLog.Api.csproj -c Release -o /out/eventlog --no-restore

FROM mongo:7.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget ca-certificates \
    && wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --channel 8.0 --runtime aspnetcore --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm -rf /tmp/dotnet-install.sh /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /out/apigateway ./apigateway
COPY --from=build /out/media ./media
COPY --from=build /out/asset ./asset
COPY --from=build /out/inspection ./inspection
COPY --from=build /out/eventlog ./eventlog

COPY start.sh /app/start.sh
RUN chmod +x /app/start.sh

EXPOSE 5296
ENTRYPOINT ["/app/start.sh"]
