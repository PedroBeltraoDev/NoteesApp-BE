# Build stage - .NET 10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar e restaurar
COPY ["NotesApp.Api.csproj", "./"]
RUN dotnet restore

# Copiar tudo e publicar
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage - .NET 10.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copiar arquivos publicados
COPY --from=build /app/publish .

# Configurar ASP.NET Core
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "NotesApp.Api.dll"]