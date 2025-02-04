FROM python:3.9-slim

WORKDIR /app

# Copy only requirements first to leverage Docker cache
COPY requirements.txt .

# Install dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy the rest of the application
COPY . .

# Expose the port the app runs on
EXPOSE 5000

# Use uvicorn with host 0.0.0.0 to make it accessible outside the container
CMD ["uvicorn", "pdf_processor:app", "--host", "0.0.0.0", "--port", "5000"]