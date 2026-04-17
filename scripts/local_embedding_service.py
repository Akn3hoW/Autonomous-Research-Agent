#!/usr/bin/env python3
import os
import asyncio
from concurrent.futures import ThreadPoolExecutor
from functools import lru_cache
from typing import Any
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException, Request, Response
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field
from sentence_transformers import SentenceTransformer
import uvicorn
from collections import deque


class EmbeddingRequest(BaseModel):
    input: str | list[str]
    model: str | None = None
    input_type: str = Field(default="document")


class HealthResponse(BaseModel):
    status: str
    models_loaded: list[str]
    current_queue_size: int
    max_concurrent_requests: int


class ModelStatus(BaseModel):
    name: str
    device: str
    loaded: bool
    last_used: float | None


MAX_CONCURRENT_REQUESTS = int(os.getenv("ARA_EMBED_MAX_CONCURRENT", "3"))
MAX_QUEUE_SIZE = int(os.getenv("ARA_EMBED_QUEUE_SIZE", "50"))
DEFAULT_MODEL = os.getenv("ARA_EMBED_MODEL", "Snowflake/snowflake-arctic-embed-m-v1.5")

_request_queue: asyncio.Queue[tuple[Any, Any, Any]] = asyncio.Queue(
    maxsize=MAX_QUEUE_SIZE
)
_executor = ThreadPoolExecutor(max_workers=MAX_CONCURRENT_REQUESTS)
_active_requests = 0
_model_cache: dict[str, SentenceTransformer] = {}
_model_status: dict[str, ModelStatus] = {}
_last_model_access: dict[str, float] = {}


def _normalize_input(text: str, input_type: str) -> tuple[str, str | None]:
    normalized_type = (input_type or "document").strip().lower()
    if normalized_type == "query":
        return text, "query"
    return text, None


def _get_available_models() -> list[str]:
    models_env = os.getenv("ARA_EMBED_AVAILABLE_MODELS", DEFAULT_MODEL)
    return [m.strip() for m in models_env.split(",") if m.strip()]


@lru_cache(maxsize=4)
def _load_model_cached(model_name: str) -> SentenceTransformer:
    device = os.getenv("ARA_EMBED_DEVICE", "cpu")
    model = SentenceTransformer(model_name, device=device)
    return model


def load_model(model_name: str) -> SentenceTransformer:
    global _model_cache, _model_status, _last_model_access

    if model_name not in _model_cache:
        device = os.getenv("ARA_EMBED_DEVICE", "cpu")
        _model_cache[model_name] = SentenceTransformer(model_name, device=device)
        _model_status[model_name] = ModelStatus(
            name=model_name, device=device, loaded=True, last_used=None
        )

    _last_model_access[model_name] = asyncio.get_event_loop().time()
    _model_status[model_name].last_used = _last_model_access[model_name]
    return _model_cache[model_name]


def encode_texts(
    model_name: str, texts: list[str], input_type: str
) -> list[list[float]]:
    model = load_model(model_name)
    prepared: list[str] = []
    prompt_name: str | None = None

    for text in texts:
        normalized_text, candidate_prompt_name = _normalize_input(text, input_type)
        prepared.append(normalized_text)
        prompt_name = candidate_prompt_name

    try:
        embeddings = model.encode(
            prepared,
            convert_to_numpy=True,
            normalize_embeddings=False,
            prompt_name=prompt_name,
        )
    except Exception:
        embeddings = model.encode(
            prepared,
            convert_to_numpy=True,
            normalize_embeddings=False,
        )

    return [row.astype("float32").tolist() for row in embeddings]


@asynccontextmanager
async def lifespan(app: FastAPI):
    for model_name in _get_available_models():
        try:
            load_model(model_name)
        except Exception as e:
            print(f"Failed to preload model {model_name}: {e}")

    asyncio.create_task(_process_queue())
    yield


app = FastAPI(title="ARA Local Embedding Service", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    return HealthResponse(
        status="ok",
        models_loaded=list(_model_cache.keys()),
        current_queue_size=_request_queue.qsize(),
        max_concurrent_requests=MAX_CONCURRENT_REQUESTS,
    )


@app.get("/models")
def list_models() -> dict[str, Any]:
    return {
        "available": _get_available_models(),
        "loaded": list(_model_cache.keys()),
        "status": {name: status.model_dump() for name, status in _model_status.items()},
        "max_concurrent": MAX_CONCURRENT_REQUESTS,
        "max_queue_size": MAX_QUEUE_SIZE,
    }


@app.post("/embeddings")
def embeddings(request: EmbeddingRequest) -> dict[str, Any]:
    global _active_requests

    model_name = request.model or DEFAULT_MODEL

    if _active_requests >= MAX_CONCURRENT_REQUESTS:
        if _request_queue.full():
            raise HTTPException(
                status_code=503, detail="Server overload, queue is full"
            )

        future = asyncio.run_coroutine_threadsafe(
            _enqueue_and_wait(model_name, request), asyncio.get_event_loop()
        )
        return future.result(timeout=300)

    _active_requests += 1
    try:
        if isinstance(request.input, list):
            vectors = encode_texts(model_name, request.input, request.input_type)
            data = [
                {"index": index, "embedding": vector}
                for index, vector in enumerate(vectors)
            ]
            return {"object": "list", "model": model_name, "data": data}

        vector = encode_texts(model_name, [request.input], request.input_type)[0]
        return {"model": model_name, "embedding": vector}
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc
    finally:
        _active_requests -= 1


async def _enqueue_and_wait(
    model_name: str, request: EmbeddingRequest
) -> dict[str, Any]:
    future: asyncio.Future = asyncio.Future()

    await _request_queue.put((model_name, request, future))

    try:
        return await asyncio.wait_for(future, timeout=300)
    except asyncio.TimeoutError:
        raise HTTPException(status_code=504, detail="Request timed out in queue")


async def _process_queue():
    global _active_requests

    while True:
        try:
            model_name, request, future = await asyncio.wait_for(
                _request_queue.get(), timeout=1.0
            )

            while _active_requests >= MAX_CONCURRENT_REQUESTS:
                await asyncio.sleep(0.1)

            _active_requests += 1
            try:
                loop = asyncio.get_event_loop()
                result = await loop.run_in_executor(
                    _executor, _process_embedding_request, model_name, request
                )
                future.set_result(result)
            except Exception as e:
                future.set_exception(e)
            finally:
                _active_requests -= 1

        except asyncio.TimeoutError:
            continue
        except Exception as e:
            print(f"Queue processing error: {e}")
            await asyncio.sleep(1.0)


def _process_embedding_request(
    model_name: str, request: EmbeddingRequest
) -> dict[str, Any]:
    if isinstance(request.input, list):
        vectors = encode_texts(model_name, request.input, request.input_type)
        data = [
            {"index": index, "embedding": vector}
            for index, vector in enumerate(vectors)
        ]
        return {"object": "list", "model": model_name, "data": data}

    vector = encode_texts(model_name, [request.input], request.input_type)[0]
    return {"model": model_name, "embedding": vector}


@app.get("/stats")
def stats() -> dict[str, Any]:
    return {
        "active_requests": _active_requests,
        "queue_size": _request_queue.qsize(),
        "max_concurrent": MAX_CONCURRENT_REQUESTS,
        "max_queue_size": MAX_QUEUE_SIZE,
        "models_loaded": list(_model_cache.keys()),
    }


if __name__ == "__main__":
    host = os.getenv("ARA_EMBED_HOST", "127.0.0.1")
    port = int(os.getenv("ARA_EMBED_PORT", "8001"))
    uvicorn.run(app, host=host, port=port)
