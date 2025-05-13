#patch_transformers.py

import os


def apply_patch():

    from transformers import ( AutoTokenizer, AutoModel )
    CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))

    MODEL_DIR_MINILM = os.path.join(CURRENT_DIR, "models/models--sentence-transformers--all-MiniLM-L6-v2")
    MODEL_DIR_DOCLING = os.path.join(CURRENT_DIR, "models/models--ds4sd--docling-models")
    LOCAL_MODEL = {
        "sentence-transformers/all-MiniLM-L6-v2" : MODEL_DIR_MINILM,
        "ds4sd/docling-models" : MODEL_DIR_DOCLING
        }
    
    def patch(cls):
        origin_fn = cls.from_pretrained

        def patched(model_id, *args, **kwargs):
            local_path = LOCAL_MODEL.get(model_id)
            if local_path :
                print(f"[patch]Rédiriger {model_id} vers {local_path}")
                model_id = local_path
                kwargs.setdefault("local_files_only", True)
            return origin_fn(model_id, *args, **kwargs)

        cls.from_pretrained = patched

        for model_cls in [
            AutoTokenizer,
            AutoModel,
        ]:
            model_cls.from_pretrained = patched(model_cls)