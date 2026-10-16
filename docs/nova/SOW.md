# SOW — QUICKPROMPT NOVA
## Reconstrucción React, experiencia JARVIS y sistema inteligente de reutilización de prompts

**Versión:** 1.0

**Estado:** Especificación propuesta, pendiente de implementación y pruebas

**Proyecto actual:** `C:\Projects\Repos\QuickPrompt-Web`

**Nombre de producto provisional:** QuickPrompt NOVA

---

## 1. MISIÓN DEL PRODUCTO

Actúa como arquitecto principal de software, ingeniero de IA local, diseñador senior de producto e ingeniero de calidad.

Reconstruye QuickPrompt como una aplicación web React moderna y ligera, con un asistente inteligente de estilo JARVIS que ayude a localizar, crear, completar, mejorar y reutilizar prompts.

**El propósito central no es conversar con una IA. Es reutilizar instrucciones de forma inmediata.**

La aplicación debe servir para cualquier categoría de trabajo o actividad personal.

Ejemplos: escritura, programación, administración, educación, investigación, análisis, soporte, organización y productividad.

Los prompts pueden tener variables o ser textos completos sin variables.

No convertir QuickPrompt en un chatbot genérico, un dashboard administrativo, una plataforma de agentes autónomos ni un gestor de proyectos.

### Visión

El usuario abre QuickPrompt y puede:

- Encontrar un prompt guardado.
- Solicitar a NOVA que lo encuentre.
- Completar variables cuando existan.
- Copiar el contenido.
- Editar o guardar una nueva versión.
- Ejecutarlo con un modelo local.
- Utilizarlo en ChatGPT, Claude u otros proveedores mediante integraciones permitidas.
- Crear un prompt nuevo, manualmente o con ayuda de IA.

Todo con la menor cantidad razonable de acciones y sin pantallas innecesarias.

### Principios innegociables

1. Simplicidad por encima de cantidad de funcionalidades.
2. IA como asistencia, no como requisito para utilizar la biblioteca.
3. Diseño futurista pero legible y accesible.
4. Privacidad y almacenamiento local por defecto.
5. Arquitectura SOLID y contratos claros.
6. Persistencia segura.
7. Compatibilidad y preservación de datos anteriores.
8. Acciones sensibles mediante confirmación explícita.
9. Pruebas reales, no éxito simulado.
10. No instalar dependencias ni descargar modelos sin autorización.

---

## 2. ALCANCE DE LA RECONSTRUCCIÓN

Eliminar completamente Blazor como tecnología de presentación final.

Crear un frontend React + TypeScript nuevo.

El backend será ASP.NET Core Web API sin componentes Razor, renderizado Blazor ni circuitos SignalR de presentación.

Reutilizar únicamente los componentes de dominio, servicios y persistencia del sistema anterior que superen auditoría de diseño, compatibilidad y pruebas.

Crear la implementación sobre una rama de reconstrucción independiente.

No eliminar inicialmente los archivos antiguos ni destruir el estado previo.

Después de establecer paridad funcional, completar migraciones y superar aceptación, retirar los componentes Blazor y sus dependencias mediante un cambio separado y verificable.

### Preservar

- Prompts.
- Variables.
- Categorías.
- Grupos personalizados.
- Favoritos.
- Historial y versiones.
- Perfiles de instrucciones existentes.
- Importación/exportación.
- Respaldos.
- Datos SQLite.
- Integración Ollama.
- Adaptadores de proveedores.
- Pruebas y evidencias anteriores.

### No trasladar a la nueva experiencia principal

- Dashboards.
- Sidebars administrativos permanentes.
- Pantallas independientes para cada operación.
- Formularios extensos.
- Paneles triples.
- Configuraciones avanzadas visibles por defecto.
- Botones repetidos.
- Controles técnicos sin valor para el usuario.
- Un chat persistente que ocupe la interfaz.

---

## 3. IDENTIDAD VISUAL: LIVING CANVAS

### Concepto

Diseñar una interfaz propia denominada Living Canvas.

La experiencia debe recordar a un asistente futurista de ciencia ficción, sin copiar exactamente la identidad visual de JARVIS, Marvel u otros productos.

No utilizar un diseño típico compuesto por un sidebar y decenas de tarjetas rectangulares.

El lienzo principal ocupa el navegador y utiliza tipografía, movimiento, separaciones y elementos de navegación ligeros para organizar el contenido.

### Principios de composición

- Espacio abierto.
- Superficies de bajo contraste visual.
- Ausencia de bordes decorativos innecesarios.
- Jerarquía tipográfica fuerte.
- Profundidad visual sutil.
- Componentes flotantes solo cuando sean útiles.
- Animaciones ligadas a cambios reales de estado.
- Acciones esenciales siempre localizables.
- Responsive genuino, no una versión desktop comprimida.

