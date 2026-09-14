FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Enrichly.JobAutomation.Domain/Enrichly.JobAutomation.Domain.csproj src/Enrichly.JobAutomation.Domain/
COPY src/Enrichly.JobAutomation.Application/Enrichly.JobAutomation.Application.csproj src/Enrichly.JobAutomation.Application/
COPY src/Enrichly.JobAutomation.Infrastructure/Enrichly.JobAutomation.Infrastructure.csproj src/Enrichly.JobAutomation.Infrastructure/
COPY src/Enrichly.JobAutomation.Api/Enrichly.JobAutomation.Api.csproj src/Enrichly.JobAutomation.Api/
RUN dotnet restore src/Enrichly.JobAutomation.Api/Enrichly.JobAutomation.Api.csproj

COPY src ./src
RUN dotnet publish src/Enrichly.JobAutomation.Api/Enrichly.JobAutomation.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Enrichly.JobAutomation.Api.dll"]
