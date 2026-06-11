FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TflAnalytics.sln ./
COPY src/TflAnalytics.Ingestion.Functions/TflAnalytics.Ingestion.Functions.csproj src/TflAnalytics.Ingestion.Functions/
COPY src/TflAnalytics.Application/TflAnalytics.Application.csproj src/TflAnalytics.Application/
COPY src/TflAnalytics.Contracts/TflAnalytics.Contracts.csproj src/TflAnalytics.Contracts/
COPY src/TflAnalytics.Infrastructure/TflAnalytics.Infrastructure.csproj src/TflAnalytics.Infrastructure/
RUN dotnet restore src/TflAnalytics.Ingestion.Functions/TflAnalytics.Ingestion.Functions.csproj

COPY src/ src/
RUN dotnet publish src/TflAnalytics.Ingestion.Functions/TflAnalytics.Ingestion.Functions.csproj \
    --configuration Release \
    --output /home/site/wwwroot \
    --no-restore

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
COPY --from=build /home/site/wwwroot /home/site/wwwroot
