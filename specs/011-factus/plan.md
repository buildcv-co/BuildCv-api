# 011-factus — Plan técnico

## Arquitectura

```
Domain (PURO)
├── Invoicing/
│   ├── InvoiceStatus.cs          (enum: Draft, Validated, Error)
│   ├── InvoiceType.cs            (enum: Bill, CreditNote, SupportDocument)
│   ├── Invoice.cs                (entity)
│   ├── NumberingRange.cs         (entity)
│   └── CompanyInfo.cs            (value object)

Application (PUERTOS)
├── Features/Invoicing/
│   ├── IInvoiceProvider.cs       (port)
│   ├── IInvoiceStore.cs          (port)
│   ├── INumberingRangeStore.cs   (port)
│   ├── CreateInvoiceHandler.cs
│   ├── GetInvoiceHandler.cs
│   ├── ListInvoicesHandler.cs
│   ├── CreateCreditNoteHandler.cs
│   ├── CreateSupportDocumentHandler.cs
│   ├── GetNumberingRangesHandler.cs
│   └── GetCompanyHandler.cs

Infrastructure (ADAPTERS)
├── Invoicing/
│   ├── FactusAdapter.cs          (IInvoiceProvider → Factus API v2)
│   ├── FactusSettings.cs
│   ├── FactusTokenCache.cs
│   ├── LocalInvoiceProvider.cs   (IInvoiceProvider → modo sin Factus)
│   ├── EfInvoiceStore.cs
│   ├── EfNumberingRangeStore.cs
│   └── InMemoryInvoiceStore.cs

Api (ENDPOINTS)
├── Endpoints/
│   ├── InvoiceEndpoints.cs
│   ├── CreditNoteEndpoints.cs
│   ├── SupportDocumentEndpoints.cs
│   ├── NumberingRangeEndpoints.cs
│   └── CompanyEndpoints.cs
```

## Decisiones técnicas

### D1: Factus como plugin opcional
- **Decisión:** `IInvoiceProvider` con dos implementaciones: `FactusAdapter` y `LocalInvoiceProvider`
- **Razón:** El sistema debe funcionar sin Factus para que el usuario pueda facturar por fuera
- **Feature flag:** `Factus:Enabled` en appsettings

### D2: Token cache con refresh
- **Decisión:** `FactusTokenCache` con `IMemoryCache`, refresh automático 5 min antes de expirar
- **Razón:** Factus token expira en 1 hora, no podemos pedir token nuevo por cada request

### D3: Tabla única para todos los tipos de documento
- **Decisión:** Una sola tabla `invoices` con columna `document_type` (Bill, CreditNote, SupportDocument)
- **Razón:** Simplifica consultas, todos los documentos comparten estructura similar

### D4: Numbering ranges persistidos
- **Decisión:** Tabla `numbering_ranges` sincronizada con Factus
- **Razón:** Necesitamos saber qué rangos están disponibles para asignar a facturas

### D5: LocalInvoiceProvider para modo sin Factus
- **Decisión:** Genera reference_code único, guarda en DB en estado Draft
- **Razón:** Permite al usuario crear facturas localmente y exportar datos para facturar por fuera

## Endpoints de la API

### POST /api/invoices
Crea una factura electrónica.

### GET /api/invoices
Lista facturas del usuario autenticado.

### GET /api/invoices/{id}
Retorna factura específica.

### GET /api/invoices/{id}/pdf
Descarga PDF de la factura.

### GET /api/invoices/{id}/xml
Descarga XML DIAN de la factura.

### POST /api/credit-notes
Crea una nota crédito.

### POST /api/support-documents
Crea un documento soporte.

### GET /api/numbering-ranges
Lista rangos de numeración.

### GET /api/company
Retorna datos de la empresa.

### PUT /api/company
Actualiza datos de la empresa.
