import cassio
import os
from fastapi import FastAPI, UploadFile, HTTPException
from pydantic import BaseModel
from PyPDF2 import PdfReader
from io import BytesIO
from langchain.text_splitter import CharacterTextSplitter
from langchain_community.vectorstores.cassandra import Cassandra
from langchain_openai import OpenAIEmbeddings, OpenAI
from langchain.chains import RetrievalQA
import openai
from cassandra.cluster import Cluster

class QuestionRequest(BaseModel):
    tenant_id: str
    question: str

app = FastAPI()

def init_database():
    cassio.init(
        token=os.getenv("ASTRA_DB_TOKEN"),
        database_id=os.getenv("ASTRA_DB_ID")
    )

def process_pdf(file_bytes, tenant_id):
    reader = PdfReader(BytesIO(file_bytes))
    print("Processing PDF file...")
    text = ''
    for page in reader.pages:
        text += page.extract_text() or ''
    
    chunks = CharacterTextSplitter(
        separator="\n",
        chunk_size=800,
        chunk_overlap=200,
        length_function=len
    ).split_text(text)

    print("Text chunks: ", chunks)
    
    embedding_model = OpenAIEmbeddings(model="text-embedding-ada-002")
    
    cluster = Cluster(['cassandra'], port=9042)
    session = cluster.connect()
    
    safeId = tenant_id.replace('-', '_')
    
    session.execute("""
    CREATE KEYSPACE IF NOT EXISTS shared_keyspace 
    WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1}
    """)
    
    session.set_keyspace('shared_keyspace')
    
    vector_store = Cassandra(
        session=session,
        embedding=embedding_model,
        table_name=f"qa_data_{safeId}",
        keyspace="shared_keyspace"
    )
    
    vector_store.add_texts(chunks[:50])
    return vector_store

@app.post("/upload")
async def upload_pdf(file: UploadFile, tenant_id: str):
    if not file.filename.endswith('.pdf'):
        raise HTTPException(400, "Only PDF files allowed")
    content = await file.read()
    vector_store = process_pdf(content, tenant_id)
    return {"message": "PDF processed successfully"}

@app.post("/query")
async def query_pdf(request: QuestionRequest):
    print(QuestionRequest);
    try:
        safe_tenant_id = request.tenant_id.replace('-', '_')
        
        cluster = Cluster(['cassandra'], port=9042)
        session = cluster.connect()
        session.set_keyspace('shared_keyspace')
        
        embedding_model = OpenAIEmbeddings(
            model="text-embedding-ada-002",
            openai_api_key=os.getenv("OPENAI_API_KEY")
        )
        
        vector_store = Cassandra(
            session=session,
            embedding=embedding_model,
            table_name=f"qa_data_{safe_tenant_id}",
            keyspace="shared_keyspace"
        )
        
        retriever = vector_store.as_retriever(
            search_type="similarity",
            search_kwargs={"k": 3}
        )
        
        llm = OpenAI(
            temperature=0,
            openai_api_key=os.getenv("OPENAI_API_KEY")
        )
        
        qa_chain = RetrievalQA.from_chain_type(
            llm=llm,
            chain_type="stuff",
            retriever=retriever,
            return_source_documents=True
        )
        
        result = qa_chain({"query": request.question})
        
        return {
            "answer": result["result"],
            "source_documents": [doc.page_content for doc in result["source_documents"]]
        }
        
    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail=f"Error querying document: {str(e)}"
        )

if __name__ == "__main__":
    init_database()
    uvicorn.run(app, host="0.0.0.0", port=8000)