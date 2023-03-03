FROM python:3.8-buster

ARG CA_CERTIFICATE
ARG NPM_REGISTRY
ARG PYPI_INDEX
ARG PYPI_INDEX_URL
ARG DOCKER_REGSITRY_MIRROR
ARG HTTP_PROXY
ARG HTTPS_PROXY

RUN apt update && apt install wget nuget gcc-mingw-w64-i686 gcc-mingw-w64-x86-64 make gcc -y
RUN wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN rm packages-microsoft-prod.deb 

# Install .NET SDK
RUN  apt-get update; \
  apt-get install -y apt-transport-https && \
  apt-get update && \
  apt-get install -y dotnet-sdk-7.0

RUN mkdir -p ${GOPATH}/src ${GOPATH}/bin

COPY ["requirements.txt", "/requirements.txt"]
RUN pip install -r /requirements.txt
RUN pip install -i https://test.pypi.org/simple/ donut-shellcode
ENTRYPOINT ["/Mythic/mythic/payload_service.sh"]