# run_server.py

import uvicorn
from chunk_api import app  # 确保 chunk_api.py 中定义了 app
import sys
import os

sys.path.append(os.path.dirname(__file__))
uvicorn.run(app, host="127.0.0.1", port=8000)