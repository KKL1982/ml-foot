# ============================================================
# Stage 1 — Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies (layer caching: only re-restore when csproj/sln change)
COPY FootballPrediction.sln ./
COPY Directory.Build.props ./
COPY NuGet.Config ./
COPY src/FootballPrediction.Domain/FootballPrediction.Domain.csproj src/FootballPrediction.Domain/
COPY src/FootballPrediction.Application/FootballPrediction.Application.csproj src/FootballPrediction.Application/
COPY src/FootballPrediction.ML/FootballPrediction.ML.csproj src/FootballPrediction.ML/
COPY src/FootballPrediction.Web/FootballPrediction.Web.csproj src/FootballPrediction.Web/
RUN dotnet restore FootballPrediction.sln

# Copy sources and publish
COPY src/ src/
RUN dotnet publish src/FootballPrediction.Web/FootballPrediction.Web.csproj \
    -c Release \
    -o /app \
    /p:UseAppHost=false

# ============================================================
# Stage 2 — Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy published app + pre-trained models
COPY --from=build /app .
COPY models/ ./models/

# Create data directory and set ownership
RUN mkdir -p /app/data && \
    chown -R app:app /app

USER app

ENV ASPNETCORE_URLS=http://+:7575
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 7575

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:7575/ || exit 1

ENTRYPOINT ["dotnet", "FootballPrediction.Web.dll"]
