# Build context is the repo root (see docker-compose.yml / docs/deployment.md) — Central Package
# Management (Directory.Packages.props) and Directory.Build.props live there, and this project
# references most of the other projects in src/, so `dotnet restore` needs the whole tree, not
# just this folder. Restoring the single csproj (not the .slnx) pulls in only the projects this
# one actually references — tests/ is never touched, matching the .dockerignore exclusion below.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ECommerce.slnx ./
COPY src/ src/

RUN dotnet restore src/Web/Store.Web/Store.Web.csproj
RUN dotnet publish src/Web/Store.Web/Store.Web.csproj -c Release -o /app/publish --no-restore

# aspnet (not aspnet:...-alpine): Directory.Build.props sets InvariantGlobalization=false for
# Arabic+English localization, which needs the ICU data the Debian-based image ships with —
# Alpine strips it out unless you add it back manually.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Store.Web.dll"]
