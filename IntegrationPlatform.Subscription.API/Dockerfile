FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["IntegrationPlatform.Subscription.API/IntegrationPlatform.Subscription.API.csproj", "IntegrationPlatform.Subscription.API/"]
RUN dotnet restore "IntegrationPlatform.Subscription.API/IntegrationPlatform.Subscription.API.csproj"
COPY . .
WORKDIR "/src/IntegrationPlatform.Subscription.API"
RUN dotnet build "IntegrationPlatform.Subscription.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "IntegrationPlatform.Subscription.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IntegrationPlatform.Subscription.API.dll"]
