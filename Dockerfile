# Multi-Stage-Build: identisch für VPS und Gerätehaus-PC.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TAILWIND_VERSION=v4.1.14
WORKDIR /src

# Tailwind Standalone-Binary (kein Node nötig); der Build ruft es über das MSBuild-Target auf.
ADD --chmod=755 https://github.com/tailwindlabs/tailwindcss/releases/download/${TAILWIND_VERSION}/tailwindcss-linux-x64 /src/tools/tailwindcss

COPY global.json Directory.Build.props Directory.Packages.props FfAdelberg.slnx ./
COPY src/FfAdelberg.Domain/FfAdelberg.Domain.csproj src/FfAdelberg.Domain/
COPY src/FfAdelberg.Infrastructure/FfAdelberg.Infrastructure.csproj src/FfAdelberg.Infrastructure/
COPY src/FfAdelberg.Web/FfAdelberg.Web.csproj src/FfAdelberg.Web/
RUN dotnet restore src/FfAdelberg.Web/FfAdelberg.Web.csproj

COPY src/ src/
RUN dotnet publish src/FfAdelberg.Web/FfAdelberg.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FfAdelberg.Web.dll"]
