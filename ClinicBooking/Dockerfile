# ===== build =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj + restore (cache)
COPY ClinicBooking.Api/ClinicBooking.Api.csproj ClinicBooking.Api/
RUN dotnet restore ClinicBooking.Api/ClinicBooking.Api.csproj

# copy everything + publish
COPY . .
RUN dotnet publish ClinicBooking.Api/ClinicBooking.Api.csproj -c Release -o /app/publish

# ===== run =====
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# (optionnel) juste informatif
EXPOSE 8080

# IMPORTANT: Render fournit $PORT -> on bind dessus
# Fallback local: 8080
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} && dotnet ClinicBooking.Api.dll"]
