# Profile Service — multi-stage build (.NET 10, port 5005).
# Local SharedKernel feed is copied in for restore (long-term: GH Packages).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY local-feed/ ./local-feed/
COPY nuget.config ./
COPY ProfileService.sln ./
COPY src/ ./src/
RUN dotnet restore ProfileService.sln
RUN dotnet publish src/Profile.Api/Profile.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
EXPOSE 5005
ENV ASPNETCORE_HTTP_PORTS=5005
ENTRYPOINT ["dotnet", "Profile.Api.dll"]
