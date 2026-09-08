import base64
import binascii
import logging
from contextlib import asynccontextmanager

import cv2
import numpy as np
from deepface import DeepFace
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

MODEL_NAME = "Facenet512"
DETECTOR_BACKEND = "opencv"
EMBEDDING_LENGTH = 512

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("face-service")


class RepresentRequest(BaseModel):
    image: str


class RepresentResponse(BaseModel):
    is_real: bool
    antispoof_score: float
    embedding: list[float]


# Nạp sẵn model lúc khởi động để request đầu tiên không phải chờ tải weights
def warm_up_models():
    blank_face = np.zeros((160, 160, 3), dtype=np.uint8)
    try:
        DeepFace.represent(
            blank_face,
            model_name=MODEL_NAME,
            detector_backend="skip",
            enforce_detection=False,
        )
        logger.info("Recognition model %s loaded", MODEL_NAME)
    except Exception as error:
        logger.warning("Cannot warm up recognition model: %s", error)

    blank_frame = np.zeros((224, 224, 3), dtype=np.uint8)
    try:
        DeepFace.extract_faces(
            blank_frame,
            detector_backend=DETECTOR_BACKEND,
            anti_spoofing=True,
            enforce_detection=False,
        )
        logger.info("Anti-spoofing model loaded")
    except Exception as error:
        logger.warning("Cannot warm up anti-spoofing model: %s", error)


@asynccontextmanager
async def lifespan(app: FastAPI):
    warm_up_models()
    yield


app = FastAPI(title="MiniBank Face Service", lifespan=lifespan)


# Chuyển chuỗi base64 (có hoặc không có tiền tố data URL) thành ảnh BGR cho OpenCV
def decode_image(image_base64: str) -> np.ndarray:
    raw = image_base64.split(",", 1)[-1].strip()
    if not raw:
        raise HTTPException(status_code=400, detail="Image is empty")

    try:
        image_bytes = base64.b64decode(raw, validate=True)
    except (binascii.Error, ValueError):
        raise HTTPException(status_code=400, detail="Image is not valid base64")

    buffer = np.frombuffer(image_bytes, dtype=np.uint8)
    image = cv2.imdecode(buffer, cv2.IMREAD_COLOR)
    if image is None:
        raise HTTPException(status_code=400, detail="Cannot decode image")

    return image


# Tách đúng một khuôn mặt trong ảnh và chấm điểm anti-spoofing cho khuôn mặt đó
def extract_single_face(image: np.ndarray) -> dict:
    try:
        faces = DeepFace.extract_faces(
            image,
            detector_backend=DETECTOR_BACKEND,
            anti_spoofing=True,
        )
    except ValueError:
        raise HTTPException(status_code=400, detail="No face detected in the image")
    except Exception as error:
        logger.exception("Face extraction failed")
        raise HTTPException(status_code=500, detail=f"Face extraction failed: {error}")

    if not faces:
        raise HTTPException(status_code=400, detail="No face detected in the image")
    if len(faces) > 1:
        raise HTTPException(status_code=400, detail="More than one face detected, keep only one person in frame")

    return faces[0]


# Sinh embedding 512 chiều từ khuôn mặt đã được cắt sẵn, không detect lại lần nữa
def build_embedding(face: dict) -> list[float]:
    face_pixels = (face["face"] * 255).astype(np.uint8)
    face_bgr = cv2.cvtColor(face_pixels, cv2.COLOR_RGB2BGR)

    try:
        results = DeepFace.represent(
            face_bgr,
            model_name=MODEL_NAME,
            detector_backend="skip",
            enforce_detection=False,
        )
    except Exception as error:
        logger.exception("Embedding failed")
        raise HTTPException(status_code=500, detail=f"Cannot build embedding: {error}")

    embedding = results[0]["embedding"]
    if len(embedding) != EMBEDDING_LENGTH:
        raise HTTPException(status_code=500, detail=f"Unexpected embedding length {len(embedding)}")

    return embedding


@app.get("/health")
def health():
    return {"status": "ok", "model": MODEL_NAME, "embeddingLength": EMBEDDING_LENGTH}


@app.post("/represent", response_model=RepresentResponse)
def represent(request: RepresentRequest):
    image = decode_image(request.image)
    face = extract_single_face(image)

    is_real = bool(face.get("is_real", False))
    antispoof_score = float(face.get("antispoof_score", 0.0))

    # Ảnh giả thì dừng luôn, không tốn thêm thời gian sinh embedding
    if not is_real:
        logger.warning("Spoof detected, score %.3f", antispoof_score)
        return RepresentResponse(is_real=False, antispoof_score=antispoof_score, embedding=[])

    embedding = build_embedding(face)
    logger.info("Embedding created, antispoof score %.3f", antispoof_score)

    return RepresentResponse(is_real=True, antispoof_score=antispoof_score, embedding=embedding)
