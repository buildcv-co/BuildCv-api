# Contracts: 007-constitution-v1.1.0 (diff formal del texto constitucional)

> **Tipo:** Contract de governance. No es un contrato HTTP, es el **diff del archivo** `constitution.md` con cada línea marcada como `+` (añadida) o `-` (eliminada).

## 1. Header del documento

```diff
@@ Header del documento
-# Constitución del Proyecto — BuildCv
+# Constitución del Proyecto — BuildCv
 
-> **Artefacto SDD:** `.specify/memory/constitution.md` — ley fundamental del proyecto al estilo Spec Kit.
-> **Versión:** 1.0.0 · **Fecha de ratificación:** 2026-06-06 · **Última enmienda:** 2026-06-06
-> **Estado:** Vigente (ratificada).
+> **Artefacto SDD:** `.specify/memory/constitution.md` — ley fundamental del proyecto al estilo Spec Kit.
+> **Versión:** 1.1.0 · **Fecha de ratificación:** 2026-06-06 · **Última enmienda:** 2026-06-09
+> **Estado:** Vigente (ratificada).
```

## 2. Artículo III — Sección de Reglas

```diff
@@ Artículo III — Privacidad primero y minimización de datos

 **Reglas.**
-- En v0, el sistema **MUST** procesar el CV y la vacante en memoria y **NO** persistirlos *(FR-040, NFR-001)*.
-- El sistema **MUST NOT** registrar en logs el contenido del CV o de la vacante; solo metadatos no sensibles (longitudes, conteos, modelo usado, identificador de traza) *(FR-041, NFR-002)*.
-- El sistema **MUST** minimizar los datos enviados al proveedor de IA al mínimo necesario para la tarea *(FR-043, NFR-003)*.
-- El borrador local del texto, si existe, **MUST** permanecer en el dispositivo del usuario, borrarse al cerrar la sesión del navegador y **NO** viajar al servidor salvo al ejecutar una operación solicitada *(FR-004)*.
+- En v0.5 (fase actual), el sistema **MUST** procesar el CV y la vacante en memoria del servidor. La persistencia local EXCLUSIVAMENTE en el dispositivo del usuario (localStorage, IndexedDB) está permitida para el borrador de edición, con borrado explícito al logout o a solicitud del usuario *(FR-040, FR-040a, NFR-001, NFR-001a)*. v1.0 introducirá cuentas de usuario y persistencia server-side con consentimiento expreso (Habeas Data, Art. IX).
+- El sistema **MUST NOT** registrar en logs el contenido del CV o de la vacante; solo metadatos no sensibles (longitudes, conteos, modelo usado, identificador de traza) *(FR-041, NFR-002)*.
+- El sistema **MUST** minimizar los datos enviados al proveedor de IA al mínimo necesario para la tarea *(FR-043, NFR-003)*.
+- El borrador local del texto, si existe, **MUST** permanecer en el dispositivo del usuario, borrarse al cerrar la sesión del navegador y **NO** viajar al servidor salvo al ejecutar una operación solicitada *(FR-004)*.
+- El sistema frontend **MUST** ofrecer al usuario un mecanismo explícito de "Limpiar borrador" que purge toda persistencia local (localStorage, IndexedDB, sessionStorage) relacionada con su CV *(FR-040b)*.
```

## 3. Artículo I — Sección de Reglas

```diff
@@ Artículo I — Cero invención de la IA

 **Reglas.**
 - El sistema **MUST** garantizar que la adaptación con IA no agregue experiencia, empleos, empresas, cargos, tecnologías, certificaciones, estudios, fechas, métricas ni logros que no estén en el CV original *(FR-024)*.
 - El sistema **MUST** ejecutar una validación posterior determinista que compare las entidades del CV adaptado contra las del original y marque todo elemento nuevo no respaldado como posible invención, aplicando una política de acción según severidad (descartar / advertir / regenerar) *(FR-025)*.
 - El sistema **MUST** comunicar al usuario el resultado de la verificación de honestidad: "sin invención" o "advertencia" con los términos potencialmente nuevos a revisar *(FR-029)*.
 - El sistema **MUST NOT** ofrecer fabricar habilidades ausentes; las brechas reales se etiquetan "aprende/añade si la cumples" y nunca como algo que el producto pueda inventar por el usuario *(FR-022)*.
 - El conteo de invenciones **MUST** ser determinista (un cruce de entidades, no una opinión del LLM); el modelo puede actuar como juez de borde, pero el veredicto cuantitativo es código.
+- El editor frontend **MUST NOT** agregar entidades nuevas (skills, certificaciones, experiencia, empresas, cargos, fechas, métricas) que el usuario no haya escrito explícitamente. El schema validado con Zod rechaza entidades nuevas en el round-trip Markdown *(FR-029a, defense in depth del lado cliente)*.
```

