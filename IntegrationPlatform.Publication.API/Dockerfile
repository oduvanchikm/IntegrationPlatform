FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["IntegrationPlatform.Publication.API/IntegrationPlatform.Publication.API.csproj", "IntegrationPlatform.Publication.API/"]
COPY ["IntegrationPlatform.Common/IntegrationPlatform.Common.csproj", "IntegrationPlatform.Common/"]
COPY ["IntegrationPlatform.Publication.DataAccess/IntegrationPlatform.Publication.DataAccess.csproj", "IntegrationPlatform.Publication.DataAccess/"]

RUN dotnet restore "IntegrationPlatform.Publication.API/IntegrationPlatform.Publication.API.csproj"

COPY ["IntegrationPlatform.Publication.API/", "IntegrationPlatform.Publication.API/"]
COPY ["IntegrationPlatform.Common/", "IntegrationPlatform.Common/"]
COPY ["IntegrationPlatform.Publication.DataAccess/", "IntegrationPlatform.Publication.DataAccess/"]

COPY ["IntegrationPlatform.Publication.API/appsettings*.json", "/app/"]

WORKDIR "/src/IntegrationPlatform.Publication.API"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IntegrationPlatform.Publication.API.dll"]
