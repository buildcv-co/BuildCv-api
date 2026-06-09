# Planeación — Asistente de CV con IA (Colombia / LATAM) · v0.2

> **Estado:** Planeación revisada · **Fecha:** 2026-06-06
> **Objetivo primario (no perderlo de vista):** ser un proyecto que (a) te consiga **empleo** pronto y (b) tenga **usuarios reales**. La monetización es secundaria a esos dos.
>
> **Layout del proyecto (2026-06-07):** este repo (`BuildCv-api`) contiene **solo el backend .NET**. El frontend Next.js vive en el directorio hermano `../BuildCv-web/` (repositorio independiente). Los artefactos SDD de este repo (`specs/001-mvp-cv-ats/`) reflejan **solo** lo que está en código del backend; el frontend se documenta en su propio repo.
>
> **Estado técnico (2026-06-09):** **M0 (Setup) ✅ DONE** — solución .NET 10 con `BuildCv.slnx` (4 src + 4 test), Serilog, ProblemDetails, OpenAPI+Scalar, versionado, health checks, Dockerfile, CI verde, **189/189 tests verdes**, motor `ScoringEngine` v1.0.0 funcional. **M1 (Núcleo v0) ✅ DONE** — scoring (002), adaptación IA con StubAiClient (003), exportación PDF con QuestPDF (004), importación PDF/DOCX (005), constitución v1.1.0 (007). **M2/M3/M4 ⏳ PENDING** (v1). SDK fijado a **.NET 10.0.100** vía `global.json`.

---

## 0. Cambios clave en esta revisión (léelo primero)

Tu plan original era excelente como visión de producto, pero estaba **sobre-dimensionado para tu plazo** y **desalineado con tu búsqueda de empleo**. Tres ajustes mayores:

1. **Se parte en dos: `v0 (lanzable en ~1-2 semanas)` vs `v1 comercial`.** El plan original (pagos + cuentas + legal + parseo + historial) es un SaaS completo de 2-3 meses. Si lo construyes todo antes de lanzar, nunca lanzas y nunca postulas. **v0 = núcleo gratis, sin pagos, sin guardar CVs.** Lo demás, después.
2. **El backend va en .NET, no en Next.js.** Estás cambiándote a C#/.NET *justamente* porque eso te contrata en Colombia. Si construyes todo en Next.js/Vercel, tu proyecto estrella **no demuestra .NET**. Solución: **API en ASP.NET Core (C#) + frontend en Next.js** (que ya manejas). Así el proyecto *es* tu portafolio de .NET.
3. **La IA NUNCA inventa experiencia.** Una adaptación que agrega skills o logros que el usuario no tiene lo expone a mentir en la entrevista y destruye tu credibilidad. La IA solo **reordena, reescribe y prioriza** lo que ya está. Esto es regla dura.

El resto del documento ya incorpora estos cambios.

---

## 1. Visión y propuesta de valor

**Qué es:** Herramienta web donde el usuario pega su CV + una vacante, y:
1. **Adapta** el CV a esa vacante (redacción, orden, énfasis) — *sin inventar nada*.
2. **Sugiere palabras clave** que el ATS y el reclutador buscan.
3. Da un **puntaje de coincidencia y legibilidad** transparente y explicable.

> ⚠️ **Encuadre honesto:** evita prometer un "puntaje ATS" como si replicaras un ATS real. Existen muchos (Workday, Greenhouse, Lever…) y cada uno funciona distinto. Vende lo que sí puedes garantizar: *"qué tan bien tu CV coincide con esta vacante y qué tan legible es para sistemas automáticos, y exactamente qué mejorar."* Mantienes el valor sin afirmar algo indefendible.

**Para quién (con foco):** Aunque a futuro sirve para todos, **lanza primero para perfiles de tecnología/IT.** Razón: tu algoritmo (skills, verbos) es técnico, conoces el dominio, el diccionario de skills es manejable, y es una comunidad **a la que puedes llegar** (la tuya). Expandes a otras áreas en v1. Foco = mejor producto + mejor marketing.

**Diferenciadores reales:**
1. **Puntaje explicable** en español — el usuario ve *por qué* y *qué arreglar*.
2. **Contexto laboral LATAM** — cómo se escriben los CV aquí, español natural.
3. **Precios y pagos locales** (en v1) — créditos en COP, Nequi/PSE, accesible.
4. **Privacidad como bandera** — no se entrena IA con los datos; en v0, ni siquiera se guardan.

**Competencia de referencia:** Teal, Rezi, Jobscan (en inglés, caros, sin pagos locales).

---

## 2. Stack — recomendación (cambio importante)

