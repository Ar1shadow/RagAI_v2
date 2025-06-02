# RagAI_v2/Extensions/Python/DocLoader.py

from pathlib import Path
from typing import Iterable, Union, Dict, Optional, Iterator, List
from docling.chunking import HybridChunker
from docling.pipeline.simple_pipeline import SimplePipeline
from langchain_docling import DoclingLoader
from docling.datamodel.pipeline_options import (EasyOcrOptions, PdfPipelineOptions,TableStructureOptions)
from docling.datamodel.base_models import InputFormat
from docling.document_converter import DocumentConverter, PdfFormatOption, WordFormatOption
from langchain_docling.loader import ExportType
from langchain_text_splitters import MarkdownHeaderTextSplitter, RecursiveCharacterTextSplitter
from langchain_core.documents import Document
from langchain_community.vectorstores.utils import filter_complex_metadata
from transformers import AutoTokenizer


# c'est default dans le processus de Docling
EMBED_MODEL = "sentence-transformers/all-MiniLM-L6-v2"

# Détection de types MIME à partir d'un chemin de fichier
class MimeTypesDetection:

    def __init__(self):
        # Types MIME supportés pour la lecture
        self._support_types = {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.ms-powerpoint",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "text/markdown",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain",
            "text/html",
        }

        # Extension de fichiers mappées aux types MIME
        self._extension_types: Dict[str, str] = {
            ".txt": "text/plain",
            ".md": "text/markdown",
            ".htm": "text/html",
            ".html": "text/html",
            ".xhtml": "application/xhtml+xml",
            ".xml": "application/xml",
            ".jsonld": "application/ld+json",
            ".css": "text/css",
            ".js": "text/javascript",
            ".sh": "application/x-sh",
            ".bmp": "image/bmp",
            ".gif": "image/gif",
            ".jpeg": "image/jpeg",
            ".jpg": "image/jpeg",
            ".png": "image/png",
            ".tiff": "image/tiff",
            ".tif": "image/tiff",
            ".webp": "image/webp",
            ".svg": "image/svg+xml",
            ".url": "text/x-uri",
            ".text_embedding": "float[]",
            ".json": "application/json",
            ".csv": "text/csv",
            ".pdf": "application/pdf",
            ".rtf": "application/rtf",
            ".doc": "application/msword",
            ".docx": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".ppt": "application/vnd.ms-powerpoint",
            ".pptx": "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".xls": "application/vnd.ms-excel",
            ".xlsx": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".odt": "application/vnd.oasis.opendocument.text",
            ".ods": "application/vnd.oasis.opendocument.spreadsheet",
            ".odp": "application/vnd.oasis.opendocument.presentation",
            ".epub": "application/epub+zip",
            ".aac": "audio/aac",
            ".mp3": "audio/mpeg",
            ".wav": "audio/wav",
            ".oga": "audio/ogg",
            ".opus": "audio/opus",
            ".weba": "audio/webm",
            ".mp4": "video/mp4",
            ".mpeg": "video/mpeg",
            ".ogv": "video/ogg",
            ".ogx": "application/ogg",
            ".webm": "video/webm",
            ".tar": "application/x-tar",
            ".gz": "application/gzip",
            ".zip": "application/zip",
            ".rar": "application/vnd.rar",
            ".7z": "application/x-7z-compressed"
        }

    def support_type(self, filename: str) -> bool:
        # Vérifie si le type MIME d’un fichier est pris en charge
        return self.try_get_file_type(filename) in self._support_types

    def get_file_type(self, filename: str) -> str:
        # Retourne le type MIME d’un fichier ou lève une erreur si non reconnu
        extension = Path(filename).suffix.lower()
        if extension in self._extension_types:
            return self._extension_types[extension]
        raise ValueError(f"File type not supported: {filename}")

    def try_get_file_type(self, filename: str) -> Optional[str]:
        # Retourne le type MIME d’un fichier, ou None si inconnu
        extension = Path(filename).suffix.lower()
        return self._extension_types.get(extension)



