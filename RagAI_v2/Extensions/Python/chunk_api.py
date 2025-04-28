#  RagAI_v2/Extensions/Python/chunk_api.py

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import List
import os

print("Initialisation du serveur...")

# >>>>>> Ici on ajoute un message
print("Chargement de Docling...")

from DocLoader import DocLoaders
print("Docling charg� avec succ�s.")

app = FastAPI()
print("Instance FastAPI cr��e.")

class ChunckRequest(BaseModel):
    file_path: str
    

@app.post("/chunk", response_model=List[str])
async def chunk_file(request : ChunckRequest) -> List[str]:
    file_path = request.file_path

    if not os.path.exists(file_path):
        raise HTTPException(status_code=400, detail="Le chemin n'existe pas: "+file_path)

    if os.path.isdir(file_path):
        files = [os.path.join(file_path, f) for f in os.listdir(file_path) if os.path.isfile(os.path.join(file_path, f))]
    else:
        files = [file_path]

    loader = DocLoaders(files)
    docs = loader.load()

    return [doc.page_content for doc in docs]

@app.get("/ping")
def ping():
    return {"status": "ok"}
