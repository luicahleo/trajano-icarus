# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Icarus.sln
RUN dotnet publish src/Host/Icarus.Host/Icarus.Host.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# Runtime (no-root)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Volúmenes privados de documentos (precios y notas de pedido, specs SP8):
# fuera del web root y con propiedad del usuario app para que los named
# volumes hereden el dueño correcto al primer uso.
RUN mkdir -p /app/documentos-privados /app/documentos-pedidos \
    && chown -R app:app /app/documentos-privados /app/documentos-pedidos
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Icarus.Host.dll"]