## 4. Artículo VI — Sección de Arquitectura (puertos)

```diff
@@ Artículo VI — El backend demuestra .NET profesional (es portafolio)

 **Reglas.**
 - El backend **MUST** estar construido en ASP.NET Core (C#, .NET) con una arquitectura limpia y defendible (separación de capas, inversión de dependencias, SOLID), de modo que el núcleo de dominio no dependa de ASP.NET ni del SDK de IA.
-- El motor de puntaje **MUST** residir en el dominio como servicio puro, aislado de infraestructura, y los proveedores externos (IA, parseo, export, pagos) **MUST** estar tras puertos/abstracciones (`IAiClient`, `ICvParser`, `IPdfExporter`, `PaymentProvider`, …) para ser sustituibles sin tocar el núcleo *(materializa FR-030 y la portabilidad de hitos)*.
+- El motor de puntaje **MUST** residir en el dominio como servicio puro, aislado de infraestructura, y los proveedores externos (IA, parseo de archivos, export PDF, pagos) **MUST** estar tras puertos/abstracciones (`IAiClient`, `ICvParser` para PDF/DOCX, `IPdfExporter`, `IPaymentProvider`, `ICvStore` para localStorage en el frontend, …) para ser sustituibles sin tocar el núcleo *(materializa FR-030 y la portabilidad de hitos)*.
 - El código **MUST NOT** acoplar tipos de un SDK externo fuera de la capa de infraestructura.
 - El backend **MUST** aplicar el principio de "no sobre-ingeniería": un patrón se introduce solo cuando paga su costo; demostrar **cuándo NO** aplicarlo es parte de la señal de seniority.
 - El sistema **MUST** degradar con elegancia ante fallo del proveedor de IA: el análisis determinista (puntaje, keywords, recomendaciones) sigue disponible y la interfaz no se rompe *(FR-030, NFR-018, NFR-019, US-016)*.
 - Las prácticas de calidad (manejo de errores tipado/RFC 9457, logging estructurado sin contenido sensible, resiliencia, OpenAPI, CI con formato + tests) **SHOULD** estar presentes y ser discutibles en una entrevista técnica.
+
+ **Puertos definidos (a la fecha de v1.1.0):**
+- `IAiClient` — Application, para invocación del proveedor de IA
+- `ICvParser` — Application, para parseo de archivos (PDF, DOCX)
+- `IPdfExporter` — Application, para generación de PDFs
+- `IPaymentProvider` — Application, para Wompi (v1+)
+- `ICvStore` — Frontend, para persistencia local (localStorage, IndexedDB) del borrador
```

## 5. Artículo VII — Sección de Reglas (rate limit)

```diff
@@ Artículo VII — v0 lanzable sin fricción; entrega por hitos

 **Reglas.**
 - v0 **MUST** ser usable de principio a fin **sin crear cuenta ni iniciar sesión** y sin guardado *(FR-040, US-008)*.
 - v0 **MUST** entregar el flujo completo: pegar CV + vacante → puntaje explicable → keywords → recomendaciones → adaptación en streaming → delta de mejora → exportar/copiar *(FR-001, FR-005, FR-019, FR-021, FR-023, FR-027, FR-032, FR-033, FR-034)*.
 - El flujo **MUST** ser usable en móvil de extremo a extremo *(US-009, NFR-012..NFR-015)*.
 - v0 **SHOULD** ofrecer un CV y una vacante de ejemplo cargables con una sola acción para probar sin pegar datos propios *(FR-003, US-010)*.
-- El sistema **MUST** limitar el uso por origen con políticas diferenciadas por costo (más estricta para la adaptación con IA que para el análisis determinista) para proteger el presupuesto de IA sin fricción para usuarios legítimos *(FR-036, FR-038, US-011)*.
+- El sistema **MUST** limitar el uso por origen con políticas diferenciadas por costo (más estricta para la adaptación con IA que para el análisis determinista o el import de archivos) para proteger el presupuesto de IA y CPU sin fricción para usuarios legítimos *(FR-036, FR-038, FR-039a, US-011)*.
 - Las capacidades de v1 (cuentas, historial, créditos, pagos, consentimiento legal, carga de archivos) **MUST NOT** introducirse como prerrequisito de v0; se planean completas pero se ordenan después *(FR-044..FR-055, prioridad P1)*.
 - La separación de hitos **MUST** mantenerse en todos los artefactos: cada requisito y tarea declara su hito (P0 = v0, P1 = v1).
+
+ **Políticas de rate-limit (referencia operacional, detalle en `RateLimiting.cs`):**
+- `"score"` (deterministic): 60/h por IP
+- `"ai"` (adaptación con LLM): 5/h por IP
+- `"export"` (PDF generation, CPU-bound): 20/h por IP
+- `"import"` (PDF/DOCX parsing, CPU-bound): 30/h por IP (NUEVO en v1.1.0)
```

