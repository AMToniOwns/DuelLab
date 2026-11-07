# syntax=docker/dockerfile:1

# One image: the ASP.NET Core API also serves the built Vue app from wwwroot, so the browser talks
# to a single origin and there is nothing else to deploy besides PostgreSQL.

# ---- Frontend build ---------------------------------------------------------------------------
FROM node:22-alpine AS web
WORKDIR /web
COPY frontend/duellab-web/package.json frontend/duellab-web/package-lock.json frontend/duellab-web/.npmrc ./
RUN npm ci --no-audit --no-fund
COPY frontend/duellab-web/ ./
RUN npm run build-only

# ---- Backend build ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig DuelLab.slnx ./
COPY src/ src/
RUN dotnet publish src/DuelLab.Api -c Release -o /app --no-self-contained

# ---- Runtime ----------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
# Card images are downloaded once and kept here; mount a volume so they survive restarts.
RUN mkdir -p /app/data/card-images && chown -R $APP_UID /app/data
VOLUME /app/data
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    CardCatalog__ImageCachePath=/app/data/card-images
EXPOSE 8080
ENTRYPOINT ["dotnet", "DuelLab.Api.dll"]
