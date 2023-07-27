FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
WORKDIR /app

# Copy everything else and build
COPY . ./
RUN dotnet publish "./src/MangaDexWatcher.Cli/MangaDexWatcher.Cli.csproj" -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "MangaDexWatcher.Cli.dll", "watch-md"]

# https://docs.docker.com/engine/examples/dotnetcore/