## 6. Artículo IX — Sección de Reglas (ZDR)

```diff
@@ Artículo IX — Cumplimiento Habeas Data al monetizar

 **Reglas.**
-- Antes de prometer públicamente "retención cero / no entrenamiento" del proveedor de IA, el sistema **MUST** verificarlo contractualmente; mientras no esté confirmado, el copy público **MUST** comunicar honestamente que el contenido se envía al proveedor y puede retenerse según su política. ZDR es un **gate bloqueante**, no una suposición *(FR-042, NFR-022)*.
+- Antes de prometer públicamente "retención cero / no entrenamiento" del proveedor de IA, el sistema **MUST** verificarlo contractualmente; mientras no esté confirmado, el copy público **MUST** comunicar honestamente que el contenido se envía al proveedor y puede retenerse según su política. ZDR es un **gate bloqueante**, no una suposición *(FR-042, NFR-022, NFR-022a)*.
 - En v1, antes de recolectar o guardar datos personales, el sistema **MUST** solicitar consentimiento informado, previo y expreso, informando la finalidad y la **transferencia internacional** del contenido al proveedor de IA *(FR-051, NFR-024)*.
 - El sistema **MUST** permitir al titular ejercer acceso, rectificación, supresión y revocación del consentimiento, dejando constancia, y detener el tratamiento al revocar *(FR-052)*.
 - El sistema **MUST** publicar una política de tratamiento de datos accesible y un aviso de privacidad conforme a la regulación vigente (Ley 1581 de 2012 y normas que la reglamenten/actualicen) *(FR-053, NFR-023)*.
 - Al monetizar (v1), el sistema **MUST** acreditar créditos únicamente tras una confirmación de pago firmada, verificada e idempotente del proveedor, sin confiar en el redireccionamiento del navegador, y **MUST** generar del lado del servidor la firma de integridad sin exponer secretos *(FR-046, FR-048, FR-049, NFR-007)*.
 - El cobro **SHOULD** soportar el flujo tributario aplicable (comprobante/factura conforme a la regulación) antes de facturar de forma recurrente *(FR-050)*.
 - Aun en v0 (sin guardado), la comunicación de privacidad **MUST** ser veraz y diseñada para minimizar la exposición legal por diseño *(NFR-025)*.
+
+ **Estado del gate ZDR (a fecha de v1.1.0, 2026-06-09):** Anthropic acepta ZDR solo en cuentas Enterprise. La cuenta de BuildCV es estándar → ZDR NO se puede garantizar → copy público dice "el contenido se envía al proveedor y puede retenerse según su política". Cuando Anthropic Enterprise se habilite, hacer PR con diff contractual + bumpear a v1.2.0.
```

## 7. CONSTITUTION-README.md — diff

```diff
@@ CONSTITUTION-README.md — Tabla comparativa Spec-Kit vs Constitution

| Spec-Kit genérica | BuildCv v1.1.0 |
|---|---|
| ... | ... |
+| v1.1.0 (2026-06-09) — Enmienda menor | Se permite persistencia local EXCLUSIVAMENTE (Art. III). Se añaden puertos `ICvParser` y `ICvStore`. Se añade política `"import"` (Art. VII). Se refuerza gate ZDR (Art. IX). Ver [specs/007-constitution-v1.1.0/spec.md](../specs/007-constitution-v1.1.0/spec.md). |
```

## Resumen del diff

- **Header**: 1 línea modificada (versión + fecha).
- **Art. III**: 4 reglas modificadas + 1 regla añadida.
- **Art. I**: 1 regla añadida (defense in depth).
- **Art. VI**: 1 regla modificada (lista de puertos actualizada) + 1 sub-sección añadida.
- **Art. VII**: 1 regla modificada + 1 sub-sección añadida.
- **Art. IX**: 1 regla modificada + 1 nota de estado añadida.
- **CONSTITUTION-README.md**: 1 fila añadida a la tabla.

**Total: ~30 líneas modificadas, ~15 líneas añadidas, 0 líneas eliminadas. Es un cambio MENOR.**
