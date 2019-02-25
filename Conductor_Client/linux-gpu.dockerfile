FROM microsoft/dotnet:2.2-sdk as builder
SHELL ["/bin/bash", "-c"]

COPY . '/root/build'
WORKDIR '/root/build'
RUN dotnet restore
RUN dotnet publish 'Conductor_Client/Conductor_Client.csproj'

FROM chemsorly/keras-tensorflow:latest-ubuntu-py3-gpu-noavx2
SHELL ["/bin/bash", "-c"]

ARG CONDUCTOR_VERSION
ENV CONDUCTOR_VERSION ${CONDUCTOR_VERSION}
ENV CONDUCTOR_OS ubuntu
ENV CONDUCTOR_TYPE gpu
ENV CONDUCTOR_HOST ""
ENV PYTHONDONTWRITEBYTECODE 1

# Install .NET Core
RUN apt-get update && apt-get -y upgrade && apt-get -y install apt-transport-https
RUN wget -q https://packages.microsoft.com/config/ubuntu/16.04/packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN apt-get update && apt-get -y install dotnet-runtime-2.2

# install other dependencies
RUN pip3 install hyperas

# run app
COPY --from=builder 'root/build/Conductor_Client/bin/Debug/netcoreapp2.2/publish/' '/root/app'
WORKDIR '/root/app'

ENTRYPOINT dotnet Conductor_Client.dll $CONDUCTOR_HOST