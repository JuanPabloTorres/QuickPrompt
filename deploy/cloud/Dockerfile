# Build from the repository root. Use the adjacent Dockerfile.dockerignore allowlist.
FROM node:22-bookworm-slim@sha256:de170b39439eb0e5a3444ad7d60b015614a381e10fecae2e30ec9e735122f38d AS web
WORKDIR /source/apps/web
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci --ignore-scripts
COPY apps/web/index.html apps/web/tsconfig.json apps/web/vite.config.ts ./
COPY apps/web/src ./src
COPY apps/web/public ./public
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS backend
WORKDIR /source
COPY global.json ./
COPY backend/QuickPrompt.Cloud ./backend/QuickPrompt.Cloud
COPY backend/QuickPrompt.Accounts ./backend/QuickPrompt.Accounts
COPY backend/QuickPrompt.Inference ./backend/QuickPrompt.Inference
COPY backend/QuickPrompt.Application ./backend/QuickPrompt.Application
COPY src/QuickPrompt.Domain ./src/QuickPrompt.Domain
COPY apps/mobile/Domain/Entities ./apps/mobile/Domain/Entities
COPY apps/mobile/Domain/Enums ./apps/mobile/Domain/Enums
RUN dotnet restore backend/QuickPrompt.Cloud/QuickPrompt.Cloud.csproj --locked-mode
RUN dotnet publish backend/QuickPrompt.Cloud/QuickPrompt.Cloud.csproj -c Release --no-restore -o /published /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS dotnet-runtime

# Native source-language observations run inside this host; no nested Docker or model runtime.
FROM debian@sha256:1e1a22175fbf1b32157ddaf0219e1db4e5401fab2b94ed285add64d18587cd2d AS runtime
ENV LANG=C.UTF-8 LC_ALL=C.UTF-8 DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
    apertium=3.9.12-1 apertium-eng-spa=0.8.1-2 apertium-es-pt=1.1.6-1 lttoolbox=3.8.0-2 liblttoolbox3=3.8.0-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=dotnet-runtime /usr/share/dotnet /usr/share/dotnet
COPY --from=dotnet-runtime /etc/ssl/certs /etc/ssl/certs
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH HOME=/tmp
WORKDIR /app
COPY --from=backend /published ./
COPY --from=web /source/apps/web/dist ./wwwroot
COPY --chmod=755 deploy/cloud/entrypoint.sh /app/entrypoint.sh
ENV ASPNETCORE_ENVIRONMENT=Production DOTNET_EnableDiagnostics=0
USER 1654:1654
EXPOSE 10000
ENTRYPOINT ["/app/entrypoint.sh"]
