FROM itsafeaturemythic/mythic_base_image:0.0.1

COPY ["requirements.txt", "/requirements.txt"]
RUN pip install -r /requirements.txt
WORKDIR /Mythic/
CMD ["python3", "main.py"]