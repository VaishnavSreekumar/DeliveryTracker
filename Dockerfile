# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY DeliveryTracker.API/DeliveryTracker.API.csproj DeliveryTracker.API/
COPY DeliveryTracker.Tests/DeliveryTracker.Tests.csproj DeliveryTracker.Tests/
RUN dotnet restore DeliveryTracker.API/DeliveryTracker.API.csproj

# Copy source code and build production release
COPY DeliveryTracker.API/ DeliveryTracker.API/
WORKDIR /src/DeliveryTracker.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Container
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy published binaries
COPY --from=build /app/publish .

# Expose Render listener port
ENV PORT=10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "DeliveryTracker.API.dll"]
