FROM python:3.11-slim-bookworm

ARG DONUT_URL=https://github.com/MEhrn00/donut/releases/download/v2.0.0/donut_shellcode-2.0.0.tar.gz
ARG DOTNET_URL=https://dot.net/v1/dotnet-install.sh

WORKDIR /Mythic/

RUN apt-get -y update && apt-get -y upgrade
RUN apt-get install --no-install-recommends -y \
        apt-utils \
        binutils-aarch64-linux-gnu \
        build-essential \
        curl \
        gcc \
        gcc-mingw-w64 \
        git \
        libbz2-dev \
        libc-dev-arm64-cross \
        libffi-dev \
        libicu-dev \
        liblzma-dev \
        libsqlite3-dev \
        libssl-dev \
        make \
        protobuf-compiler \
        software-properties-common \
        tk-dev \
        wget \
        xz-utils \
        zlib1g-dev

# Install requirements for the project
RUN python3 -m pip install pycryptodome mythic-container pefile py2app

# RUN python3 -m pip install --no-cache /wheels/*
RUN wget ${DOTNET_URL} -O dotnet-install.sh
ENV PATH="${PATH}:/root/.dotnet/"
ENV PATH="${PATH}:/root/.dotnet/tools"
ENV DOTNET_ROOT="/root/.dotnet/"

RUN chmod +x ./dotnet-install.sh
RUN ./dotnet-install.sh --version 7.0.115
#For ARM64
RUN ./dotnet-install.sh --version 6.0.425
RUN dotnet tool install Obfuscar.GlobalTool -g

# Add Donut with ARM64 support
# RUN mkdir /tmp/donut
# RUN wget -qO- ${DONUT_URL} | tar xvz -C /tmp/donut/
# RUN cd /tmp/donut/donut_shellcode-2.0.0/ && make && cp donut /
# RUN rm -rf /tmp/donut
RUN python3 -m pip install git+https://github.com/MEhrn00/donut.git@v2.0.0
COPY [".", "."]

CMD ["python3", "/Mythic/main.py"]
