FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["Chat.Client/Chat.Client.csproj", "Chat.Client/"]
COPY ["Chat.Contracts/Chat.Contracts.csproj", "Chat.Contracts/"]

# Restore dependencies
RUN dotnet restore "Chat.Client/Chat.Client.csproj"

# Copy source code
COPY . .

WORKDIR "/src/Chat.Client"
RUN dotnet build "Chat.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Chat.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Chat.Client.dll"]