### Tres estados de un mismo espacio

**A. DISCOVER**

Aparece al abrir QuickPrompt.

Contiene:

- Identidad QuickPrompt NOVA.
- Campo inteligente central.
- Prompts recientes/favoritos.
- Acceso discreto a biblioteca y colecciones.

No es una página de bienvenida vacía. Los prompts deben estar disponibles inmediatamente.

**B. FOCUS**

Aparece al seleccionar un prompt.

El lienzo central muestra:

- Nombre.
- Texto resumido.
- Variables, si existen.
- Vista previa expandible.
- Botón principal Copiar.
- Acción Usar en...
- Edición contextual.

El resto de la interfaz pierde protagonismo.

No navegar a otra pantalla para completar variables.

**C. CREATE**

Aparece al crear un prompt.

Formulario principal:

- Título.
- Contenido.

Detectar variables automáticamente.

Grupos, descripción, etiquetas y opciones avanzadas deben ser opcionales.

NOVA puede proponer contenido desde una intención escrita.

### Navegación

Estos tres estados comparten el mismo shell React.

Utilizar navegación y transiciones contextuales, no pantallas completamente desconectadas.

En móvil, emplear una composición vertical progresiva y un área inferior compacta para las acciones de uso.

---

## 4. COMPONENTE CENTRAL: NOVA CORE

NOVA es una presencia visual discreta y un asistente contextual.

No debe parecer un robot animado permanentemente.

### Estados visuales

- Idle: presencia sutil.
- Listening: el usuario escribe o activa una consulta.
- Thinking: procesa una petición local.
- Searching: busca prompts.
- Suggesting: ofrece opciones.
- Ready: resultado disponible.
- Error: fallo recuperable.
- Offline: modelo no disponible.

El elemento visual puede utilizar anillos SVG, ondas discretas, un punto luminoso y variaciones de intensidad.

### Reglas de movimiento

- Microinteracciones aproximadamente entre 120 y 250 ms.
- Transiciones de superficie aproximadamente entre 200 y 350 ms.
- Animaciones de espera que no simulen progreso inexistente.
- Nunca bloquear controles durante animaciones cosméticas.
- No animar listas largas elemento por elemento.
- Suspender animaciones intensivas cuando la ventana no está activa.
- Respetar `prefers-reduced-motion`.
- Optimizar para GPU integrada y dispositivos móviles.
- Sin uso obligatorio de WebGL, Canvas 3D o Three.js.

NOVA no necesita una escena 3D para transmitir presencia.

### Personalidad

Respuesta natural, breve y contextual.

Ejemplos:

"Encontré tres prompts que pueden servirte."

"Este prompt tiene dos variables. ¿Quieres completarlas?"

"No tengo uno parecido. Puedo ayudarte a crearlo."

"He preparado una mejora. Revisa los cambios antes de guardar."

No mostrar mensajes repetitivos, felicitaciones constantes ni animaciones para acciones triviales.

---

## 5. TECNOLOGÍAS FRONTEND

Implementar:

- React con TypeScript estricto.
- Vite.
- Tailwind CSS v4 con plugin oficial.
- Motion for React.
- Lucide React.
- Radix Primitives selectivamente para overlays accesibles.
- TanStack Query para estado remoto y caché de consultas, si las necesidades observadas justifican su incorporación.
- React Router solo para rutas que aporten valor.
- Zod para validar contratos del lado cliente.
- Vitest y React Testing Library.
- Playwright para E2E real.

No instalar automáticamente todas las bibliotecas enumeradas.

Auditar primero compatibilidad, licencias, versiones y necesidad. Solicitar autorización para paquetes faltantes.

No añadir Redux, Zustand, XState, Next.js, múltiples bibliotecas de componentes o dos sistemas de estado si no se justifican.

Utilizar React state, hooks personalizados y composición para el estado local simple.

No usar componentes de demostración como implementación final.

---

## 6. DESIGN SYSTEM

Construir un sistema visual propio, no aplicar indiscriminadamente un template.

### Tokens principales

- Background: azul noche profundo.
- Accent: cyan/teal luminoso.
- Foreground: blanco azulado.
- Muted: azul grisáceo.
- Success: verde discreto.
- Warning: ámbar.
- Error: rojo accesible.

Crear variantes light y dark.

Los efectos glow deben ser limitados y respetar contraste.

### Tipografía

Usar una fuente principal legible y una fuente de acento técnica solo en títulos breves o indicadores.

Evitar fuentes futuristas difíciles de leer en formularios.

### Componentes reutilizables

