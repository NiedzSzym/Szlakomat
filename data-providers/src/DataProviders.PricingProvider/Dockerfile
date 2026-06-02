FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/DataProviders.Shared/DataProviders.Shared.csproj",         "src/DataProviders.Shared/"]
COPY ["src/DataProviders.PricingProvider/DataProviders.PricingProvider.csproj", "src/DataProviders.PricingProvider/"]
RUN dotnet restore "src/DataProviders.PricingProvider/DataProviders.PricingProvider.csproj"
COPY . .
WORKDIR "/src/src/DataProviders.PricingProvider"
RUN dotnet publish "DataProviders.PricingProvider.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DataProviders.PricingProvider.dll"]
