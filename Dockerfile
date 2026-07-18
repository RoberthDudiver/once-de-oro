# ── Build ───────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore por capas (mejor cache): primero los .csproj
COPY Shared/OnceDeOro.Shared.csproj Shared/
COPY Client/OnceDeOro.Client.csproj Client/
COPY Server/OnceDeOro.Server.csproj Server/
RUN dotnet restore Server/OnceDeOro.Server.csproj

# Copiamos el resto y publicamos el Server (incluye el WASM del Client)
COPY . .
RUN dotnet publish Server/OnceDeOro.Server.csproj -c Release -o /app/publish --no-restore

# Al publicar una app Blazor WASM hosteada, el placeholder del fingerprint del loader
# no siempre se resuelve. Parcheamos index.html para apuntar al blazor.webassembly.<hash>.js real.
RUN cd /app/publish/wwwroot && \
    f=$(ls _framework/blazor.webassembly.*.js | grep -vE '\.(gz|br)$' | head -1) && f=${f#_framework/} && \
    sed -i "s|_framework/blazor.webassembly.js|_framework/$f|" index.html && \
    echo "Blazor loader -> $f"

# ── Runtime ─────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "OnceDeOro.Server.dll"]
