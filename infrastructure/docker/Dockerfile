FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Roam.Api/Roam.Api.csproj", "src/Roam.Api/"]
COPY ["src/Roam.Application/Roam.Application.csproj", "src/Roam.Application/"]
COPY ["src/Roam.Contracts/Roam.Contracts.csproj", "src/Roam.Contracts/"]
COPY ["src/Roam.Domain/Roam.Domain.csproj", "src/Roam.Domain/"]
COPY ["src/Roam.Infrastructure/Roam.Infrastructure.csproj", "src/Roam.Infrastructure/"]
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
RUN dotnet restore "src/Roam.Api/Roam.Api.csproj"
COPY . .
WORKDIR "/src/src/Roam.Api"
RUN dotnet build "Roam.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Roam.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Roam.Api.dll"]
