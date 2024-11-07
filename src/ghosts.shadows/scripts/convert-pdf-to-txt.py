import os
import PyPDF2
import logging

# Set up logging
logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s"
)


def pdf_to_txt(pdf_path, txt_path):
    """Convert a PDF file to a text file."""
    try:
        with open(pdf_path, "rb") as pdf_file:
            reader = PyPDF2.PdfReader(pdf_file)
            with open(txt_path, "w", encoding="utf-8") as txt_file:
                for page_num in range(len(reader.pages)):
                    page = reader.pages[page_num]
                    text = page.extract_text()
                    if text:  # Check if text was extracted
                        txt_file.write(text)
                    else:
                        logging.warning(
                            f"No text extracted from page {page_num + 1} of {pdf_path}."
                        )
        logging.info(f"Successfully converted {pdf_path} to {txt_path}.")
    except Exception as e:
        logging.error(f"Failed to convert {pdf_path}: {e}")


def convert_folder_pdfs_to_txt(folder_path):
    """Convert all PDF files in a given folder to text files."""
    if not os.path.exists(folder_path):
        logging.error(f"The folder path '{folder_path}' does not exist.")
        return

    for filename in os.listdir(folder_path):
        if filename.lower().endswith(".pdf"):  # Handle case insensitivity
            pdf_path = os.path.join(folder_path, filename)
            txt_path = os.path.splitext(pdf_path)[0] + ".txt"
            pdf_to_txt(pdf_path, txt_path)


if __name__ == "__main__":
    folder_path = "../docs"
    convert_folder_pdfs_to_txt(folder_path)
