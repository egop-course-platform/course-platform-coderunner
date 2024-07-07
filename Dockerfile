# Use the SDK image to build and publish the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["Coderunner.Core/Coderunner.Core.csproj", "Coderunner.Core/"]
COPY ["Coderunner.Presentation/Coderunner.Presentation.csproj", "Coderunner.Presentation/"]
COPY ["Coderunner.DistributedOutbox/Coderunner.DistributedOutbox.csproj", "Coderunner.DistributedOutbox/"]
COPY ["Coderunner.DistributedOutbox.Kafka/Coderunner.DistributedOutbox.Kafka.csproj", "Coderunner.DistributedOutbox.Kafka/"]
COPY ["Coderunner.DistributedOutbox.Linq2Db/Coderunner.DistributedOutbox.Linq2Db.csproj", "Coderunner.DistributedOutbox.Linq2Db/"]
RUN dotnet restore "Coderunner.Presentation/Coderunner.Presentation.csproj"

# Copy the rest of the application and build it
COPY ./Coderunner.Core ./Coderunner.Core
COPY ./Coderunner.Presentation ./Coderunner.Presentation
COPY ./Coderunner.DistributedOutbox ./Coderunner.DistributedOutbox
COPY ./Coderunner.DistributedOutbox.Kafka ./Coderunner.DistributedOutbox.Kafka
COPY ./Coderunner.DistributedOutbox.Linq2Db ./Coderunner.DistributedOutbox.Linq2Db
WORKDIR "/src/Coderunner.Presentation"
RUN dotnet publish "Coderunner.Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the runtime image to run the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Coderunner.Presentation.dll"]
