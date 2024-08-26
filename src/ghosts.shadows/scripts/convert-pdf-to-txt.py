import os
import PyPDF2

def pdf_to_txt(pdf_path, txt_path):
    with open(pdf_path, 'rb') as pdf_file:
        reader = PyPDF2.PdfReader(pdf_file)
        with open(txt_path, 'w', encoding='utf-8') as txt_file:
            for page_num in range(len(reader.pages)):
                page = reader.pages[page_num]
                text = page.extract_text()
                txt_file.write(text)

def convert_folder_pdfs_to_txt(folder_path):
    for filename in os.listdir(folder_path):
        if filename.endswith('.pdf'):
            pdf_path = os.path.join(folder_path, filename)
            txt_path = os.path.splitext(pdf_path)[0] + '.txt'
            pdf_to_txt(pdf_path, txt_path)
            print(f"Converted {filename} to text file.")

if __name__ == "__main__":
    folder_path = '../docs'
    convert_folder_pdfs_to_txt(folder_path)
