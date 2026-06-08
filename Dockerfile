FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

RUN apt-get update && \
    apt-get install -y libgssapi-krb5-2 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["src/PhantomPulse.Api/PhantomPulse.Api.csproj", "src/PhantomPulse.Api/"]
COPY ["src/PhantomPulse.SharedKernel/PhantomPulse.SharedKernel.csproj", "src/PhantomPulse.SharedKernel/"]
COPY ["src/PhantomPulse.Infrastructure/PhantomPulse.Infrastructure.csproj", "src/PhantomPulse.Infrastructure/"]
COPY ["src/Modules/Foundation/PhantomPulse.Foundation.csproj", "src/Modules/Foundation/"]
COPY ["src/Modules/Crm/PhantomPulse.Crm.csproj", "src/Modules/Crm/"]
COPY ["src/Modules/Messaging/PhantomPulse.Messaging.csproj", "src/Modules/Messaging/"]
COPY ["src/Modules/Automation/PhantomPulse.Automation.csproj", "src/Modules/Automation/"]
COPY ["src/Modules/Campaigns/PhantomPulse.Campaigns.csproj", "src/Modules/Campaigns/"]
RUN dotnet restore "src/PhantomPulse.Api/PhantomPulse.Api.csproj"
COPY . .
RUN dotnet publish "src/PhantomPulse.Api/PhantomPulse.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PhantomPulse.Api.dll"]