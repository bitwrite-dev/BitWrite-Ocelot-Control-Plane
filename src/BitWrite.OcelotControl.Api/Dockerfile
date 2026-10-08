# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY BitWrite.OcelotControl.slnx .
COPY src/BitWrite.OcelotControl.Api/BitWrite.OcelotControl.Api.csproj src/BitWrite.OcelotControl.Api/
COPY src/BitWrite.OcelotControl.Application/BitWrite.OcelotControl.Application.csproj src/BitWrite.OcelotControl.Application/
COPY src/BitWrite.OcelotControl.Domain/BitWrite.OcelotControl.Domain.csproj src/BitWrite.OcelotControl.Domain/
COPY src/BitWrite.OcelotControl.Infrastructure/BitWrite.OcelotControl.Infrastructure.csproj src/BitWrite.OcelotControl.Infrastructure/

# Restore dependencies
RUN dotnet restore BitWrite.OcelotControl.slnx

# Copy source
COPY . .

# Build API
WORKDIR /src/src/BitWrite.OcelotControl.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published app
COPY --from=build /app/publish .

# Set environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Expose port
EXPOSE 8080

# Run as non-root
USER appuser

ENTRYPOINT ["dotnet", "BitWrite.OcelotControl.Api.dll"]