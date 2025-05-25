FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Install protobuf compiler
RUN apt-get update && \
    apt-get install -y protobuf-compiler && \
    rm -rf /var/lib/apt/lists/*

# Copy the solution and project files
COPY ["OceanMonitoringSystem.sln", "./"]
COPY ["Aggregator/Aggregator.csproj", "Aggregator/"]
COPY ["Common/Common.csproj", "Common/"]

# Restore dependencies
RUN dotnet restore "Aggregator/Aggregator.csproj"

# Copy the rest of the source code
COPY ["Aggregator/", "Aggregator/"]
COPY ["Common/", "Common/"]

# Build and publish
RUN dotnet publish "Aggregator/Aggregator.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Create data directory
RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "Aggregator.dll"] 