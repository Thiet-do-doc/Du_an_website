FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/DatingApp.Api/DatingApp.Api.csproj backend/DatingApp.Api/
RUN dotnet restore backend/DatingApp.Api/DatingApp.Api.csproj
COPY backend/DatingApp.Api/ backend/DatingApp.Api/
RUN dotnet publish backend/DatingApp.Api/DatingApp.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /out .
HEALTHCHECK --interval=10s --timeout=3s --retries=5 --start-period=15s \
    CMD curl --fail --silent http://127.0.0.1:8080/api/v1/health || exit 1
ENTRYPOINT ["dotnet", "DatingApp.Api.dll"]
