FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory inside the container
WORKDIR /src

# Copy the solution file and project files
COPY OceanMonitoringSystem.sln ./
COPY Wavy/Wavy.csproj Wavy/
COPY Common/Common.csproj Common/

# Restore dependencies
RUN dotnet restore "Wavy/Wavy.csproj"

# Copy the rest of the application files
COPY Wavy/ Wavy/
COPY Common/ Common/

# Build the application
RUN dotnet build "Wavy/Wavy.csproj" -c Release -o /app/build

# Use the runtime base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Set the working directory inside the container
WORKDIR /app

# Copy the built application files
COPY --from=build /app/build .

# Define the command to run the application
ENTRYPOINT ["dotnet", "Wavy.dll"]
