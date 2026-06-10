# 011-factus — Tasks

## Phase 1: Domain + Ports (5 tasks)

- [ ] 1.1 Crear `InvoiceStatus.cs` (enum: Draft, Validated, Error)
- [ ] 1.2 Crear `InvoiceType.cs` (enum: Bill, CreditNote, SupportDocument)
- [ ] 1.3 Crear `Invoice.cs` (entity con todas las propiedades)
- [ ] 1.4 Crear `NumberingRange.cs` (entity)
- [ ] 1.5 Crear `CompanyInfo.cs` (value object)

## Phase 2: Application Ports (4 tasks)

- [ ] 2.1 Crear `IInvoiceProvider.cs` (port con todos los métodos)
- [ ] 2.2 Crear `IInvoiceStore.cs` (port)
- [ ] 2.3 Crear `INumberingRangeStore.cs` (port)
- [ ] 2.4 Crear handlers: CreateInvoice, GetInvoice, ListInvoices, CreateCreditNote, CreateSupportDocument, GetNumberingRanges, GetCompany

## Phase 3: Infrastructure — Factus Adapter (6 tasks)

- [ ] 3.1 Crear `FactusSettings.cs` (config model)
- [ ] 3.2 Crear `FactusTokenCache.cs` (token cache con refresh)
- [ ] 3.3 Crear `FactusAdapter.cs` — autenticación OAuth2
- [ ] 3.4 Crear `FactusAdapter.cs` — crear y validar factura
- [ ] 3.5 Crear `FactusAdapter.cs` — consultar facturas, descargar PDF/XML
- [ ] 3.6 Crear `FactusAdapter.cs` — notas crédito, documentos soporte, rangos, empresa

## Phase 4: Infrastructure — Stores (3 tasks)

- [ ] 4.1 Crear `EfInvoiceStore.cs` (PostgreSQL)
- [ ] 4.2 Crear `EfNumberingRangeStore.cs` (PostgreSQL)
- [ ] 4.3 Crear `InMemoryInvoiceStore.cs` (testing)

## Phase 5: Infrastructure — Local Provider (2 tasks)

- [ ] 5.1 Crear `LocalInvoiceProvider.cs` (modo sin Factus)
- [ ] 5.2 Actualizar `DependencyInjection.cs` con feature flag

## Phase 6: API Endpoints (5 tasks)

- [ ] 6.1 Crear `InvoiceEndpoints.cs` (POST /invoices, GET /invoices, GET /invoices/{id})
- [ ] 6.2 Crear descarga de PDF/XML (GET /invoices/{id}/pdf, GET /invoices/{id}/xml)
- [ ] 6.3 Crear `CreditNoteEndpoints.cs`
- [ ] 6.4 Crear `SupportDocumentEndpoints.cs`
- [ ] 6.5 Crear `NumberingRangeEndpoints.cs` y `CompanyEndpoints.cs`

## Phase 7: Tests (6 tasks)

- [ ] 7.1 Tests unitarios para `FactusAdapter` (mock HTTP)
- [ ] 7.2 Tests unitarios para handlers
- [ ] 7.3 Tests para `LocalInvoiceProvider`
- [ ] 7.4 Integration tests: crear factura con Factus mock
- [ ] 7.5 Integration tests: crear factura sin Factus (modo local)
- [ ] 7.6 Tests de idempotency (reference code único)

## Phase 8: Build + Verify (3 tasks)

- [ ] 8.1 dotnet build (0 warnings)
- [ ] 8.2 dotnet test (todos pasando)
- [ ] 8.3 dotnet format --verify-no-changes

**Total: 34 tasks**
