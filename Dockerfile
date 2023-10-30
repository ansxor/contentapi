FROM mcr.microsoft.com/dotnet/sdk:6.0 as build

WORKDIR /src
COPY contentapi ./contentapi
COPY contentapi.data ./contentapi.data
COPY contentapi.test ./contentapi.test
RUN dotnet restore ./contentapi/contentapi.csproj
RUN dotnet build contentapi -c Release -o ./app
RUN dotnet publish contentapi -c Release -o ./publish
RUN ls -la publish

FROM mcr.microsoft.com/dotnet/aspnet:6.0 as base
COPY --from=build  /src/publish /app
WORKDIR /app
COPY  Deploy ./Deploy
RUN apt-get update && apt-get install -y sqlite3
EXPOSE 5000

CMD ["/app/Deploy/entrypoint.sh"]