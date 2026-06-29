FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/AuditTrail.API/AuditTrail.API.csproj", "src/AuditTrail.API/"]
COPY ["src/AuditTrail.Application/AuditTrail.Application.csproj", "src/AuditTrail.Application/"]
COPY ["src/AuditTrail.Domain/AuditTrail.Domain.csproj", "src/AuditTrail.Domain/"]
COPY ["src/AuditTrail.Infrastructure/AuditTrail.Infrastructure.csproj", "src/AuditTrail.Infrastructure/"]
RUN dotnet restore "src/AuditTrail.API/AuditTrail.API.csproj"
COPY . .
WORKDIR "/src/src/AuditTrail.API"
RUN dotnet build "AuditTrail.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AuditTrail.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AuditTrail.API.dll"]
