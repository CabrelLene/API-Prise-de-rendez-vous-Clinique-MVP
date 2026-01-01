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
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ClinicBooking.Api.dll"]
