FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory inside the container
WORKDIR /src

# Copy the solution file and project files
COPY OceanMonitoringSystem.sln ./
COPY Aggregator/Aggregator.csproj Aggregator/
COPY Common/Common.csproj Common/

# Restore dependencies
RUN dotnet restore "Aggregator/Aggregator.csproj"

# Copy the rest of the application files
COPY Aggregator/ Aggregator/
COPY Common/ Common/

# Build the application
RUN dotnet build "Aggregator/Aggregator.csproj" -c Release -o /app/build

# Use the runtime base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Set the working directory inside the container
WORKDIR /app

# Copy the built application files
COPY --from=build /app/build .

# Expose the port used by the application
EXPOSE 9000

# Define the command to run the application
ENTRYPOINT ["dotnet", "Aggregator.dll"]