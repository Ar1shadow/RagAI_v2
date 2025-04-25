# RagAI_v2/Extensions/Python/DocLoader.py

from pathlib import Path
from typing import Iterable, Union, Dict, Optional
from docling.chunking import HybridChunker
from docling.pipeline.simple_pipeline import SimplePipeline
from langchain_docling import DoclingLoader
from docling.datamodel.pipeline_options import (AcceleratorOptions, AcceleratorDevice, PdfPipelineOptions,
                                                TableStructureOptions)
from docling.datamodel.base_models import InputFormat
from docling.document_converter import DocumentConverter, PdfFormatOption, WordFormatOption
from langchain_docling.loader import ExportType
from langchain_text_splitters import MarkdownHeaderTextSplitter

from langchain_community.vectorstores.utils import filter_complex_metadata


FILE_PATH_1 = '/Users/lipengcheng/Downloads/OCR-free.pdf'
FILE_PATH_2 = '/Users/lipengcheng/Downloads/wang2020.pdf'
FILE_PATH_3 = "/Users/lipengcheng/Downloads/evaluation_stage_47423.docx"
FILE_PATH_4 = "/Users/lipengcheng/Downloads/CuisineS3.docx"
FILE_PATH_5 = "/Users/lipengcheng/Downloads/mixed.md"
FILE_PATH_6 = "/Users/lipengcheng/Downloads/powerpoint_with_image.pptx"
FILE_PATH_7 = "/Users/lipengcheng/Downloads/test-01.xlsx"
FILE_PATH_8 = "/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/2023AnnualReport.pdf"

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
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
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
    def __init__(self,
                 file_path: Union[str, Iterable[str]]
                 ):
        # Gère un ou plusieurs chemins de fichiers
        self._file_paths = (
            file_path
            if isinstance(file_path, Iterable) and not isinstance(file_path, str)
            else [file_path]
        )
        self._mimetype_detector = MimeTypesDetection()
        for file in self._file_paths:
            if not self._mimetype_detector.support_type(file):
                raise ValueError(f"Unsupported file type: {self._mimetype_detector.get_file_type(file)}")

    # Traitement pour fichier PDF
    def __pdf_loader(self):
        pipeline_options = PdfPipelineOptions(
            do_table_structure = True,  # True: perform table structure extraction
            do_ocr = False,
            table_structure_options=TableStructureOptions(do_cell_matching=True),
            accelerator_options=AcceleratorOptions(
                num_threads=8,
                device=AcceleratorDevice.MPS
            )
        )
        #_loader = PyMuPDFLoader(self._file_paths)
        doc_convertor = DocumentConverter(
            format_options={InputFormat.PDF: PdfFormatOption(pipeline_options=pipeline_options)}
        )
        _loader = DoclingLoader(self._file_paths,converter=doc_convertor, chunker=HybridChunker())
        filter_documents = filter_complex_metadata(_loader.load())
        return filter_documents

    # Traitement pour document Word (.docx)
    def __word_loader(self):
        doc_convertor = DocumentConverter(
            allowed_formats=[InputFormat.DOCX],
            format_options={InputFormat.DOCX: WordFormatOption(pipeline_cls=SimplePipeline)}
        )
        _loader = DoclingLoader(self._file_paths,converter=doc_convertor)
        filter_documents = filter_complex_metadata(_loader.load())
        return filter_documents

    # Traitement pour fichier PowerPoint (.pptx)
    def __ppt_loader(self):
        doc_convertor = DocumentConverter(
            allowed_formats=[InputFormat.PPTX],
        )

        _loader = DoclingLoader(self._file_paths, converter=doc_convertor)
        filter_documents = filter_complex_metadata(_loader.load())
        return filter_documents

    # Traitement pour fichier Markdown (.md)
    def __md_loader(self):
        doc_convertor = DocumentConverter(
            allowed_formats=[InputFormat.MD],
        )
        _loader = DoclingLoader(self._file_paths, converter=doc_convertor,export_type=ExportType.MARKDOWN)

        _docs = _loader.load()
        splitter = MarkdownHeaderTextSplitter(headers_to_split_on=[
            ("#", "Header_1"),
            ("##", "Header_2"),
            ("###", "Header_3"),
        ])
        splits = [split for _doc in _docs for split in splitter.split_text(_doc.page_content)]
        filter_documents = filter_complex_metadata(splits)
        return filter_documents

    # Traitement pour fichier Excel (.xlsx)
    def __excel_loader(self):
        doc_convertor = DocumentConverter(
            allowed_formats=[InputFormat.XLSX],
        )
        _loader = DoclingLoader(self._file_paths, converter=doc_convertor)
        filter_documents = filter_complex_metadata(_loader.load())
        return filter_documents

    def load(self):
        # Sélectionne dynamiquement le bon chargeur en fonction du type MIME
        for file in self._file_paths:
            _type = self._mimetype_detector.get_file_type(file)
            if _type == "application/pdf":
                return self.__pdf_loader()
            if _type == "application/msword" or _type == "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
                return self.__word_loader()
            if _type == "application/vnd.ms-powerpoint" or _type == "application/vnd.openxmlformats-officedocument.presentationml.presentation":
                return self.__ppt_loader()
            if _type == "text/markdown":
                return self.__md_loader()
            if _type == "application/vnd.ms-excel" or _type == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
                return self.__excel_loader()






