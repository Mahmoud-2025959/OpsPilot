# OpsPilot.API — multi-stage image (plan, P2.2).
# Build from the repository root:
#   docker build -t opspilot-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OpsPilot.slnx ./
COPY OpsPilot.Domain/OpsPilot.Domain.csproj OpsPilot.Domain/
COPY OpsPilot.Contracts/OpsPilot.Contracts.csproj OpsPilot.Contracts/
COPY OpsPilot.Application/OpsPilot.Application.csproj OpsPilot.Application/
COPY OpsPilot.Infrastructure/OpsPilot.Infrastructure.csproj OpsPilot.Infrastructure/
COPY OpsPilot.API/OpsPilot.API.csproj OpsPilot.API/

RUN dotnet restore OpsPilot.API/OpsPilot.API.csproj

COPY OpsPilot.Domain/ OpsPilot.Domain/
COPY OpsPilot.Contracts/ OpsPilot.Contracts/
COPY OpsPilot.Application/ OpsPilot.Application/
COPY OpsPilot.Infrastructure/ OpsPilot.Infrastructure/
COPY OpsPilot.API/ OpsPilot.API/

RUN dotnet publish OpsPilot.API/OpsPilot.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --system app \
    && useradd --system --gid app --home-dir /app --no-create-home appuser

COPY --from=build /app/publish .
RUN chown -R appuser:app /app

USER appuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "OpsPilot.API.dll"]
