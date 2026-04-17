FROM python:3.11.8-slim

WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY scripts/requirements-local-ml.txt .

RUN pip install --no-cache-dir -r requirements-local-ml.txt

COPY scripts/local_embedding_service.py .

EXPOSE 8001

CMD ["python", "local_embedding_service.py"]
