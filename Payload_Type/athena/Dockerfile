FROM ghcr.io/mythicagents/athena:dev

RUN cd athena/agent_code && \
    dotnet build sftp -c Release --nologo && \
    dotnet build ssh -c Release --nologo && \
    dotnet build screenshot -c Release --nologo && \
    dotnet build ds -c Release --nologo && \
    dotnet build wmi -c Release --nologo
