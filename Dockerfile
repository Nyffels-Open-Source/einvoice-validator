FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY EInvoicing.Validation.sln ./
COPY src ./src
COPY tests ./tests
RUN dotnet publish src/EInvoicing.Validation.Api/EInvoicing.Validation.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends openjdk-17-jre-headless curl unzip \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV VALIDATION_ARTEFACTS_PATH=/data/artefacts
ENV VALIDATION_DEFAULT_PROFILE=peppol-bis3
ENTRYPOINT ["dotnet", "EInvoicing.Validation.Api.dll"]