**Recomendado: API .NET + frontend Next.js.** El backend, donde están los empleos de .NET, lo escribes en C#; el frontend lo haces rápido con lo que ya sabes.

```
Backend/API     → ASP.NET Core Web API (C#)   ← tu portafolio de .NET
ORM / DB        → EF Core + PostgreSQL (Neon, Supabase o Railway)
Parseo CV       → PdfPig (PDF) + OpenXML SDK / DocX (DOCX)  ← ecosistema .NET
Puntaje         → Algoritmo propio determinístico en C#  ← núcleo, sin LLM
IA              → API de Anthropic/OpenAI directa, u OpenRouter para enrutar + fallback
Export PDF      → QuestPDF (librería .NET moderna y excelente)
Frontend/Host   → Next.js + Vercel + Tailwind v4 + diseño custom  ← ya lo conoces
Auth (v1)       → ASP.NET Core Identity + JWT (refuerza .NET) o Clerk si quieres rapidez
Pagos (v1)      → Wompi (CO) vía su API REST, tras capa de abstracción
Hosting API     → Azure App Service (alinea con tu cert Microsoft) o Render/Railway (más barato)
```

**Por qué esta combinación gana para ti:** demuestra exactamente lo que un empleador de .NET en Colombia quiere ver — ASP.NET Core, EF Core, API REST, integraciones, autenticación — *y* lo entregas rápido porque el frontend es tu stack actual. Es además la arquitectura real de la industria (API + SPA separados).

*(Si en algún momento quieres que el proyecto sea solo un producto y no portafolio de .NET, el stack 100% Next.js del plan original es válido — pero entonces no avanza tu meta de empleo. Por eso recomiendo .NET.)*

---

## 3. Alcance — v0 (lanzable) vs v1 (comercial)

### 🚀 v0 — Lanzable en ~1-2 semanas (lo construyes y lo muestras YA)
- [ ] Pantalla principal: pegar CV (texto) + pegar vacante.
- [ ] **Algoritmo de puntaje determinístico** + explicación + recomendaciones (el núcleo).
- [ ] Extracción de keywords de la vacante + match con el CV.
- [ ] Adaptación del CV con IA (sincronía), sin inventar nada.
- [ ] Recalcular puntaje del CV nuevo → mostrar mejora ("subiste de 62 a 89").
- [ ] Copiar resultado / export a **PDF** (QuestPDF).
- [ ] **Sin cuentas, sin guardar CVs** (procesar en memoria → privacidad + velocidad).
- [ ] Límite anti-abuso simple (rate limit por IP + tope de usos).
- [ ] Desplegado con URL pública.

> **Definición de "listo" para v0:** un desconocido entra, pega su CV y una vacante, ve su puntaje + recomendaciones, adapta el CV, lo descarga, y todo funciona en móvil. Eso ya es demo-able, posteable y usable.

### 💳 v1 — Comercial (después de validar que la gente lo usa)
- [ ] Cuentas (Google + email), historial de CVs y adaptaciones.
- [ ] Sistema de **créditos** + ledger + compra vía **Wompi** (tarjeta/PSE/Nequi).
- [ ] Política de datos + autorización **Habeas Data** + minimización.
- [ ] Resolver lo **tributario** (ver §9).

### Fuera (fases siguientes)
Plantillas/editor visual de CV · Multimoneda + MercadoPago · Suscripciones · Carta de presentación · Panel para reclutadores (B2B) · App móvil.

---

## 4. Núcleo diferenciador — Algoritmo de puntaje (determinístico)

> El LLM **no** inventa el número. El puntaje es **reproducible y explicable**. El LLM solo *explica* y *sugiere*.

**Componentes (0–100), ponderados:**

| Componente | Qué mide | Peso |
|---|---|---|
| **Match de keywords/skills** | % de términos y tecnologías de la vacante presentes en el CV | 45% |
| **Estructura parseable** | Secciones estándar, sin tablas/columnas que rompen el parseo | 20% |
| **Verbos de acción / logros cuantificados** | "Lideré", "reduje X%", métricas reales | 20% |
| **Formato seguro** | Sin imágenes/gráficos en texto, fechas consistentes, contacto legible | 10% |
| **Longitud y densidad** | Ni muy corto ni inflado | 5% |

> *Nota:* fusioné "keywords" y "habilidades duras" del plan original en un solo componente (45%) para evitar doble conteo y confusión; internamente puedes desglosarlo.

