# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/PlataformaCreditos/PlataformaCreditos.csproj src/PlataformaCreditos/
RUN dotnet restore src/PlataformaCreditos/PlataformaCreditos.csproj
COPY src/PlataformaCreditos/ src/PlataformaCreditos/
RUN dotnet publish src/PlataformaCreditos/PlataformaCreditos.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
# El start command del servicio en Render expande $PORT en tiempo de ejecucion
CMD sh -c "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet PlataformaCreditos.dll"