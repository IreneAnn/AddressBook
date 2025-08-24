# See https://aka.ms/customizecontainer to learn how to customize your debug container 
# and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# Build image
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["AddressBook.Api/AddressBook.Api.csproj", "AddressBook.Api/"]
COPY ["AddressBook.Application/AddressBook.Application.csproj", "AddressBook.Application/"]
COPY ["AddressBook.Domain/AddressBook.Domain.csproj", "AddressBook.Domain/"]
COPY ["AddressBook.Infrastructure/AddressBook.Infrastructure.csproj", "AddressBook.Infrastructure/"]

RUN dotnet restore "./AddressBook.Api/AddressBook.Api.csproj"

COPY . .
WORKDIR "/src/AddressBook.Api"
RUN dotnet build "./AddressBook.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AddressBook.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Configure environment (HTTP only)
ENV ASPNETCORE_ENVIRONMENT="Development"
ENV ASPNETCORE_URLS="http://+:8080"

# Expose port
EXPOSE 8080

ENTRYPOINT ["dotnet", "AddressBook.Api.dll"]