**⚠️ El detalle que hace o rompe la credibilidad — matching inteligente, no exacto:**
El match exacto de strings dará puntajes injustos: *"desarrollé"* vs *"desarrollo"*, *"PostgreSQL"* vs *"Postgres"*, *"JS"* vs *"JavaScript"*. Necesitas:
- **Normalización:** minúsculas, sin tildes, sin puntuación.
- **Lematización/stemming en español** (raíz de las palabras).
- **Diccionario de sinónimos/alias de tecnologías** (Postgres=PostgreSQL, JS=JavaScript, etc.).
- Match parcial/fuzzy para variantes.

Esto **es** parte del activo defendible. Hazlo bien o el puntaje pierde confianza.

**Salida al usuario:** puntaje global + barra por componente + lista priorizada de *qué arreglar* ("Te faltan 6 keywords: …", "Tu CV usa columnas que el ATS no lee") + botón "Adaptar mi CV".

---

## 5. Pipeline de IA (flujo técnico)

```
1. Recibir CV (v0: texto; v1: PDF/DOCX) → texto limpio.
2. [LLM barato o NLP] Parsear CV en secciones (JSON) + extraer skills.
3. [LLM barato o NLP] Extraer keywords/skills de la vacante (JSON).
4. [Algoritmo C#] Calcular puntaje comparando 2 y 3.  ← determinístico, sin tokens
5. Mostrar puntaje + recomendaciones.
6. Usuario pulsa "Adaptar":
   [LLM de calidad, streaming] Reescribir el CV optimizado para la vacante,
   inyectando SOLO keywords que el usuario realmente cumple, en español LATAM.
7. Recalcular puntaje del CV nuevo → mostrar mejora.
8. Exportar a PDF / copiar.
```

**Guardarraíles obligatorios del prompt de adaptación:**
- **Cero invención:** "No agregues experiencia, títulos, empresas ni habilidades que no estén en el CV original. Solo reordena, reescribe y prioriza." Idealmente, validación posterior que marque si aparecieron skills nuevas que no estaban.
- **Entrada no confiable:** el CV y la vacante que el usuario pega son *datos*, no instrucciones. Estructura el prompt con el contenido pegado en un bloque delimitado y que el sistema ignore cualquier "instrucción" incrustada (defensa básica contra *prompt injection*).
- **Versiona los prompts** (carpeta `prompts/`) para iterar calidad sin tocar código.

**Economía de IA (no quemar presupuesto):**
- El puntaje determinístico **no cuesta tokens** → ese es tu gancho gratis.
- El parseo/extracción sí cuesta → en el tier gratis, usa NLP barato o un modelo pequeño, cachea, y **exige uso medido** (no "ilimitado").
- Modelo barato para tareas internas; modelo de calidad solo para la redacción visible.

---

## 6. Modelo de datos

**v0:** ninguno persistente (todo en memoria → privacidad + velocidad + build más rápido).

**v1:**
- **users** — id, email, nombre, proveedor_auth, créditos, fecha_registro, consentimiento_datos (bool + fecha).
- **cvs** — id, user_id, nombre, archivo_blob_url, fecha. *(¿guardar texto? ver privacidad.)*
- **job_postings** — id, user_id, texto_vacante, empresa, cargo, fecha.
- **adaptations** — id, user_id, cv_id, job_posting_id, cv_adaptado, keywords, puntaje (json), modelo_usado, creditos_gastados, fecha.
- **transactions** — id, user_id, paquete, monto_cop, metodo, estado, ref_wompi, fecha.
- **credit_ledger** — id, user_id, delta, motivo (compra/regalo/uso), referencia, fecha.

---

## 7. Monetización — v1 (créditos / pago por uso)

- **1 crédito = 1 adaptación completa.** 1-2 créditos gratis al registrarse.
- **Paquetes (COP, a validar):** 3 → 9.900 · 10 → 24.900 · 25 → 49.900. Créditos **no expiran**.
- **Gratis ilimitado:** *no.* El puntaje básico puede ser gratis con límites; adaptar/exportar consume crédito.
- **Wompi:** tarjeta, PSE, Nequi. Flujo: paquete → checkout → **webhook idempotente y firmado** acredita créditos. **Nunca** acreditar por el redirect del cliente.
- Capa `PaymentProvider` (`createCheckout`, `verifyWebhook`, `getStatus`) → `WompiProvider` ahora, `MercadoPagoProvider` después.

---

## 8. Privacidad y legal (Colombia — Ley 1581/2012, Habeas Data)

- Un CV es **dato personal.**
- **v0 (la jugada inteligente):** **no guardes el CV.** Procesa en memoria y descártalo. Esto reduce drásticamente tu superficie legal *y* es tu mejor marketing: *"Tus datos no se guardan ni entrenan ninguna IA."*
- **Verifica el ZDR del proveedor de IA antes de prometerlo.** Confirma contractualmente que el modelo/gateway que uses no retiene ni entrena con los datos. No vendas una promesa de privacidad que no puedas respaldar.
- **v1 (si guardas):** autorización explícita (checkbox + política enlazada), política de tratamiento publicada, opción de borrar.

