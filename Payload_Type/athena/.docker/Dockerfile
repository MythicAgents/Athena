FROM python:3.11-slim-bookworm as builder

COPY [".docker/requirements.txt", "requirements.txt"]

RUN apt-get -y update && \
    apt-get -y upgrade && \
    apt-get install --no-install-recommends \
      software-properties-common apt-utils make build-essential libssl-dev zlib1g-dev libbz2-dev \
      xz-utils tk-dev libffi-dev liblzma-dev libsqlite3-dev protobuf-compiler \
      binutils-aarch64-linux-gnu libc-dev-arm64-cross -y
RUN python3 -m pip wheel --wheel-dir /wheels -r requirements.txt

RUN apt update && apt install wget nuget -y
RUN wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN rm packages-microsoft-prod.deb
RUN  apt-get update; \
  apt-get install -y apt-transport-https && \
  apt-get update && \
  apt-get install -y dotnet-sdk-7.0
FROM python:3.11-slim-bookworm

COPY --from=builder /wheels /wheels

RUN pip install --no-cache /wheels/*
RUN dotnet tool install Obfuscar.GlobalTool -g

WORKDIR /Mythic/

COPY [".", "."]

CMD ["python3", "main.py"]