# Profile Service — multi-stage build (.NET 10, port 5005).
# JobPlatform.SharedKernel is restored from GitHub Packages; the nuget_token secret is mounted only for restore.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY nuget.config ./
COPY ProfileService.sln ./
COPY src/ ./src/
RUN --mount=type=secret,id=nuget_token,required=true \
    GITHUB_TOKEN="$(cat /run/secrets/nuget_token)" dotnet restore src/Profile.Api/Profile.Api.csproj
RUN dotnet publish src/Profile.Api/Profile.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
EXPOSE 5005
ENV ASPNETCORE_HTTP_PORTS=5005
# Run as the image's non-root user (APP_UID=1654 in mcr.microsoft.com/dotnet/aspnet), not root.
USER $APP_UID
ENTRYPOINT ["dotnet", "Profile.Api.dll"]
