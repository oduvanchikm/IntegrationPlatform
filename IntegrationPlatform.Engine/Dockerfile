FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["IntegrationPlatform.Engine/IntegrationPlatform.Engine.csproj", "IntegrationPlatform.Engine/"]
COPY ["IntegrationPlatform.Common/IntegrationPlatform.Common.csproj", "IntegrationPlatform.Common/"]
COPY ["IntegrationPlatform.Publication.DataAccess/IntegrationPlatform.Publication.DataAccess.csproj", "IntegrationPlatform.Publication.DataAccess/"]
COPY ["IntegrationPlatform.Subscription.DataAccess/IntegrationPlatform.Subscription.DataAccess.csproj", "IntegrationPlatform.Subscription.DataAccess/"]

RUN dotnet restore "IntegrationPlatform.Engine/IntegrationPlatform.Engine.csproj"

COPY . .

WORKDIR "/src/IntegrationPlatform.Engine"
RUN dotnet publish "IntegrationPlatform.Engine.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IntegrationPlatform.Engine.dll"]