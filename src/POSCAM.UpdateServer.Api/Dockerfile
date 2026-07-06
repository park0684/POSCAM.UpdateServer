FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore ./src/POSCAM.UpdateServer.Api/POSCAM.UpdateServer.Api.csproj
RUN dotnet publish ./src/POSCAM.UpdateServer.Api/POSCAM.UpdateServer.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "POSCAM.UpdateServer.Api.dll"]
