# Multi-stage Dockerfile for JBDS-GymApp ASP.NET Core 10 API (AMD64 & ARM64 compatible for OCI Always Free)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/JBDS.Platform.Core/JBDS.Platform.Core.csproj src/JBDS.Platform.Core/
COPY src/JBDS.GymApp.Shared/JBDS.GymApp.Shared.csproj src/JBDS.GymApp.Shared/
COPY src/JBDS.GymApp.Api/JBDS.GymApp.Api.csproj src/JBDS.GymApp.Api/
RUN dotnet restore src/JBDS.GymApp.Api/JBDS.GymApp.Api.csproj

COPY src/JBDS.Platform.Core/ src/JBDS.Platform.Core/
COPY src/JBDS.GymApp.Shared/ src/JBDS.GymApp.Shared/
COPY src/JBDS.GymApp.Api/ src/JBDS.GymApp.Api/
RUN dotnet publish src/JBDS.GymApp.Api/JBDS.GymApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JBDS.GymApp.Api.dll"]