- NovaCore
- NovaInput
- PromptStream
- PromptItem
- PromptFocus
- VariableField
- VariableComposer
- PromptPreview
- ActionDock
- ProviderPicker
- SearchResults
- GroupSelector
- InlinePromptEditor
- NovaSuggestion
- StatusIndicator
- ConfirmDialog
- ToastRegion
- LoadingState
- EmptyState
- ErrorState

Cada componente debe tener una responsabilidad clara.

No convertir todo en un componente genérico configurado mediante decenas de props.

### Reglas de interacción

- Una acción principal por contexto.
- Estados hover/focus/pressed/disabled claros.
- Feedback después de copiar o guardar.
- Confirmación antes de eliminar.
- Escape cierra overlays cuando corresponde.
- Focus trapping en diálogos.
- Gestión correcta del scroll.
- No solapamientos.
- Iconos con nombres accesibles cuando sean acciones independientes.

---

## 7. DOMINIO: PROMPTS

Definir un modelo de dominio independiente de React.

### Prompt

Campos:

- Id.
- Title.
- Description opcional.
- TemplateText.
- CategoryId opcional.
- IsFavorite.
- CreatedAt.
- UpdatedAt.
- Revision.
- IsArchived.

### Variables

Permitir prompts sin variables.

Los placeholders del legado deben conservarse.

Ejemplo:

"Analiza el siguiente texto: <contenido>. Responde en <idioma>."

Los nombres deben detectarse mediante un parser determinista.

No reemplazar texto mediante expresiones ambiguas ni sustituciones parciales.

Soportar:

- Variables repetidas.
- Texto multilínea.
- Caracteres Unicode.
- Valores vacíos cuando sean válidos.
- Valores por defecto.
- Variables obligatorias/opcionales cuando estén definidas explícitamente.
- Caracteres de escape.
- Nombres duplicados.
- Valores largos.
- Copia exacta del resultado.

El formato actual del repositorio será la fuente de compatibilidad.

Solo introducir una sintaxis nueva si existe un plan de migración y pruebas de equivalencia.

### Compilación

Entrada:

PromptTemplate + VariableValues + Options.

Salida:

CompiledPrompt + ValidationErrors + TemplateRevision.

El resultado no debe modificar automáticamente la plantilla guardada.

---

## 8. OPERACIONES DE NEGOCIO

Implementar operaciones:

- CreatePrompt
- UpdatePrompt
- DuplicatePrompt
- ArchivePrompt
- RestorePrompt
- DeletePrompt
- SearchPrompts
- GetPrompt
- ListRecentPrompts
- ToggleFavorite
- CompilePrompt
- AssignPromptToGroups
- RemovePromptFromGroup
- SavePromptRevision
- ExportLibrary
- ImportLibrary
- CreateBackup
- RestoreBackup

Los casos de uso deben vivir fuera de los componentes React.

Ninguna llamada al LLM ejecuta directamente CRUD.

Versionar cambios sustanciales del contenido.

Utilizar control de concurrencia optimista para evitar sobrescrituras accidentales.

Las eliminaciones deben requerir confirmación. Valorar archivado como acción preferente.

Registrar auditoría local mínima de operaciones relevantes, sin almacenar innecesariamente contenido sensible en logs.

---

## 9. GRUPOS, CATEGORÍAS Y BIBLIOTECA

Mantener categorías como clasificación opcional.

Mantener grupos personalizados como colecciones.

Un prompt puede estar asociado a varios grupos.

La asignación debe ser muchos-a-muchos sin duplicar plantillas.

La experiencia de organización debe ser sencilla:

- Buscar.
- Filtrar.
- Marcar favorito.
- Asignar grupo.
- Crear grupo.
- Renombrar o eliminar grupo.

No mostrar permanentemente todas las opciones.

Una persona debe poder crear y utilizar prompts sin crear una sola categoría o grupo.

### Frecuencia de uso

Registrar lastUsedAt y un contador de ejecuciones/reutilizaciones cuando el usuario realice acciones de uso verificables.

No considerar una simple visualización como utilización.

Los prompts frecuentes y recientes pueden aparecer como accesos directos.

Permitir desactivar historial de uso si se prefiere.

---

## 10. BÚSQUEDA: SIMPLE ANTES QUE SEMÁNTICA

Primera estrategia:

- Búsqueda exacta.
- Prefijos.
- Títulos.
- Descripciones.
- Texto del prompt.
- Filtros de grupo.
- Favoritos.
- Recientes.

Implementar SQLite FTS5/BM25 si es compatible con la base existente y su licencia/compilación.

La búsqueda debe funcionar sin el modelo local.

No crear una base vectorial nueva por defecto.

### NOVA Search

Cuando el usuario introduce una intención natural, como:

