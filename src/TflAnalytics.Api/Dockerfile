FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TflAnalytics.sln ./
COPY src/TflAnalytics.Api/TflAnalytics.Api.csproj src/TflAnalytics.Api/
COPY src/TflAnalytics.Application/TflAnalytics.Application.csproj src/TflAnalytics.Application/
COPY src/TflAnalytics.Contracts/TflAnalytics.Contracts.csproj src/TflAnalytics.Contracts/
COPY src/TflAnalytics.Infrastructure/TflAnalytics.Infrastructure.csproj src/TflAnalytics.Infrastructure/
RUN dotnet restore src/TflAnalytics.Api/TflAnalytics.Api.csproj

COPY src/ src/
RUN dotnet publish src/TflAnalytics.Api/TflAnalytics.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TflAnalytics.Api.dll"]
