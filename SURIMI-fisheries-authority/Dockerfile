# syntax=docker/dockerfile:1
# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5493

ENV ASPNETCORE_HTTP_PORTS=5493
ENV OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
#ENV OTEL_EXPORTER_OTLP_PROTOCOL=grpc
ENV OTEL_SERVICE_NAME=ValueChain


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy NuGet configuration
COPY ["NuGet.config", "."]

# Add authentication for GitHub Packages using BuildKit secret (pass via: --secret id=GITHUB_TOKEN,env=GITHUB_TOKEN)
RUN --mount=type=secret,id=GITHUB_TOKEN \
    if [ -f /run/secrets/GITHUB_TOKEN ]; then \
    dotnet nuget remove source github-Official-EwE || true; \
    dotnet nuget add source https://nuget.pkg.github.com/Official-EwE/index.json \
    --name github-Official-EwE \
    --username github \
    --password "$(cat /run/secrets/GITHUB_TOKEN)" \
    --store-password-in-clear-text \
    --configfile NuGet.config; \
    fi

# Add authentication for BSR (pass via: --secret id=BSR_TOKEN,env=BSR_TOKEN)
RUN --mount=type=secret,id=BSR_TOKEN \
    if [ -f /run/secrets/BSR_TOKEN ]; then \
    dotnet nuget remove source BSR || true; \
    dotnet nuget add source https://buf.build/gen/nuget/index.json \
    --name BSR \
    --username bsr \
    --password "$(cat /run/secrets/BSR_TOKEN)" \
    --store-password-in-clear-text \
    --configfile NuGet.config; \
    fi

COPY ["SURIMI-fisheries-authority/SURIMI-fisheries-authority.csproj", "SURIMI-fisheries-authority/"]
RUN dotnet nuget locals all --clear     # Clear local NuGet cache to ensure we get the latest packages, especially important when using tokens for private feeds
RUN dotnet restore "./SURIMI-fisheries-authority/SURIMI-fisheries-authority.csproj"
COPY . .
WORKDIR "/src/SURIMI-fisheries-authority"
RUN dotnet build "./SURIMI-fisheries-authority.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Remove Includes directory after build
RUN rm -rf /src/SURIMI-fisheries-authority/Includes

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SURIMI-fisheries-authority.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SURIMI-fisheries-authority.dll"]