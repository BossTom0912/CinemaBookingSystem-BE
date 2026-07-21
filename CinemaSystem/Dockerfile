FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["CinemaSystem/CinemaSystem.csproj", "CinemaSystem/"]
COPY ["CinemaSystem.Application/CinemaSystem.Application.csproj", "CinemaSystem.Application/"]
COPY ["CinemaSystem.Contracts/CinemaSystem.Contracts.csproj", "CinemaSystem.Contracts/"]
COPY ["CinemaSystem.Domain/CinemaSystem.Domain.csproj", "CinemaSystem.Domain/"]
COPY ["CinemaSystem.Infrastructure/CinemaSystem.Infrastructure.csproj", "CinemaSystem.Infrastructure/"]
RUN dotnet restore "CinemaSystem/CinemaSystem.csproj"

COPY . .
RUN dotnet publish "CinemaSystem/CinemaSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CinemaSystem.dll"]
