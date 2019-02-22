FROM microsoft/dotnet:2.2-sdk as builder

WORKDIR /source
COPY . .
RUN dotnet restore
RUN dotnet publish ./Conductor_Server/Conductor_Server.csproj --output /app/ --configuration Release

FROM mcr.microsoft.com/windows/servercore:ltsc2019
MAINTAINER Adrian.Neubauer@paluno.uni-due.de
ENV RUNTIMEENV="docker"

ARG DEPLOY_BRANCH="dev"
ENV DEPLOY_BRANCH=$DEPLOY_BRANCH
ARG DEPLOY_VERSION="dev"
ENV DEPLOY_VERSION=$DEPLOY_VERSION

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]
#Install ASP.NET Core Runtime
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_VERSION=2.2.2
RUN Invoke-WebRequest -OutFile aspnetcore.zip https://dotnetcli.blob.core.windows.net/dotnet/aspnetcore/Runtime/$Env:ASPNETCORE_VERSION/aspnetcore-runtime-$Env:ASPNETCORE_VERSION-win-x64.zip; \
    $aspnetcore_sha512 = 'F7FA97F793A511379F3018CC7798C9918FAB566E14199DAB76AB60628D4C1772B71B896E1728A23D3191F58FAF723F23210D4F9A585666BF3160555365082039'; \
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