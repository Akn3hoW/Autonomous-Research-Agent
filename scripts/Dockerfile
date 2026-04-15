FROM python:3.11.8-slim

WORKDIR /app

COPY scripts/requirements-local-ml.txt .

RUN pip install --no-cache-dir -r requirements-local-ml.txt

COPY scripts/local_embedding_service.py .

EXPOSE 8001

CMD ["python", "local_embedding_service.py"]