"Necesito uno para revisar la arquitectura de un proyecto."

NOVA debe:

1. Interpretar la intención.
2. Buscar candidatos reales en la biblioteca.
3. Recuperar títulos y metadatos autorizados.
4. Recomendar hasta tres candidatos.
5. Explicar brevemente por qué podrían servir.
6. Permitir abrir el prompt directamente.

Nunca inventar un prompt guardado que no existe.

Distinguir resultados recuperados de propuestas generadas.

Si no encuentra coincidencias, ofrecer crear uno.

Embeddings opcionales en una fase posterior únicamente si una evaluación demuestra ventaja frente a búsqueda léxica.

---

## 11. NOVA AI: ASISTENTE LOCAL

Implementar un asistente contextual mediante Ollama.

Modelo principal inicial: `qwen3:8b`, si está instalado y cumple las evaluaciones.

Alternativa local configurable: `qwen2.5:7b-instruct`, con limitaciones de calidad documentadas.

No descargar modelos.

### Capacidades

NOVA puede:

- Encontrar un prompt existente.
- Explicar las variables de un prompt.
- Proponer valores de variables.
- Crear un borrador nuevo desde una intención.
- Mejorar un prompt existente.
- Sugerir un título.
- Sugerir grupo o categoría.
- Detectar prompts posiblemente duplicados.
- Preparar el prompt final.
- Proponer el proveedor de destino según la preferencia explícita del usuario.

No puede:

- Eliminar datos automáticamente.
- Modificar plantillas sin aceptación.
- Crear grupos silenciosamente.
- Enviar mensajes a proveedores externos sin acción autorizada.
- Ejecutar shell ni leer archivos arbitrarios.
- Inventar el contenido de la biblioteca.

### Agente finito, no autónomo ilimitado

Implementar un workflow controlado:

INTENT → SEARCH/READ → PROPOSE → USER_DECISION → DOMAIN_ACTION → VERIFY.

El agente solo accede a herramientas permitidas.

Herramientas iniciales:

- search_prompts
- get_prompt
- list_groups
- inspect_variables
- compile_preview
- propose_prompt
- propose_improvement

Las herramientas de lectura son seguras y acotadas.

Guardar, editar o borrar requiere confirmación explícita y ejecución mediante servicios de dominio.

### Límites

- Máximo tres pasos de herramientas por solicitud inicial.
- Límites de tokens configurables.
- Timeout y cancelación.
- Validación de argumentos con JSON Schema.
- Validación de salidas.
- Rechazo de herramientas desconocidas.
- Control de tamaño del contexto.
- Id de correlación.
- Abstención cuando no exista evidencia.

Ajustar los límites mediante mediciones en el hardware real.

No utilizar LangChain, LangGraph u otro framework de agentes sin demostrar que su complejidad resuelve un requisito concreto.

Un orquestador finito propio será el baseline.

---

## 12. CONTRATO DEL AGENTE

Definir contratos tipados versionados.

El clasificador de intención debe distinguir, como mínimo:

- SEARCH
- OPEN
- CREATE_DRAFT
- IMPROVE_DRAFT
- FILL_VARIABLES
- COMPILE
- RUN_LOCAL
- SELECT_PROVIDER
- HELP
- UNKNOWN

Cada acción tendrá:

- Id.
- Intent.
- Arguments.
- SourcePromptIds.
- Confidence/uncertainty cuando sea útil y verificable.
- RequiresConfirmation.
- ProposedEffect.
- ValidationStatus.

Nunca interpretar texto libre como autorización para una acción irreversible.

Ante ambigüedad, pedir una única aclaración concreta.

En caso de fallo del modelo, mantener disponible la búsqueda convencional.

---

## 13. INTEGRACIÓN CON PROVEEDORES DE IA

Separar claramente cuatro mecanismos:

1. LOCAL_EXECUTION: ejecutar mediante Ollama.
2. COPY_AND_OPEN: copiar texto y abrir el sitio.
3. BROWSER_BRIDGE: insertar en una página mediante extensión, cuando esté permitido y validado.
4. OFFICIAL_API: integración explícita con API oficial, si el usuario la configura y autoriza.

No asumir que tener una sesión web equivale a disponer de acceso API.

### Registry de proveedores

Cada proveedor debe implementar un contrato común de capacidades:

- ProviderId.
- DisplayName.
- DestinationUrl.
- ModesSupported.
- CanPrepare.
- CanSubmit.
- RequiresExtension.
- RequiresCredentials.
- AvailabilityStatus.
- LastVerifiedAt.

Destinos iniciales:

- Ollama local.
- ChatGPT.
- Claude.
- Gemini.
- Copilot.
- Perplexity.

