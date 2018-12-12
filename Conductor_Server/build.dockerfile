FROM microsoft/dotnet:2.1-sdk as builder
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /source
COPY . .
RUN dotnet restore
RUN dotnet publish ./Conductor_Server/Conductor_Server.csproj --output /app/ --configuration Release

FROM microsoft/windowsservercore
MAINTAINER Adrian.Neubauer@paluno.uni-due.de
ENV RUNTIMEENV="docker"

ARG DEPLOY_BRANCH="dev"
ENV DEPLOY_BRANCH=$DEPLOY_BRANCH
ARG DEPLOY_VERSION="dev"
ENV DEPLOY_VERSION=$DEPLOY_VERSION

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]
#Install ASP.NET Core Runtime
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_VERSION=2.1.2
RUN Invoke-WebRequest -OutFile aspnetcore.zip https://dotnetcli.blob.core.windows.net/dotnet/aspnetcore/Runtime/$Env:ASPNETCORE_VERSION/aspnetcore-runtime-$Env:ASPNETCORE_VERSION-win-x64.zip; \
    $aspnetcore_sha512 = 'a9ab3f01fc07527016513f47fc46427f6da8ee45ab847eebe228ca940f00d7b791431295b5aeaf8c8fb07f4ff1d4e8894fb4cfe5c36e74684f08f7d9d15a0e6b'; \
    if ((Get-FileHash aspnetcore.zip -Algorithm sha512).Hash -ne $aspnetcore_sha512) { \
        Write-Host 'CHECKSUM VERIFICATION FAILED!'; \
        exit 1; \
    }; \
    \
    Expand-Archive aspnetcore.zip -DestinationPath  $Env:ProgramFiles\dotnet; \
    Remove-Item -Force aspnetcore.zip
RUN setx /M PATH $($Env:PATH + ';' + $Env:ProgramFiles + '\dotnet')

# copy app
WORKDIR /app
COPY --from=builder /app .

ENTRYPOINT ["dotnet", "Conductor_Server.dll"]