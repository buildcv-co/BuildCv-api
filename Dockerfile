# syntax=docker/dockerfile:1

# ---------- Compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/BuildCv.Domain/BuildCv.Domain.csproj                 src/BuildCv.Domain/
COPY src/BuildCv.Application/BuildCv.Application.csproj        src/BuildCv.Application/
COPY src/BuildCv.Infrastructure/BuildCv.Infrastructure.csproj src/BuildCv.Infrastructure/
COPY src/BuildCv.Api/BuildCv.Api.csproj                       src/BuildCv.Api/
RUN dotnet restore src/BuildCv.Api/BuildCv.Api.csproj

COPY src/ src/
RUN dotnet publish src/BuildCv.Api/BuildCv.Api.csproj -c Release -o /app --no-restore

# ---------- Ejecución ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

EXPOSE 8080
# Render/Railway inyectan $PORT; por defecto 8080. exec preserva las señales (apagado limpio).
ENTRYPOINT ["sh", "-c", "exec dotnet BuildCv.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
