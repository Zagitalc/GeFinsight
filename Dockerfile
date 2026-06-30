FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["GeFinsight.sln", "./"]
COPY ["GeFinsight.Core/GeFinsight.Core.csproj", "GeFinsight.Core/"]
COPY ["GeFinsight.Infrastructure/GeFinsight.Infrastructure.csproj", "GeFinsight.Infrastructure/"]
COPY ["GeFinsight.Web/GeFinsight.Web.csproj", "GeFinsight.Web/"]

RUN dotnet restore "GeFinsight.Web/GeFinsight.Web.csproj"

COPY . .

RUN dotnet publish "GeFinsight.Web/GeFinsight.Web.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "GeFinsight.Web.dll"]