Permitir otros proveedores mediante adaptadores documentados.

### UX

El usuario selecciona un prompt final y pulsa "Usar en...".

Mostrar proveedores mediante un selector compacto, por ejemplo una banda de iconos.

No presentar seis botones grandes simultáneamente.

### Claude

La automatización de Claude.ai queda deshabilitada mientras no exista autorización o un mecanismo oficialmente permitido.

Ofrecer copiar y abrir como experiencia funcional.

### ChatGPT y otros

Reevaluar términos, permisos y mecanismos disponibles para cada sitio.

No depender exclusivamente de selectores CSS frágiles.

No declarar una integración operativa sin validación en el servicio auténtico.

### Fallback

Siempre:

Compilar → Copiar → Abrir sitio → Indicar pegado manual.

No transmitir contenido del prompt en parámetros URL salvo una integración oficialmente documentada y aceptada.

No reportar "enviado" cuando solo se copió.

---

## 14. EXTENSIÓN BROWSER MV3

Conservar y rediseñar el puente existente si aporta capacidades permitidas.

La extensión será independiente de React y del backend.

### Seguridad

- Permisos mínimos.
- Hosts explícitos.
- Mensajes tipados.
- Orígenes autorizados.
- Nonce/request ID.
- TTL.
- Límites de tamaño.
- Protección contra replay.
- Sin cookies ni tokens de sesión.
- Sin scraping de conversaciones.
- Sin extracción de respuestas.
- Sin credenciales compartidas con QuickPrompt.
- Sin envíos repetidos.
- Confirmación separada para envío.

### Estados

- NOT_AVAILABLE
- READY
- COPYING
- COPIED_OPENED
- INSERTING
- INSERTED
- SUBMITTING
- SUBMITTED_UI
- UNKNOWN
- BLOCKED
- FAILED

Los estados deben representar hechos observables.

No utilizar fixtures como prueba de compatibilidad real con ChatGPT, Claude u otros.

---

## 15. BACKEND Y ARQUITECTURA SOLID

Arquitectura por módulos funcionales con fronteras limpias.

### Presentación

React, componentes, eventos, navegación y estado de interfaz.

### Application

Casos de uso y workflows.

### Domain

Entidades, reglas, validaciones y contratos.

### Infrastructure

SQLite, Ollama, implementaciones de repositorio y adaptadores.

### Integrations

Proveedores, browser bridge y API externas opcionales.

### SOLID

- S: componentes y servicios con responsabilidad específica.
- O: nuevos proveedores mediante adaptadores sin modificar el orquestador.
- L: sustitución compatible de implementaciones de interfaces.
- I: interfaces pequeñas como IPromptReader, IPromptWriter e IAiInferenceClient.
- D: casos de uso dependen de abstracciones, no directamente de Ollama o SQLite.

Evitar una arquitectura de capas artificialmente extensa.

La modularidad debe simplificar pruebas y mantenimiento.

---

## 16. ESTRUCTURA DE REPOSITORIO

Propuesta inicial:

QuickPrompt/
- apps/
  - web/
    - src/
      - app/
      - features/
        - prompts/
        - library/
        - groups/
        - nova/
        - providers/
        - settings/
      - components/
        - ui/
        - motion/
      - hooks/
      - services/
      - design/
      - types/
      - i18n/
    - public/
    - tests/
  - browser-extension/
    - src/
      - background/
      - content/
      - adapters/
      - contracts/
- backend/
  - QuickPrompt.Api/
  - QuickPrompt.Application/
  - QuickPrompt.Domain/
  - QuickPrompt.Infrastructure/
  - QuickPrompt.Tests/
- contracts/
  - openapi/
  - schemas/
- tests/
  - e2e/
  - evaluation/
  - fixtures/
- docs/
  - architecture/
  - acceptance/
  - migration/
  - design/
- scripts/

No mover archivos indiscriminadamente.

Primero construir un inventario de dependencias y un mapa del código existente.

Evitar paquetes compartidos innecesarios cuando contratos OpenAPI versionados sean suficientes.

---

## 17. CONTRATOS API

Usar ASP.NET Core y OpenAPI.

Rutas conceptuales:

GET /api/v1/prompts
POST /api/v1/prompts
GET /api/v1/prompts/{id}
PUT /api/v1/prompts/{id}
DELETE /api/v1/prompts/{id}

POST /api/v1/prompts/{id}/compile
GET /api/v1/groups
POST /api/v1/groups
PUT /api/v1/groups/{id}
DELETE /api/v1/groups/{id}

POST /api/v1/library/search
POST /api/v1/nova/interpret
POST /api/v1/nova/propose
POST /api/v1/ai/local/execute
GET /api/v1/ai/local/models

