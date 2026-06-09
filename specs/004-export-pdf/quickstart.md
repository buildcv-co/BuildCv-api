# Quickstart: 004-export-pdf

**Date**: 2026-06-08

## Pre-requisitos

- .NET 10 SDK
- Spec-kit + gentle-ai corriendo
- (M0 + M1 ya implementados: score engine + adapt)

## Setup local

```bash
cd ~/Dev/portfolio/buildCV/BuildCv-api
dotnet restore
dotnet build
```

## Tests TDD (red → green → refactor)

```bash
# 1. Tests rojos PRIMERO (deben fallar al inicio)
dotnet test --filter "FullyQualifiedName~Export"
# Expected: 100% fail (la feature no existe aún)

# 2. Implementar lo mínimo para verde
# ... escribir Domain/Export/ + Application/Features/Export/ + Infrastructure/Pdf/ + Api/Endpoints/ExportEndpoints.cs ...

# 3. Re-ejecutar tests
dotnet test --filter "FullyQualifiedName~Export"
# Expected: 100% pass

# 4. Refactor + verificación final
dotnet test
dotnet build BuildCv.slnx -c Release    # 0 warnings
dotnet format --verify-no-changes       # limpio
```

## Test end-to-end manual

```bash
# 1. Arrancar dev environment
cd ~/Dev/portfolio/buildCV
./scripts/dev.sh

# 2. Health check
curl http://localhost:5080/health/ready

# 3. Export PDF (happy path, sin invenciones)
curl -X POST http://localhost:5080/api/v1/export \
  -H "Content-Type: application/json" \
  -d '{
    "adaptedCv": "# Juan Pérez\n\n## Resumen\nBackend developer con 2 años de experiencia en C# y .NET.\n\n## Experiencia\n- Acme Corp · Developer · 2024-2026",
    "validation": {
      "isValid": true,
      "severity": "None",
      "inventions": [],
      "warnings": []
    },
    "candidateName": "Juan Pérez"
  }' \
  --output cv-test.pdf
# Expected: HTTP 200, archivo cv-test.pdf descargado (~50-200kB)

# 4. Verificar el PDF
file cv-test.pdf
# Expected: "PDF document, version 1.x"

# 5. Verificar la marca de agua en el footer (abrir con PDF reader)
# Expected: "Generado por BuildCv · v0 · 2026-06-08"
#           "No es un puntaje ATS oficial"

# 6. Test 422 (Hard invención bloquea)
curl -X POST http://localhost:5080/api/v1/export \
  -H "Content-Type: application/json" \
  -d '{
    "adaptedCv": "CV con trampa",
    "validation": {
      "isValid": false,
      "severity": "Critical",
      "inventions": [
        { "type": "Company", "claimed": "FakeCorp", "original": null, "severity": "Hard", "position": 0 }
      ],
      "warnings": []
    },
    "candidateName": "Test"
  }' -w "\nHTTP %{http_code}\n" | tail -5
# Expected: HTTP 422 con detalle mencionando "FakeCorp"

# 7. Test rate-limit "export" (20/h por IP)
# Hacer 21 requests → el 21º recibe 429
for i in {1..21}; do
  HTTP=$(curl -sS -o /dev/null -w "%{http_code}" -X POST http://localhost:5080/api/v1/export \
    -H "Content-Type: application/json" \
    -d "{\"adaptedCv\":\"test $i\",\"validation\":{\"isValid\":true,\"severity\":\"None\",\"inventions\":[],\"warnings\":[]},\"candidateName\":\"Test\"}")
  echo "Req $i: HTTP $HTTP"
done
# Expected: req 1-20 → 200, req 21 → 429
```

## Verificación pre-merge

```bash
# 1. Pre-flight
./scripts/preflight.sh
# Expected: all green, exit 0

# 2. Constitution check
./scripts/constitution-check.sh
# Expected: 19/19 passes, 0 critical

# 3. Si ambos pasan, abrir PR con:
gh pr create --title "feat(004-export-pdf): export CV adaptado a PDF" \
             --body "Implements FR-032..FR-035, FR-046, FR-049. Cite Constitution Art. I, III, IV."
```

## Troubleshooting

- **QuestPDF license error**: verificar que `QuestPDF.Settings.License = LicenseType.Community;` esté en `Program.cs`.
- **PDF malformado con caracteres especiales**: verificar que el CV use UTF-8 (no Latin-1).
- **PDF pesa >500kB**: el CV es muy largo (>20k chars). Considerar truncar secciones largas.
- **Timeout en generación**: si tarda >10s, verificar CPU del server. QuestPDF es CPU-bound.
- **Memory pressure**: para CVs >50k chars, aumentar el límite de memoria del container.

## Tareas OpenSpec

Las tareas TDD-ordered están en `tasks.md`. Cada task es independiente y testeable.
