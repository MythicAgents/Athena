#FROM itsafeaturemythic/mythic_base_image:0.0.1
FROM itsafeaturemythic/mythic_dotnet:latest

COPY ["requirements.txt", "/requirements.txt"]
RUN python3 -m pip install -r /requirements.txt
WORKDIR /Mythic/
CMD ["python3", "main.py"]