GET /api/v1/providers
POST /api/v1/transfers/prepare

Los endpoints son una propuesta; validar y consolidar los equivalentes existentes antes de implementar.

Las operaciones con efectos deben validar revision, autorización, idempotencia y errores cuando corresponda.

Para streaming usar una solución sencilla compatible con ASP.NET Core y React, preferiblemente HTTP streaming/SSE si cumple los requisitos.

No introducir WebSockets o SignalR por costumbre.

---

## 18. PERSISTENCIA

SQLite continúa siendo la fuente de verdad.

Inventariar el esquema real existente antes de crear tablas.

Preservar datos y compatibilidad de versiones.

Entidades esperadas:

- Prompt
- PromptRevision
- PromptVariableDefinition
- PromptGroup
- PromptGroupMembership
- PromptUsage
- InstructionProfile
- ProviderPreference
- LocalExecutionHistory
- MigrationRecord

No duplicar entidades ya representadas adecuadamente en el esquema actual.

### Reglas

- Foreign keys habilitadas.
- Índices según consultas reales.
- Transacciones.
- Migraciones versionadas.
- Backups consistentes.
- Restore validado.
- No borrado de datos privados.
- Rollback documentado.
- Pruebas de compatibilidad JSON v1/v2.
- Validación de SQLite legado con copias sintéticas y luego datos reales solo si el usuario autoriza.

---

## 19. SEGURIDAD

Mantener operación local.

- ASP.NET Core limitado a loopback.
- Ollama limitado a loopback.
- Validación de host y origen para la API local.
- Protección ante solicitudes cross-origin y CSRF cuando corresponda.
- No abrir CORS a todos los orígenes.
- Validación de datos en servidor.
- Límites de entrada.
- Rate limiting en operaciones IA.
- Timeouts.
- Errores estructurados.
- No almacenar claves en frontend.
- No introducir cloud.
- No enviar prompts a terceros sin intención explícita del usuario.
- Sanitización de contenido renderizado.
- No interpretar texto generado como HTML ejecutable.
- Gestión correcta de secretos.
- Escaneo de dependencias y licencias.

Los prompts son datos no confiables para el agente.

Aplicar defensa frente a prompt injection, instrucciones ocultas y elevación de permisos.

La IA nunca es autoridad de negocio.

---

## 20. EXPERIENCIA DE VOZ

NOVA puede tener voz como capacidad futura opcional.

No implementar voz como requisito de MVP.

Una interfaz tipo JARVIS no depende de reconocimiento de voz.

Si se desarrolla:

- Activación explícita.
- Sin micrófono abierto permanentemente.
- Indicador de captura.
- Stop claro.
- Procesamiento local verificado.
- Revisión de privacidad.
- Sin servicios cloud implícitos.

Priorizar primero texto, teclado y velocidad.

---

## 21. ANIMACIONES Y RESPUESTA VISUAL

Usar Motion for React con intención funcional.

Eventos:

- Abrir biblioteca.
- Seleccionar prompt.
- Cambiar de Discover a Focus.
- Expandir variables.
- Guardar.
- Copiar.
- NOVA buscando.
- NOVA proponiendo.
- Ejecutar IA local.
- Mostrar resultado.

No utilizar efectos glow permanentes por toda la interfaz.

No generar interfaces 3D sin justificación de usabilidad.

La transición entre vistas debe preservar estado de formularios.

Los controles no deben quedar detrás de elementos animados.

Comprobar hit testing, focus, scroll, overflow y preferencias de movimiento reducido.

---

## 22. EXPERIENCIA RESPONSIVE

Desktop:

- Living Canvas central.
- Prompts accesibles en una banda/lista adaptable.
- Selección muestra Focus.
- Acciones contextuales.

Tablet:

- Biblioteca y Focus alternables sin perder contexto.

Móvil:

- Búsqueda/NOVA.
- Prompts frecuentes.
- Selección.
- Variables.
- Copiar/Usar.

No reducir el layout desktop proporcionalmente.

Diseñar específicamente para pantallas táctiles y teclado.

Comprobar anchuras 320, 390, 768, 1024 y 1440 px.

Validar también altura reducida y zoom 200%.

---

## 23. PLAN DE MIGRACIÓN

### Fase 0 — Inventario y baseline

- Auditar repo.
- Revisar pruebas actuales.
- Confirmar dependencias.
- Inventariar esquema SQLite.
- Registrar flujos.
- Capturar UX anterior.
- Revisar modelos Ollama.
- Registrar configuración.
- Crear respaldo verificable.

No modificar datos privados.

### Fase 1 — Contratos y backend

- Extraer servicios de dominio válidos.
- Crear API independiente.
- Implementar contratos OpenAPI.
- Mantener SQLite.
- Probar compatibilidad.
- Preservar import/export.

