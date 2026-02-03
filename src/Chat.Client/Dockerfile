FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["src/Chat.Client/Chat.Client.csproj", "src/Chat.Client/"]
COPY ["src/Chat.Contracts/Chat.Contracts.csproj", "src/Chat.Contracts/"]
COPY ["Directory.Build.props", "src/"]
COPY ["Directory.Packages.props", "src/"]

# Restore dependencies
RUN dotnet restore "src/Chat.Client/Chat.Client.csproj"

# Copy source code
COPY . .

WORKDIR "/src/src/Chat.Client"
RUN dotnet build "Chat.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Chat.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Chat.Client.dll"]
