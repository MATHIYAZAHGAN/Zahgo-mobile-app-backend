# ─── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and all project files first (layer-cache friendly)
COPY ZahSellerAI.sln ./
COPY src/API/ZahSellerAI.API.csproj             src/API/
COPY src/Application/ZahSellerAI.Application.csproj  src/Application/
COPY src/Domain/ZahSellerAI.Domain.csproj       src/Domain/
COPY src/Infrastructure/ZahSellerAI.Infrastructure.csproj src/Infrastructure/
COPY src/Shared/ZahSellerAI.Shared.csproj       src/Shared/

# Restore dependencies
RUN dotnet restore ZahSellerAI.sln

# Copy the rest of the source code
COPY src/ src/

# Publish in Release mode to /app/publish
RUN dotnet publish src/API/ZahSellerAI.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create logs directory
RUN mkdir -p logs

# Copy published output from build stage
COPY --from=build /app/publish .

# Render injects PORT env var; ASP.NET Core reads ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "ZahSellerAI.API.dll"]
