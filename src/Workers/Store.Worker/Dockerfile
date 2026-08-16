# See src/Web/Store.Web/Dockerfile's comment — same reasoning (repo-root context, restore the
# single csproj not the .slnx).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ECommerce.slnx ./
COPY src/ src/

RUN dotnet restore src/Workers/Store.Worker/Store.Worker.csproj
RUN dotnet publish src/Workers/Store.Worker/Store.Worker.csproj -c Release -o /app/publish --no-restore

# Plain `runtime` image, not `aspnet` — Store.Worker has no inbound HTTP (docs/observability.md),
# so it doesn't need the ASP.NET Core shared framework, only the base .NET runtime.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Store.Worker.dll"]
