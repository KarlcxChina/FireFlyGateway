# See https://aka.ms/customizecontainer to learn how to customize your debug container
# and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used for running from VS in fast mode (defaulting to the Debug configuration).
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# It's a good practice to run containers as a non-root user.
# The UID is passed by the container engine.
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage builds the service project.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["FireFlyGateway.csproj", "."]
RUN dotnet restore "./FireFlyGateway.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./FireFlyGateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage publishes the service project to be copied to the final stage.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FireFlyGateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used for production or when running from VS in regular mode.
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FireFlyGateway.dll"]