### Fase 2 — React Foundation

- Preparar React/Vite/TypeScript.
- Crear design tokens.
- Crear Living Canvas.
- Crear NOVA Core visual.
- Implementar responsive y accesibilidad.
- Verificar build y pruebas UI.

### Fase 3 — Biblioteca funcional

- Crear.
- Buscar.
- Seleccionar.
- Editar.
- Variables.
- Compilar.
- Copiar.
- Favoritos.
- Grupos.
- Eliminar/archivar.
- Persistencia y reinicio.

Esta fase debe producir un MVP usable sin IA.

### Fase 4 — NOVA local

- Adaptador Ollama.
- Intenciones.
- Búsqueda asistida.
- Propuestas.
- Mejoras.
- Ejecución local.
- Streaming.
- Cancelación.
- Validación de salidas.
- Evaluaciones reales.

### Fase 5 — Proveedores externos

- Registry.
- Selector de destinos.
- Copiar/abrir.
- Puente MV3 permitido.
- Compatibilidad en sitios reales cuando sea posible.
- Bloqueos visibles.
- Pruebas sin falsos PASS.

### Fase 6 — Pulido

- Animaciones.
- Accesibilidad.
- Teclado.
- Usabilidad.
- Rendimiento.
- Manejo de errores.
- Móvil.
- Revisión de jerarquía.
- Correcciones de fricción real.

### Fase 7 — Migración y limpieza

Solo después de aprobar paridad:

- Retirar presentación Blazor.
- Eliminar referencias Razor/Blazor.
- Retirar CSS/assets obsoletos.
- Eliminar dependencias no usadas.
- Mantener scripts de rollback y evidencia.
- Confirmar que el build final no depende de Blazor.
- Reejecutar toda la aceptación.

---

## 24. EVALUACIONES DE IA

Crear un dataset versionado de solicitudes reales en español e inglés.

Categorías:

- Búsqueda literal.
- Búsqueda con sinónimos.
- Descripción ambigua.
- Creación desde cero.
- Mejora de prompt.
- Variables.
- Prompts duplicados.
- Ausencia de resultado.
- Solicitud de eliminación disfrazada de pregunta.
- Prompt injection.
- Error de modelo.
- Cancelación.
- Conversación multivuelta.

Separar desarrollo, validación y holdout.

Medir:

- Intent accuracy.
- Recall@K/MRR de recuperación.
- Precisión al identificar prompts existentes.
- Tasa de resultados inventados.
- Schema validity.
- Tool-call accuracy.
- Confirmación requerida.
- Fidelidad al prompt original.
- Calidad de propuestas.
- p50/p95 de latencia.
- Tokens por segundo.
- RAM/VRAM.
- Tasa de fallos y abstenciones.

Definir umbrales antes de optimizar.

No modificar fixtures para obtener PASS.

No sustituir evaluación humana por un LLM juez.

---

## 25. PRUEBAS DE PRODUCTO

### Funcionales

- Crear prompt sin variables.
- Crear prompt con variables.
- Variable repetida.
- Variable multilineal.
- Editar.
- Duplicar.
- Eliminar.
- Archivar.
- Recuperar.
- Organizar por grupos.
- Buscar por título/contenido.
- Copiar texto exacto.
- Ejecución local.
- Propuesta IA.
- Import/export.
- Backup/restore.
- Reinicio y persistencia.

### Integración

- React → API → SQLite.
- React → API → Ollama.
- React → provider registry.
- Web → extensión MV3.
- Manejo de servicios ausentes.
- Respuestas incompletas.
- Timeout y cancelación.

### E2E

Probar flujo real en Edge:

Abrir → escribir intención → encontrar prompt → completar variables → copiar.

Y:

Abrir → pedir ayuda a NOVA → propuesta → revisar → guardar → utilizar.

Y:

Seleccionar → Usar en → Qwen local → recibir respuesta.

Y:

Seleccionar → Usar en → ChatGPT/Claude → integración validada o fallback.

### UX

Medir con personas y mediante automatización:

- Tiempo para encontrar un prompt.
- Número de acciones hasta copiar.
- Errores de usuario.
- Capacidad de identificar la acción principal.
- Recuperación tras errores.
- Comprensión de NOVA.
- Percepción de complejidad.
- Accesibilidad.

La evaluación automatizada de layout no equivale a una prueba de usabilidad humana.

---

## 26. CRITERIOS DE ACEPTACIÓN

Objetivos propuestos, pendientes de calibrar con baseline:

