# 011-factus — Research

## Factus API v2

### Autenticación
- **Tipo:** OAuth2 password grant
- **Endpoint:** `POST /oauth/token`
- **Campos:** `grant_type=password`, `client_id`, `client_secret`, `username`, `password`
- **Response:** `access_token`, `refresh_token`, `expires_in` (3600s)
- **Refresh:** `grant_type=refresh_token` con `refresh_token`

### Endpoints principales

#### Facturas
| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/v2/bills/validate` | POST | Crear y validar factura |
| `/v2/bills/:number` | GET | Ver factura por número |
| `/v2/bills` | GET | Listar facturas con filtros |
| `/v2/bills/destroy/reference/:ref` | DELETE | Eliminar factura no validada |
| `/v2/bills/:number/download-pdf` | GET | Descargar PDF |
| `/v2/bills/:number/download-xml` | GET | Descargar XML DIAN |
| `/v2/bills/:number/download-attached-document-xml` | GET | Descargar AttachedDocument XML |
| `/v2/bills/:number/radian/events` | GET | Eventos de factura |
| `/v2/bills/:number/radian/events/:type` | POST | Emitir evento RADIAN |
| `/v2/bills/:number/email-content` | GET | Contenido de email |
| `/v2/bills/:number/send-email` | POST | Enviar email |

#### Notas Crédito
| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/v2/credit-notes/validate` | POST | Crear y validar nota crédito |
| `/v2/credit-notes/:number` | GET | Ver nota crédito |
| `/v2/credit-notes` | GET | Listar notas crédito |
| `/v2/credit-notes/destroy/reference/:ref` | DELETE | Eliminar nota no validada |
| `/v2/credit-notes/:number/download-pdf` | GET | Descargar PDF |
| `/v2/credit-notes/:number/download-xml` | GET | Descargar XML |
| `/v2/credit-notes/:number/download-attached-document-xml` | GET | Descargar AttachedDocument XML |
| `/v2/credit-notes/:number/email-content` | GET | Contenido de email |
| `/v2/credit-notes/:number/send-email` | POST | Enviar email |

#### Documentos Soporte
| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/v2/support-documents/validate` | POST | Crear y validar documento soporte |
| `/v2/support-documents/:number` | GET | Ver documento soporte |
| `/v2/support-documents` | GET | Listar documentos soporte |
| `/v2/support-documents/destroy/reference/:ref` | DELETE | Eliminar documento no validado |
| `/v2/support-documents/:number/download-pdf` | GET | Descargar PDF |
| `/v2/support-documents/:number/download-xml` | GET | Descargar XML |

#### Notas de Ajuste a Documentos Soporte
| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/v2/support-document-adjustments/validate` | POST | Crear y validar nota de ajuste |
| `/v2/support-document-adjustments/:number` | GET | Ver nota de ajuste |
| `/v2/support-document-adjustments` | GET | Listar notas de ajuste |
| `/v2/support-document-adjustments/:number/download-pdf` | GET | Descargar PDF |
| `/v2/support-document-adjustments/:number/download-xml` | GET | Descargar XML |

#### Rangos de Numeración
| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/v2/numbering-ranges` | GET | Listar rangos |
| `/v2/numbering-ranges` | POST | Crear rango |
| `/v2/numbering-ranges/:id` | GET | Ver rango |
| `/v2/numbering-ranges/:id/actualizar-consecutivo` | PATCH | Actualizar consecutivo |
| `/v2/numbering-ranges/:id/cambiar-estado` | PATCH | Cambiar estado |
| `/v2/numbering-ranges/programacion` | GET | Rangos programados |

#### Empresa
| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/v2/companies` | GET | Ver empresa |
| `/v2/companies` | PUT | Actualizar empresa |
| `/v2/companies/logo` | POST | Actualizar logo |

#### Suscripciones
| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/v2/subscriptions` | GET | Listar suscripciones |

#### Recepción de Documentos
| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/v2/receptions/bills` | GET | Ver facturas recibidas |
| `/v2/receptions/bills/:id/radian/events/:type` | POST | Emitir evento de recepción |

### Tipos de documento
- `01` = Factura de venta
- `03` = Documento soporte

### Tipos de operación
- `10` = Estándar
- `11` = Mandatos
- `12` = Transporte
- `SS-CUFE` = Salud
- `SS-Recaudo` = Salud
- `SS-Reporte` = Salud
- `SS-SinAporte` = Salud

### Códigos de impuestos
- `01` = IVA (19%)
- `02` = IVA (5%)
- `03` = IVA (0%)
- `04` = Impuesto consumo (8%)
- `05` = Impuesto consumo (4%)

### Estados de factura
- `1` = Validado
- `0` = No validado

### Formas de pago
- `1` = Contado
- `2` = Crédito

### Métodos de pago
- `10` = Efectivo
- `42` = Tarjeta débito
- `43` = Tarjeta crédito
- `30` = Transferencia

### Sandbox
- **URL:** `https://api-sandbox.factus.com.co`
- **Producción:** `https://api.factus.com.co`

## Wompi API (futuro 012-wompi)

### Autenticación
- Public key: `pub_test_*` (sandbox) / `pub_prod_*` (producción)
- Private key: `prv_test_*` / `prv_prod_*`
- Integrity secret: `prod_integrity_*`
- Events secret: `prod_events_*`

### Checkout
- Web Checkout: redirect a `https://checkout.wompi.co/p/`
- Widget: JavaScript en tu sitio
- Test cards: `4242` (APPROVED), `4111` (DECLINED)

### Webhook
- Evento: `transaction.updated`
- Verificación: SHA256 checksum
- Estados: APPROVED, DECLINED, VOIDED, ERROR
