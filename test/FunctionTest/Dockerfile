FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["test/FunctionTest/FunctionTest.csproj", "test/FunctionTest/"]
RUN dotnet restore "test/FunctionTest/FunctionTest.csproj"
COPY . .
WORKDIR "/src/test/FunctionTest"
RUN dotnet build "FunctionTest.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FunctionTest.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FunctionTest.dll"]
