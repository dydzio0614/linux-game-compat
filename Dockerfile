FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LinuxGameCompat.sln ./
COPY LinuxGameCompat/LinuxGameCompat.csproj LinuxGameCompat/
RUN dotnet restore LinuxGameCompat.sln

COPY . .
RUN dotnet publish LinuxGameCompat/LinuxGameCompat.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["sh", "-c", "dotnet LinuxGameCompat.dll --urls http://0.0.0.0:${PORT:-8080}"]
