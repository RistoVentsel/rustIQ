# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/RustIQ.sln backend/
COPY backend/src/RustIQ.Api/RustIQ.Api.csproj backend/src/RustIQ.Api/
COPY backend/src/RustIQ.Application/RustIQ.Application.csproj backend/src/RustIQ.Application/
COPY backend/src/RustIQ.Domain/RustIQ.Domain.csproj backend/src/RustIQ.Domain/
COPY backend/src/RustIQ.Infrastructure/RustIQ.Infrastructure.csproj backend/src/RustIQ.Infrastructure/
RUN dotnet restore backend/src/RustIQ.Api/RustIQ.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/src/RustIQ.Api/RustIQ.Api.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RustIQ.Api.dll"]
