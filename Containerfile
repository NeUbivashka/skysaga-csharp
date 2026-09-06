# SkySaga C# servers — web API (:5164) + Smilegate auth (tcp:10106).
# SkySaga.Game is NOT in this image: it P/Invokes RakNet.dll (a Windows PE) and
# reads Data\... backslash paths, so it only runs on Windows for now.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src
COPY Servers/ ./Servers/
RUN dotnet publish Servers/SkySaga.Web/SkySaga.Web.csproj -c Release -o /out/web \
 && dotnet publish Servers/SmilegateAuth/SmilegateAuth.csproj -c Release -o /out/auth

# Runtime: aspnet image carries the ASP.NET Core framework the web project needs.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=builder /out/ /app/
# Which server to run: web | auth (see docker-compose.yml).
ENV SKYSAGA_ROLE=web
EXPOSE 5164/tcp 10106/tcp
CMD ["sh", "-c", "cd /app/$SKYSAGA_ROLE && exec dotnet $([ \"$SKYSAGA_ROLE\" = web ] && echo SkySaga.Web.dll || echo SmilegateAuth.dll)"]
