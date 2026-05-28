# AutoDoc

> Automatically generate enriched OpenAPI 3.0.3 documentation from raw C# ASP.NET Core controllers using local AI - no manual annotations required.
<img width="1912" height="912" alt="Screenshot 2026-05-28 135113" src="https://github.com/user-attachments/assets/1fe9dd36-c3f2-47f9-a7e8-dde24da477a2" />

---

## What it does

Paste any ASP.NET Core controller. AutoDoc reads the raw C# code and infers everything automatically:

- Routes and HTTP methods
- Operation IDs and summaries
- Request body schemas
- Response codes (200, 201, 400, 404, 500)
- Error schemas
- Live Swagger Preview

No `[ProducesResponseType]`. No `[SwaggerOperation]`. No XML comments. Just raw C# code.

---

## How it was built

| Phase | What it is |
|---|---|
| Phase 1 - Console App | Proof of concept, YAML printed to terminal |
| Phase 2 - Docker Web API | REST backend, callable via HTTP POST |
| Phase 3 - Playground UI | Browser dashboard, paste and generate instantly |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.ai) installed and running
- Llama 3.2 model pulled

```bash
ollama pull llama3.2
```

---

## Run locally

```bash
# Clone the repo
git clone https://github.com/jmy744/AutoDoc
cd AutoDoc/autodoc-api

# Start Ollama (in a separate terminal)
ollama serve

# Run the API
dotnet run
```

Open your browser at `http://localhost:5250`

---

## Run with Docker

```bash
# Make sure Ollama is running on your machine first
ollama serve

# Build and run
cd AutoDoc/autodoc-api
docker build -t autodoc .
docker run -p 5250:5250 -e OLLAMA_HOST=http://host.docker.internal:11434 autodoc
```

Open your browser at `http://localhost:5250`

---

## Using the Playground UI

1. Open `http://localhost:5250`
2. The sample `TodoController` is pre-filled — or paste your own
3. Click **Generate OpenAPI Docs**
4. Switch between **Raw YAML** and **Swagger Preview** tabs

---

## API endpoint

```http
POST /generate-openapi
Content-Type: application/json

{
  "controllerCode": "your C# controller code here",
  "controllerName": "TodoController"
}
```

Response:
```json
{
  "controllerName": "TodoController",
  "yaml": "openapi: 3.0.3 ...",
  "lines": 87,
  "generationTime": "12 seconds"
}
```

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10 Minimal API |
| AI Model | Llama 3.2 via Ollama |
| Frontend | Vanilla HTML/CSS/JS |
| Swagger Render | Swagger UI 5 |
| Containerisation | Docker |

---

## Built with GitHub Copilot

This project was built as part of the [GitHub Finish-Up-A-Thon Challenge](https://dev.to/challenges/github-2026-05-21).
GitHub Copilot was used throughout for the Docker API conversion, the Playground UI, and the YAML post-processing pipeline.

---

## License

MIT