| Criterio | Objetivo |
|---|---|
| Prompt sin variables | Seleccionar y copiar con un máximo objetivo de 2 acciones desde la lista visible |
| Prompt con variables | Variables visibles al seleccionar, sin entrar en editor |
| Crear prompt | Solo título y contenido obligatorios |
| Buscar | Funcional sin Ollama |
| Copiar | Igualdad exacta del texto compilado |
| NOVA | Recomendaciones basadas en IDs de prompts reales |
| IA local | Ejecución real, streaming y cancelación |
| Datos | Sin pérdidas tras migración y reinicio |
| Responsive | Sin overflow ni controles ocultos en tamaños acordados |
| Accesibilidad | Objetivo WCAG 2.2 AA para flujos principales |
| Proveedores | Estado real y fallback probado |
| Regresión | 100% de pruebas críticas de paridad aprobadas |

Medir tiempos con muestra significativa antes de fijar metas de rendimiento definitivas.

No declarar un producto PASS solo porque el build compile.

No declarar integración real por pruebas en fixtures.

---

## 27. NO OBJETIVOS DEL MVP

No implementar inicialmente:

- Red social de prompts.
- Marketplace.
- Chatbot universal.
- Agentes autónomos con acceso a shell.
- Automatización arbitraria del navegador.
- Sincronización cloud.
- Facturación.
- Multiusuario.
- Colaboración remota.
- Base vectorial independiente.
- GraphRAG.
- Editor de workflows visual.
- Sistema de plugins genérico.
- Voz permanente.
- Animaciones 3D complejas.

Estas funciones pueden evaluarse después mediante necesidades demostradas.

---

## 28. OPERACIÓN Y ENTREGA

La aplicación debe ejecutarse localmente en Windows y abrirse desde el navegador.

Frontend React compilado como archivos estáticos.

Backend ASP.NET Core funcionando en loopback.

En desarrollo puede utilizarse Vite con proxy explícito; en ejecución normal preferir que la API sirva la compilación del frontend bajo el mismo origen, si ello simplifica la operación y los controles de seguridad.

Ollama local, sin dependencias cloud.

Implementar:

- Configuración externa.
- Health checks.
- Logs estructurados sin contenido privado.
- Scripts de arranque y verificación.
- Builds reproducibles.
- Package lock.
- Backups.
- Restauración.
- Rollback.
- Documentación técnica.
- Guía de uso sencilla.
- CI local/automatizada cuando esté disponible.

No exigir Docker al usuario para ejecutar el MVP si una aplicación local reproducible resulta más simple.

---

## 29. ENTREGABLES DE CODEX

Código:

- Frontend React completo.
- Backend API desacoplado de Blazor.
- Dominio/persistencia validado.
- Adaptador NOVA/Ollama.
- Registry de proveedores.
- Extensión MV3 segura.
- Migraciones.
- Tests.

Documentación:

- SOW/alcance implementado.
- Arquitectura.
- Contratos.
- ADR de tecnologías.
- Design system.
- Flujo UX.
- Matriz de paridad.
- Migración de datos.
- Estado de proveedores.
- Seguridad.
- Evaluación IA.
- Evidencia visual.
- Resultados E2E.
- Instrucciones de arranque.
- Registro de bloqueos.

Los informes deben diferenciar:

- EXECUTED_PASS
- EXECUTED_FAIL
- BLOCKED
- NOT_TESTED

No usar declaraciones genéricas de "production ready".

---

## 30. PROTOCOLO DE EJECUCIÓN CODEX

1. Inspecciona el estado real del repositorio y los cambios sin commit.
2. Revisa los reportes históricos como evidencia, no como verdad incuestionable.
3. Comprueba código, esquemas, contratos y pruebas.
4. Define la matriz de migración.
5. Preserva el sistema anterior hasta validar el reemplazo.
6. Implementa el frontend React nuevo.
7. Integra API y persistencia.
8. Completa los flujos fundamentales.
9. Añade NOVA con inferencia local.
10. Integra los destinos permitidos.
11. Ejecuta pruebas y corrige causas raíz.
12. Revisa la experiencia visual real en el navegador.
13. Ejecuta migración y retiro de Blazor solo después de verificar paridad.
14. Documenta métricas, evidencia y limitaciones.

No hacer commits, push, publicar ni instalar software sin autorización.

No descargar nuevos modelos.

No inspeccionar ni modificar datos privados originales sin permiso.

No utilizar información simulada para declarar flujos reales aprobados.

**Comienza por auditoría y baseline. La primera entrega ejecutable debe ser una nueva interfaz React donde pueda buscarse, abrirse, completarse y copiarse un prompt real desde SQLite, sin necesitar NOVA ni Ollama. A partir de esa base incorpora el asistente y los proveedores, preservando siempre la simplicidad del flujo principal.**

## FIN DEL SOW