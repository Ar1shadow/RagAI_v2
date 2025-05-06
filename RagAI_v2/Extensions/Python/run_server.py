# RagAI_v2/Extensions/Python/run_server.py

import uvicorn
import signal
import asyncio
from chunk_api import app  # assurer app soit bien défini dans chunk_api.py
import sys
import os

sys.path.append(os.path.dirname(__file__))

def run_uvicorn():
    host = "127.0.0.1"
    port = 8000
    config = uvicorn.Config(app, host=host, port=port, log_level="info")
    server = uvicorn.Server(config)
    
    loop = asyncio.get_event_loop()

    stop_event = asyncio.Event()

    def shutdown_signal_handler(*_):
        print("Signal reçu:fer,eture propre...")
        stop_event.set()

    if sys.platform != "win32":
        signal.signal(signal.SIGINT, shutdown_signal_handler)
        signal.signal(signal.SIGTERM, shutdown_signal_handler)

    try:
        loop.run_until_complete(start_server(server, stop_event))
    except KeyboardInterrupt:
        print("Arrêt du serveur par l'utilisateur.")
    finally:
        loop.close()

async def start_server(server, stop_event):
    server_task = asyncio.create_task(server.serve())
    print(f"Uvicorn serveur démarré")
    await stop_event.wait()

    # Arrêt du serveur
    server.should_exit = True
    await server_task
    print("Serveur arrêté proprement.")


if __name__ == "__main__":
    # chemin pour test temporaire utiliser un fichier de config ou un chemin relatif
    #os.environ["HF_HOME"] = "Z:\\Stagiaires\\Pengcheng LI\\Code\\RagAI_v2\\RagAI_v2\\Assets\\Models\\"

    run_uvicorn()
