# Use the official image as a parent image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["smart-lab-empower-api.csproj", "./"]
RUN dotnet restore "smart-lab-empower-api.csproj"
COPY . .
RUN dotnet build "smart-lab-empower-api.csproj" -c Release -o /app/build && \
    dotnet publish "smart-lab-empower-api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
COPY --from=build /app/publish .
COPY ./database/data/resultsCheck.db /app/

# Set the environment variable to ensure the app listens on port 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "smart-lab-empower-api.dll"]
