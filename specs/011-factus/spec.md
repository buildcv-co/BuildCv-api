# 011-factus — Facturación electrónica DIAN (Factus API v2)

## Resumen

Integración opcional con la API de Factus para generar facturas electrónicas válidas ante la DIAN. El sistema funciona sin Factus (modo local); cuando se habilita, valida y genera documentos electrónicos conformes.

## User Stories

### US1: Crear factura electrónica
**Como** usuario del sistema,
**Quiero** que se genere una factura electrónica cuando realizo un pago,
**Para** tener comprobante fiscal válido ante la DIAN.

**Escenarios:**
- S1.1: Crea factura estándar (operation_type=10) con datos del cliente e items
- S1.2: Factus valida con DIAN → retorna CUFE y número de factura
- S1.3: Si Factus no está habilitado, crea factura en estado Draft (local)
- S1.4: Reference code es único (idempotencia)

### US2: Consultar factura
**Como** usuario,
**Quiero** ver mis facturas y descargar PDF/XML,
**Para** tener mis comprobantes fiscales.

**Escenarios:**
- S2.1: GET /invoices retorna lista de facturas del usuario
- S2.2: GET /invoices/{id} retorna factura específica con detalle
- S2.3: GET /invoices/{id}/pdf descarga PDF de la factura
- S2.4: GET /invoices/{id}/xml descarga XML DIAN de la factura

### US3: Nota crédito
**Como** usuario,
**Quiero** que se genere una nota crédito cuando hay un reembolso,
**Para** tener el documento fiscal del ajuste.

**Escenarios:**
- S3.1: Crea nota crédito referenciando factura original
- S3.2: Factus valida con DIAN → retorna CUFE de la nota
- S3.3: Si Factus no está habilitado, crea en estado Draft

### US4: Documento soporte
**Como** usuario del sistema,
**Quiero** generar documentos soporte para compras,
**Para** sustentar gastos ante la DIAN.

**Escenarios:**
- S4.1: Crea documento soporte con datos del proveedor
- S4.2: Factus valida con DIAN → retorna CUFE
- S4.3: Si Factus no está habilitado, crea en estado Draft

### US5: Rangos de numeración
**Como** administrador,
**Quiero** gestionar los rangos de numeración,
**Para** controlar la secuencia de facturación.

**Escenarios:**
- S5.1: Lista rangos de numeración disponibles
- S5.2: Crea nuevo rango de numeración
- S5.3: Actualiza consecutivo de un rango
- S5.4: Cambia estado de un rango (activo/inactivo)

### US6: Información de empresa
**Como** administrador,
**Quiero** ver y actualizar los datos de la empresa,
**Para** que aparezcan correctamente en las facturas.

**Escenarios:**
- S6.1: GET /company retorna datos de la empresa desde Factus
- S6.2: PUT /company actualiza datos en Factus
- S6.3: POST /company/logo actualiza logo de la empresa

## Non-Functional Requirements

| ID | Requisito | Valor |
|----|-----------|-------|
| NFR-001 | Modo sin Factus funcional | Sistema funciona con `Factus:Enabled=false` |
| NFR-002 | Token cache | Token OAuth2 cacheado, refresh antes de expirar |
| NFR-003 | Idempotency | Reference code único por invoice |
| NFR-004 | Zero suppressions | Sin `#pragma warning disable`, sin `[Skip]` |
| NFR-005 | Clean Architecture | Domain 0 packages, puertos en Application, adapters en Infrastructure |
| NFR-006 | Tests | ≥90% cobertura en handlers, integration tests con Factus mock |
| NFR-007 | Secretos | Solo via `IOptions<FactusSettings>` binder |

## Constitution Compliance

| Art. | Requisito | Cómo se cumple |
|------|-----------|----------------|
| **III** | Privacidad en logs | Logs: invoiceId, referenceCode, status. Nunca customer data |
| **VI** | Clean Architecture | IInvoiceProvider port en Application, FactusAdapter en Infrastructure |
| **IX** | Facturación DIAN | Plugin opcional, no bloquea uso del sistema |

## Out of Scope

- Suscripciones recurrentes de Factus
- Recepción de documentos de terceros (se agrega después)
- Notas de ajuste a documentos soporte (se agrega después)
- Integración con Wompi (012-wompi)
