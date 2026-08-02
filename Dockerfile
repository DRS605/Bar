# --- Etapa de compilación ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restauro primero (aprovecha la caché de capas).
COPY *.sln Directory.Build.props nuget.config ./
COPY src/ src/
RUN dotnet restore src/AlxorCore.Api/AlxorCore.Api.csproj

# Publico la API.
RUN dotnet publish src/AlxorCore.Api/AlxorCore.Api.csproj -c Release -o /app --no-restore

# --- Etapa de ejecución ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# QuestPDF (SkiaSharp) necesita fontconfig para generar los PDF de las facturas.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AlxorCore.Api.dll"]
