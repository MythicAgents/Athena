From python:3.8-buster

ARG CA_CERTIFICATE
ARG NPM_REGISTRY
ARG PYPI_INDEX
ARG PYPI_INDEX_URL
ARG DOCKER_REGSITRY_MIRROR
ARG HTTP_PROXY
ARG HTTPS_PROXY

RUN apt update && apt install wget nuget -y
RUN wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN rm packages-microsoft-prod.deb

# RUN  apt-get update; \
#   apt-get install -y apt-transport-https && \
#   apt-get update && \
#   apt-get install -y dotnet-sdk-7.0

ENV DOTNET_VERSION=7.0.0-rc.2.22472.3

# Install .NET Runtime
RUN dotnet_version=7.0.0-rc.2.22472.3 \
    && curl -fSL --output dotnet.tar.gz https://download.visualstudio.microsoft.com/download/pr/f5c74056-330b-452b-915e-d98fda75024e/18076ca3b89cd362162bbd0cbf9b2ca5/dotnet-sdk-7.0.100-rc.2.22477.23-linux-x64.tar.gz \
    && dotnet_sha512='22db5d1d16f6fcedfc46f87896920425b5d9b61d09c47d254d6ac731c6d853657882b21faf21f313ed20b33e6331d01b9f723b2c586f0e0cf5acc5ed570b0260' \
    && echo "$dotnet_sha512  dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /dotnet \
    && tar -oxzf dotnet.tar.gz -C /dotnet \
    && rm dotnet.tar.gz \
    && ln -s /dotnet/dotnet /usr/bin/dotnet


COPY ["requirements.txt", "/requirements.txt"]
RUN pip install -r /requirements.txt

ENTRYPOINT ["/Mythic/mythic/payload_service.sh"]