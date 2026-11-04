FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props nuget.config ./
COPY Pactogg.Api/Pactogg.Api.csproj Pactogg.Api/
RUN dotnet restore Pactogg.Api/Pactogg.Api.csproj
COPY Pactogg.Api/ Pactogg.Api/
RUN dotnet publish Pactogg.Api/Pactogg.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN addgroup --system --gid 1001 pactogg \
    && adduser --system --uid 1001 --ingroup pactogg --no-create-home pactogg
COPY --from=build --chown=pactogg:pactogg /app/publish ./
USER pactogg
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pactogg.Api.dll"]
