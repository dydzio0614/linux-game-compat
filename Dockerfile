FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY LinuxGameCompat.sln ./
COPY LinuxGameCompat/LinuxGameCompat.csproj LinuxGameCompat/
RUN dotnet restore LinuxGameCompat/LinuxGameCompat.csproj

COPY . .
RUN dotnet publish LinuxGameCompat/LinuxGameCompat.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish && \
    test -f /app/publish/wwwroot/_framework/blazor.web.js && \
    grep -Fq '"Route":"_framework/blazor.web.js"' \
        /app/publish/LinuxGameCompat.staticwebassets.endpoints.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["sh", "-c", "dotnet LinuxGameCompat.dll --urls http://0.0.0.0:${PORT:-8080}"]
