# 011-factus — Quickstart

## 1. Configurar credenciales Factus

```bash
cd BuildCv-api

# Agregar credenciales al user secrets
dotnet user-secrets set "Factus:ClientId" "TU_CLIENT_ID"
dotnet user-secrets set "Factus:ClientSecret" "TU_CLIENT_SECRET"
dotnet user-secrets set "Factus:Email" "TU_EMAIL"
dotnet user-secrets set "Factus:Password" "TU_PASSWORD"
```

## 2. Habilitar Factus

```json
// appsettings.Development.json
{
  "Factus": {
    "Enabled": true,
    "BaseUrl": "https://api-sandbox.factus.com.co"
  }
}
```

## 3. Crear migración

```bash
dotnet ef migrations add AddInvoicingTables --project src/BuildCv.Infrastructure
dotnet ef database update --project src/BuildCv.Infrastructure
```

## 4. Probar creación de factura

```bash
# Login primero (obtener JWT)
curl -X POST http://localhost:5080/api/auth/google \
  -H "Content-Type: application/json" \
  -d '{"code":"test","redirectUri":"http://localhost:3000"}'

# Crear factura
curl -X POST http://localhost:5080/api/invoices \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "Juan Perez",
    "customerIdentification": "123456789",
    "customerEmail": "juan@email.com",
    "items": [{
      "codeReference": "CREDIT-3",
      "name": "Paquete Starter",
      "quantity": 1,
      "price": 9900,
      "taxCode": "01",
      "taxRate": 19
    }]
  }'

# Listar facturas
curl http://localhost:5080/api/invoices \
  -H "Authorization: Bearer {token}"
```

## 5. Modo sin Factus

```json
{
  "Factus": {
    "Enabled": false
  }
}
```

Las facturas se crean en estado `Draft` sin llamar a Factus.

## 6. Endpoints disponibles

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| POST | /api/invoices | Crear factura |
| GET | /api/invoices | Listar facturas |
| GET | /api/invoices/{id} | Ver factura |
| GET | /api/invoices/{id}/pdf | Descargar PDF |
| GET | /api/invoices/{id}/xml | Descargar XML |
| POST | /api/credit-notes | Crear nota crédito |
| POST | /api/support-documents | Crear documento soporte |
| GET | /api/numbering-ranges | Listar rangos |
| GET | /api/company | Ver empresa |
| PUT | /api/company | Actualizar empresa |
