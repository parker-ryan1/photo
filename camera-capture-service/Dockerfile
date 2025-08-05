# Multi-stage build for Canon Camera Control Service
# Note: This requires Windows containers for Canon SDK support

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

# Copy project files
COPY *.csproj .
RUN dotnet restore

# Copy source code
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0-windowsservercore-ltsc2022 AS runtime
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Create output directory for images
RUN mkdir captured_images

# Expose volume for captured images
VOLUME ["C:/app/captured_images"]

# Set environment variables
ENV DOTNET_ENVIRONMENT=Production

# Set the entry point
ENTRYPOINT ["CameraCaptureService.exe"]