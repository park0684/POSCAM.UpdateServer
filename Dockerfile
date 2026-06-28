FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY ["src/POSCAM.UpdateServer.Api/POSCAM.UpdateServer.Api.csproj", "src/POSCAM.UpdateServer.Api/"]
RUN dotnet restore "src/POSCAM.UpdateServer.Api/POSCAM.UpdateServer.Api.csproj"

COPY . .
RUN dotnet publish "src/POSCAM.UpdateServer.Api/POSCAM.UpdateServer.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

RUN mkdir -p \
        /app/update-storage/packages \
        /app/update-storage/.staging \
        /app/update-storage/.quarantine \
    && chown -R app:app /app

COPY --from=build --chown=app:app /app/publish .

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "POSCAM.UpdateServer.Api.dll"]
