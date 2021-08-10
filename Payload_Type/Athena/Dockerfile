from mcr.microsoft.com/dotnet/sdk:5.0
RUN apt-get install software-properties-common apt-utils make build-essential libssl-dev zlib1g-dev libbz2-dev \
    libreadline-dev libsqlite3-dev wget curl llvm libncurses5-dev libncursesw5-dev \
    xz-utils tk-dev libffi-dev liblzma-dev -y
RUN wget https://www.python.org/ftp/python/3.8.6/Python-3.8.6.tgz
RUN tar xvf Python-3.8.6.tgz
RUN Python-3.8.6/configure --with-ensurepip=install
RUN make -j8
RUN make altinstall
COPY ["requirements.txt", "/requirements.txt"]
RUN python3.8 -m pip install -r /requirements.txt


ENTRYPOINT ["/Mythic/mythic/payload_service.sh"]