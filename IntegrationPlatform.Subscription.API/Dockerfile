FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["IntegrationPlatform.Subscription.API/IntegrationPlatform.Subscription.API.csproj", "IntegrationPlatform.Subscription.API/"]
COPY ["IntegrationPlatform.Common/IntegrationPlatform.Common.csproj", "IntegrationPlatform.Common/"]
COPY ["IntegrationPlatform.Subscription.DataAccess/IntegrationPlatform.Subscription.DataAccess.csproj", "IntegrationPlatform.Subscription.DataAccess/"]
COPY ["IntegrationPlatform.Publication.DataAccess/IntegrationPlatform.Publication.DataAccess.csproj", "IntegrationPlatform.Publication.DataAccess/"]



RUN dotnet restore "IntegrationPlatform.Subscription.API/IntegrationPlatform.Subscription.API.csproj"

COPY ["IntegrationPlatform.Subscription.API/", "IntegrationPlatform.Subscription.API/"]
COPY ["IntegrationPlatform.Common/", "IntegrationPlatform.Common/"]
COPY ["IntegrationPlatform.Subscription.DataAccess/", "IntegrationPlatform.Subscription.DataAccess/"]
COPY ["IntegrationPlatform.Publication.DataAccess/", "IntegrationPlatform.Publication.DataAccess/"]

COPY ["IntegrationPlatform.Search.API/appsettings*.json", "/app/"]

WORKDIR "/src/IntegrationPlatform.Subscription.API"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IntegrationPlatform.Subscription.API.dll"]
