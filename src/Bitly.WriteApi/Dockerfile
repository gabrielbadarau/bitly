FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Bitly.Domain/Bitly.Domain.csproj src/Bitly.Domain/
COPY src/Bitly.WriteApi/Bitly.WriteApi.csproj src/Bitly.WriteApi/
RUN dotnet restore src/Bitly.WriteApi/Bitly.WriteApi.csproj

COPY src/Bitly.Domain/ src/Bitly.Domain/
COPY src/Bitly.WriteApi/ src/Bitly.WriteApi/
RUN dotnet publish src/Bitly.WriteApi/Bitly.WriteApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Bitly.WriteApi.dll"]
