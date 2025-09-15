FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . ./

# Восстанавливаем зависимости для всего решения
RUN dotnet restore

# ИЛИ если restore всего решения не работает, восстанавливаем конкретные проекты
# RUN dotnet restore "IntegrationPlatform.Publication.API.csproj"
# RUN dotnet restore "Common/IntegrationPlatform.Common.csproj"

RUN dotnet build "IntegrationPlatform.Publication.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IntegrationPlatform.Publication.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IntegrationPlatform.Publication.API.dll"]