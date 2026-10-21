FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching restore layer
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Roam.Contracts/Roam.Contracts.csproj", "src/Roam.Contracts/"]
COPY ["src/Roam.Domain/Roam.Domain.csproj", "src/Roam.Domain/"]
COPY ["src/Roam.Application/Roam.Application.csproj", "src/Roam.Application/"]
COPY ["src/Roam.Infrastructure/Roam.Infrastructure.csproj", "src/Roam.Infrastructure/"]
COPY ["src/Roam.Api/Roam.Api.csproj", "src/Roam.Api/"]

RUN dotnet restore "src/Roam.Api/Roam.Api.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/src/Roam.Api"
RUN dotnet build "Roam.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Roam.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Roam.Api.dll"]
