FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["autodoc-api.csproj", "./"]
RUN dotnet restore "autodoc-api.csproj"
COPY . .
RUN dotnet build "autodoc-api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "autodoc-api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "autodoc-api.dll"]