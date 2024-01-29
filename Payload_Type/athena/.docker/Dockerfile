FROM python:3.11-slim-bookworm as builder

COPY [".docker/requirements.txt", "requirements.txt"]
#force push
RUN apt-get -y update && \
    apt-get -y upgrade && \
    apt-get install --no-install-recommends \
      wget software-properties-common apt-utils make build-essential libssl-dev zlib1g-dev libbz2-dev \
      xz-utils tk-dev libffi-dev liblzma-dev libsqlite3-dev protobuf-compiler \
      binutils-aarch64-linux-gnu libc-dev-arm64-cross -y
RUN python3 -m pip wheel --wheel-dir /wheels -r requirements.txt

FROM python:3.11-slim-bookworm

COPY --from=builder /wheels /wheels

RUN apt-get -y update && apt-get install -y wget gcc libicu-dev
RUN pip install --no-cache /wheels/*
RUN pip install donut-shellcode
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
ENV PATH="${PATH}:/root/.dotnet/"
ENV PATH="${PATH}:/root/.dotnet/tools"
ENV DOTNET_ROOT="/root/.dotnet/"

RUN chmod +x ./dotnet-install.sh
RUN ./dotnet-install.sh --version 7.0.115
RUN dotnet tool install Obfuscar.GlobalTool -g

WORKDIR /Mythic/

COPY [".", "."]

CMD ["python3", "main.py"]