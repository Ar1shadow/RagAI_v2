#  RagAI_v2/Extensions/Python/chunk_api.py

from fastapi import FastAPI, Form
from pydantic import BaseModel
from typing import List, Optional, Dict
from DocLoader import loaders
import os

app = FastAPI()

@app.post("/chunk", response_model=List[str])
async def chunk_file(
        file_path: str = Form(...)
) -> List[str]:
    if os.path.isdir(file_path):
        files = [os.path.join(file_path, f) for f in os.listdir(file_path) if os.path.isfile(os.path.join(file_path, f))]
    else:
        files = [file_path]

    loader = loaders.DocLoaders(files)
    docs = loader.load()

    return [doc.page_content for doc in docs]

@app.get("/ping")
def ping():
    return {"status": "ok"}
