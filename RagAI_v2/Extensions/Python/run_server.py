# RagAI_v2/Extensions/Python/run_server.py

import uvicorn
from chunk_api import app  # assurer app soit bien défini dans chunk_api.py
import sys
import os

sys.path.append(os.path.dirname(__file__))
uvicorn.run(app, host="127.0.0.1", port=8000)