# Chargeur de documents intelligent selon le type MIME détecté
class DocLoaders:
    '''
        Si des nouveaux loder vont être ajoutés, 
        modifiez la liste support_type dans la classe MimeTypesDetection,
        modifiez Méthodes load dans la classe Docloaders,
        implementez le nouveau loader
    '''
    def __init__(self,file_path: Union[str, Iterable[str]]):

        # Gère un ou plusieurs chemins de fichiers
        self._file_paths = (
            file_path
            if isinstance(file_path, Iterable) and not isinstance(file_path, str)
            else [file_path]
        )
        valide_files = []
        self._mimetype_detector = MimeTypesDetection()
        # ignorer les fichiers de type non supporté
        for file in self._file_paths:
            if not self._mimetype_detector.support_type(file):
                print(f"Unsupported file type: {self._mimetype_detector.get_file_type(file)}")
                continue
            else:
                valide_files.append(file)               
        
        self._file_paths = valide_files
        self.MAX_Tokens = 1000
        self.MODEL_ID = EMBED_MODEL # default model
        self.tokenizer = AutoTokenizer.from_pretrained(self.MODEL_ID)
        
    # Traitement pour fichier PDF
    def __pdf_loader(self):
        pipeline_options = PdfPipelineOptions(
            do_table_structure = False,  # True: perform table structure extraction
            do_ocr = True, # True: perform OCR, replace programmatic PDF text
            table_structure_options=TableStructureOptions(do_cell_matching=False),
            ocr_options=EasyOcrOptions(force_full_page_ocr=True)
        )
        #_loader = PyMuPDFLoader(self._file_paths)
        doc_convertor = DocumentConverter(
            format_options={InputFormat.PDF: PdfFormatOption(pipeline_options=pipeline_options)}
        )
        _loader = DoclingLoader(self._file_paths,converter=doc_convertor, chunker=HybridChunker(tokenizer=self.tokenizer,max_tokens=self.MAX_Tokens, merge_peers=True,))
        #filter_documents = filter_complex_metadata(_loader.load())
        #return filter_documents
        return _loader.load()

    # Traitement pour document Word (.docx)
    def __word_loader(self):
        doc_convertor = DocumentConverter(
            allowed_formats=[InputFormat.DOCX],
            format_options={InputFormat.DOCX: WordFormatOption(pipeline_cls=SimplePipeline)}
        )
        _loader = DoclingLoader(self._file_paths,converter=doc_convertor,chunker=HybridChunker(max_tokens=self.MAX_Tokens, merge_peers=True,))
        filter_documents = filter_complex_metadata(_loader.load())
       
        
        return filter_documents
     # Traitement pour fichier PowerPoint (.pptx) /  Excel (.xlsx) / HTML
    def __general_loader__(self):
        doc_convertor = DocumentConverter(
            allowed_formats=[
                InputFormat.PPTX,
                InputFormat.XLSX,
                InputFormat.HTML,
                ],
        )
        _loader = DoclingLoader(self._file_paths, converter=doc_convertor,chunker=HybridChunker(max_tokens=self.MAX_Tokens, merge_peers=True,))
        filter_documents = filter_complex_metadata(_loader.load())
        return filter_documents


    # Traitement pour fichier Markdown (.md)
    def __md_loader(self):
        doc_convertor = DocumentConverter(
            allowed_formats=[InputFormat.MD],
        )
        _loader = DoclingLoader(self._file_paths, converter=doc_convertor,export_type=ExportType.MARKDOWN,chunker=HybridChunker(max_tokens=self.MAX_Tokens, merge_peers=True,))

        _docs = _loader.load()
        splitter = MarkdownHeaderTextSplitter(headers_to_split_on=[
            ("#", "Header_1"),
            ("##", "Header_2"),
            ("###", "Header_3"),
        ])
        splits = [split for _doc in _docs for split in splitter.split_text(_doc.page_content)]
        filter_documents = filter_complex_metadata(splits)
        return filter_documents


    # Traitement pour plain text(.txt)
    class __text_loader__:
        def __init__(self, file_path: str, chunk_size :int = 1000, chunk_overlap : int = 200):
            self.file_path = file_path
            self.chunk_size = chunk_size
            self.chunk_overlap = chunk_overlap


        def load(self) -> Iterator[Document] :

            if not self.file_path.lower().endswith(".txt"):
                raise ValueError(f"Type de fichier non supporté pour txt chargeur : {self.file_path}")

            with open(self.file_path, "r" , encoding="utf-8") as f:
                raw_text = f.read()
            splitter = RecursiveCharacterTextSplitter(
                separators=["\n\n","\r","\n",".",""," "],
                chunk_size = self.chunk_size,
                chunk_overlap = self.chunk_overlap
                )
            chunks = splitter.split_text(raw_text)

            documents = [Document(page_content=chunk) for chunk in chunks]
            return documents
        

    def load(self):
        # Sélectionne dynamiquement le bon chargeur en fonction du type MIME
        for file in self._file_paths:
            _type = self._mimetype_detector.get_file_type(file)
            if _type == "application/pdf":
                return self.__pdf_loader()
            if _type == "application/msword" or _type == "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
                return self.__word_loader()
            if _type == "application/vnd.ms-powerpoint" or _type == "application/vnd.openxmlformats-officedocument.presentationml.presentation":
                return self.__general_loader__()
            if _type == "text/markdown":
                return self.__md_loader()
            if _type == "application/vnd.ms-excel" or _type == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or _type == "text/html":
                return self.__general_loader__()

            if _type == "text/plain":
                return self.__text_loader__(file).load()


def merge_chunks_by_headings(docs : list[Document])-> list[Document]:
    from collections import defaultdict
    """
    Merge chunks by headings.
    """
    merged = defaultdict(list)

    for doc in docs:
        headings = doc.metadata.get("dl_meta", {}).get("headings", [])
        heading = headings[0] if headings else "No_Heading"
        merged[heading].append(doc.page_content)
    merged_chunks = []
  
    for heading, contents in merged.items():
       merged_chunks.append(Document(page_content="\n".join(contents), metadata={"heading": heading}))
    return merged_chunks



