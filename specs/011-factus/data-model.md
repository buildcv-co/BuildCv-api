# 011-factus — Modelo de datos

## Tabla `invoices`

| Atributo | Columna | Tipo PG | Restricción / nota |
|----------|---------|---------|-------------------|
| identidad | `id` | `uuid` PK | UUIDv7 |
| dueño | `user_id` | `uuid` FK → `users.id` | ON DELETE RESTRICT |
| tipo de documento | `document_type` | `text` | HasConversion; CHECK IN ('Bill','CreditNote','SupportDocument') |
| código de referencia | `reference_code` | `varchar(80)` | ÚNICO |
| número de factura | `number` | `varchar(50)` | Nullable (hasta validación) |
| CUFE | `cufe` | `varchar(100)` | Nullable (hasta validación) |
| monto en centavos | `amount_in_cents` | `bigint` | CHECK > 0 |
| moneda | `currency` | `char(3)` | DEFAULT 'COP' |
| estado | `status` | `text` | HasConversion; CHECK IN ('Draft','Validated','Error') |
| cliente nombre | `customer_name` | `varchar(200)` | Para búsqueda |
| cliente identificación | `customer_identification` | `varchar(20)` | Para búsqueda |
| datos del cliente | `customer_data` | `jsonb` | Snapshot completo del cliente |
| items | `items_data` | `jsonb` | Snapshot de items |
| datos de pago | `payment_details_data` | `jsonb` | Snapshot de medios de pago |
| respuesta del proveedor | `provider_raw` | `jsonb` | Respuesta completa de Factus |
| ID de Factus | `provider_id` | `varchar(100)` | ID devuelto por Factus |
| creado | `created_at` | `timestamptz` | DEFAULT now() |
| actualizado | `updated_at` | `timestamptz` | |

**Claves/relaciones:** PK `id`; FK `user_id`.

**Índices:**
- `UX_invoices_reference_code` (ÚNICO sobre `reference_code`)
- `IX_invoices_user_id_created_at` (`user_id`, `created_at DESC`)
- `IX_invoices_status` (`status`)

**Notas EF Core:**
- `customer_data`, `items_data`, `payment_details_data` como jsonb con conversor System.Text.Json
- `document_type` y `status` con `HasConversion<string>()`

## Tabla `numbering_ranges`

| Atributo | Columna | Tipo PG | Restricción / nota |
|----------|---------|---------|-------------------|
| identidad | `id` | `uuid` PK | UUIDv7 |
| ID de Factus | `provider_id` | `integer` | ID numérico en Factus |
| prefijo | `prefix` | `varchar(20)` | Ej: "SETP" |
| desde | `from` | `integer` | |
| hasta | `to` | `integer` | |
| consecutivo actual | `current` | `integer` | |
| estado | `status` | `text` | HasConversion; CHECK IN ('Active','Inactive') |
| creado | `created_at` | `timestamptz` | DEFAULT now() |

**Índices:**
- `UX_numbering_ranges_provider_id` (ÚNICO sobre `provider_id`)

## Tabla `invoice_events`

| Atributo | Columna | Tipo PG | Restricción / nota |
|----------|---------|---------|-------------------|
| identidad | `id` | `uuid` PK | UUIDv7 |
| factura | `invoice_id` | `uuid` FK → `invoices.id` | ON DELETE CASCADE |
| tipo de evento | `event_type` | `varchar(10)` | |
| descripción | `description` | `text` | |
| datos | `data` | `jsonb` | |
| creado | `created_at` | `timestamptz` | DEFAULT now() |

**Índices:**
- `IX_invoice_events_invoice_id` (`invoice_id`)

## Diagrama ER

```
users (1) ──< (N) invoices
invoices (1) ──< (N) invoice_events
```
