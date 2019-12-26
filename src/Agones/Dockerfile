FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 7654

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /
COPY ["src/Agones/Agones.csproj", "src/Agones/"]
RUN dotnet restore "src/Agones/Agones.csproj"
COPY . .
WORKDIR "/src/Agones"
RUN dotnet build "Agones.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Agones.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Agones.dll"]