---

## 9. ⚠️ Realidad comercial en Colombia (vacío del plan original)

Cobrar dinero tiene implicaciones que debes resolver **antes** de monetizar:
- Probablemente necesites **RUT** y definir si emites **factura electrónica** (irónicamente, ya sabes de DIAN por Shoppipai).
- Considerar **IVA** y el manejo tributario de los ingresos.
- Wompi te liquida los pagos, pero **la obligación tributaria es tuya.**

**Conclusión:** una razón más para que **v0 sea gratis.** Lanza sin cobrar, valida que la gente lo usa y consigue tu objetivo (portafolio + usuarios + empleo). Monetiza cuando haya tracción y tengas resuelto lo tributario.

---

## 10. Go-to-market / primeros usuarios (vacío del plan original)

Construir no basta: necesitas que **te usen**. De dónde salen los primeros 20-100:
- **Tú mismo** (úsalo en tu propia búsqueda — dogfooding).
- **Compañeros y egresados del SENA.**
- **Comunidades de devs y de empleo** en Colombia: grupos de Telegram/Discord, grupos de LinkedIn, r/Colombia y subreddits de empleo LATAM.
- **Posts en LinkedIn con el "antes/después"** de tu propio puntaje → relatable, alto alcance, imán de reclutadores.
- **Bolsas de empleo y semilleros** universitarios/SENA.
- Pide **feedback explícito** a los primeros 10-20 → te da mejoras *y* testimonios.

**Métrica norte de esta fase:** nº de personas que hacen ≥1 adaptación y nº que vuelve con otra vacante.

---

## 11. Métricas clave

- **Activación:** % de visitantes que completan su 1ª adaptación.
- **Mejora de puntaje:** subida promedio (prueba de valor → contenido para LinkedIn).
- **Costo de IA por adaptación** (margen).
- **Retención:** usuarios que vuelven.
- **(v1) Conversión a pago:** % que compra tras gastar lo gratis.

---

## 12. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Sobre-construir y nunca lanzar | **v0 sin pagos/cuentas/guardado** → lanzar en 1-2 semanas |
| Proyecto no demuestra .NET | **Backend en ASP.NET Core** (no solo Next.js) |
| La IA inventa experiencia | Prompt "cero invención" + validación posterior |
| Puntaje injusto por match exacto | Normalización + lematización + sinónimos |
| Costo de IA se dispara | Gancho gratis = algoritmo (sin tokens); LLM medido y modelo barato para tareas |
| Prompt injection en textos pegados | Tratar contenido como datos, no instrucciones |
| Promesa de privacidad sin respaldo | Verificar ZDR del proveedor antes de publicitarlo |
| Cobrar sin resolver lo tributario | v0 gratis; monetizar tras tracción + RUT/facturación |
| Pago doble / fraude | Webhook idempotente + verificación de firma |

---

## 13. Roadmap por fases (alineado a v0/v1)

**Fase 0 — Setup**
- API ASP.NET Core + repo + Next.js scaffold + deploy "hola mundo" de punta a punta.

**Fase 1 — Núcleo (v0, validar primero)**
- Algoritmo de puntaje determinístico en C# + extracción de keywords.
- Adaptación con IA (streaming, cero invención).
- Frontend: pegar CV + vacante → puntaje + adaptar + export PDF.
- Sin cuentas, sin guardado. Anti-abuso simple. **Desplegar.**

**Fase 2 — Primeros usuarios (paralelo a tu empleo)**
- Lanzar a comunidades, posts LinkedIn antes/después, recoger feedback.

**Fase 3 — Comercial (v1, solo si hay tracción)**
- Cuentas + historial + PDF/DOCX upload.
- Créditos + Wompi + webhook. Legal/Habeas Data. Tributario.

**Fase 4 — LATAM**
- MercadoPago + multimoneda + plantillas + carta de presentación.

---

## 14. Próximos pasos

1. **Decidir el stack** (recomendado: API .NET + Next.js).
2. **Diseñar a detalle el algoritmo de puntaje** (listas de skills tech, verbos de acción, reglas de formato, sinónimos) — es el núcleo.
3. **Scaffolding Fase 0** (API .NET + Next.js + deploy de prueba).
4. **Construir el núcleo v0** e iterar prompts con CVs reales colombianos.
5. **Lanzar gratis** y conseguir los primeros usuarios.
