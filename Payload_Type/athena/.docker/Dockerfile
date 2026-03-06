FROM ubuntu:24.04

ARG DOTNET_URL=https://dot.net/v1/dotnet-install.sh
RUN mkdir /Mythic/
WORKDIR /Mythic/

RUN apt-get update -y && apt-get upgrade -y && \
    apt-get install --no-install-recommends -y \
        apt-utils \
        binutils-aarch64-linux-gnu \
        build-essential \
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
        python3.12-dev \
        python3-pip \
        software-properties-common \
        tk-dev \
        wget \
        zlib1g-dev && \
    rm -rf /var/lib/apt/lists/*

RUN python3 -m pip install pycryptodome mythic-container pefile py2app --break-system-packages

ENV PATH="${PATH}:/root/.dotnet/:/root/.dotnet/tools"
ENV DOTNET_ROOT="/root/.dotnet/"

RUN wget ${DOTNET_URL} -O dotnet-install.sh && \
    chmod +x ./dotnet-install.sh && \
    ./dotnet-install.sh --channel 10.0 && \
    dotnet tool install Obfuscar.GlobalTool -g && \
    rm dotnet-install.sh

RUN python3 -m pip install git+https://github.com/MEhrn00/donut.git@v2.0.0 --break-system-packages

COPY [".", "."]

CMD ["python3", "/Mythic/main.py"]
