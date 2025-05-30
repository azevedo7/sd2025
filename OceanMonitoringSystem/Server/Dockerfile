FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory inside the container
WORKDIR /src

# Copy the solution file and project files
COPY OceanMonitoringSystem.sln ./
COPY Server/Server.csproj Server/
COPY Common/Common.csproj Common/

# Restore dependencies
RUN dotnet restore "Server/Server.csproj"

# Copy the rest of the application files
COPY Server/ Server/
COPY Common/ Common/

# Build the application
RUN dotnet build "Server/Server.csproj" -c Release -o /app/build

# Use the runtime base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Set the working directory inside the container
WORKDIR /app

# Copy the built application files
COPY --from=build /app/build .

# Create wwwroot directory for static files
RUN mkdir -p wwwroot

# Copy static files if they exist
COPY --from=build /src/Server/wwwroot ./wwwroot

# Expose the ports used by the server
EXPOSE 8080
EXPOSE 5001

# Define the command to run the server
ENTRYPOINT ["dotnet", "Server.dll"]