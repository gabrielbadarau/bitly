FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Bitly.Domain/Bitly.Domain.csproj src/Bitly.Domain/
COPY src/Bitly.ReadApi/Bitly.ReadApi.csproj src/Bitly.ReadApi/
RUN dotnet restore src/Bitly.ReadApi/Bitly.ReadApi.csproj

COPY src/Bitly.Domain/ src/Bitly.Domain/
COPY src/Bitly.ReadApi/ src/Bitly.ReadApi/
RUN dotnet publish src/Bitly.ReadApi/Bitly.ReadApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Bitly.ReadApi.dll"]
