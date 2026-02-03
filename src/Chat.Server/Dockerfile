FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["src/Chat.Server/Chat.Server.csproj", "src/Chat.Server/"]
COPY ["src/Chat.Contracts/Chat.Contracts.csproj", "src/Chat.Contracts/"]
COPY ["Directory.Build.props", "src/"]
COPY ["Directory.Packages.props", "src/"]

# Restore dependencies
RUN dotnet restore "src/Chat.Server/Chat.Server.csproj"

# Copy source code
COPY . .

WORKDIR "/src/src/Chat.Server"
RUN dotnet build "Chat.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Chat.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .

# Non-root user for security
USER $APP_UID

ENTRYPOINT ["dotnet", "Chat.Server.dll"]
