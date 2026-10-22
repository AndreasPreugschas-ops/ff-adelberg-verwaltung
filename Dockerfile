# Multi-Stage-Build: identisch für VPS und Gerätehaus-PC.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TAILWIND_VERSION=v4.1.14
# Commit für die Versionsanzeige; im Build-Kontext gibt es kein .git (siehe .dockerignore).
ARG SOURCE_REVISION=
WORKDIR /src

# Tailwind Standalone-Binary (kein Node nötig); der Build ruft es über das MSBuild-Target auf.
ADD --chmod=755 https://github.com/tailwindlabs/tailwindcss/releases/download/${TAILWIND_VERSION}/tailwindcss-linux-x64 /src/tools/tailwindcss

COPY global.json Directory.Build.props Directory.Packages.props Fevad.slnx ./
COPY src/Fevad.Domain/Fevad.Domain.csproj src/Fevad.Domain/
COPY src/Fevad.Infrastructure/Fevad.Infrastructure.csproj src/Fevad.Infrastructure/
COPY src/Fevad.Web/Fevad.Web.csproj src/Fevad.Web/
RUN dotnet restore src/Fevad.Web/Fevad.Web.csproj

COPY src/ src/
RUN dotnet publish src/Fevad.Web/Fevad.Web.csproj -c Release -o /app --no-restore -p:SourceRevisionId=${SOURCE_REVISION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
LABEL org.opencontainers.image.title="FEVAD" \
      org.opencontainers.image.description="FEVAD – Verwaltung und Dokumentation der Freiwilligen Feuerwehr Adelberg" \
      org.opencontainers.image.licenses="EUPL-1.2"
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Fevad.Web.dll"]
