from mcr.microsoft.com/dotnet/sdk:5.0

RUN apt update
RUN apt install build-essential zlib1g-dev libncurses5-dev libgdbm-dev libnss3-dev libssl-dev libreadline-dev libffi-dev libsqlite3-dev wget libbz2-dev -y
RUN wget https://www.python.org/ftp/python/3.8.6/Python-3.8.6.tgz

RUN tar xvf Python-3.8.6.tgz
RUN Python-3.8.6/configure --with-ensurepip=install --enable-optimizations
RUN make -j8
RUN make altinstall
COPY ["requirements.txt", "/requirements.txt"]
RUN python3.8 -m pip install -r /requirements.txt


ENTRYPOINT ["/Mythic/mythic/payload_service.sh"]