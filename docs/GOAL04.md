# GOAL 04 — QuickPrompt Web: recuperación funcional, UX profesional y organización avanzada

## 1. Misión

Actúa como arquitecto de software .NET, ingeniero de IA local, especialista en Blazor/Tailwind, diseñador de experiencia de usuario y evaluador de calidad.

Trabaja sobre el proyecto existente:

`C:\Projects\Repos\QuickPrompt-Web`

**Objetivo principal:** corregir los problemas funcionales y visuales detectados por el usuario y convertir QuickPrompt Web en una aplicación profesional, intuitiva y confiable para gestionar, organizar, completar, mejorar y ejecutar prompts e instrucciones reutilizables.

No reconstruyas el proyecto desde cero. No sustituyas funcionalidades aprobadas. No confundas las pruebas de componentes con la aceptación del producto completo.

El usuario ha identificado los siguientes defectos reales:

1. El layout presenta superposiciones entre menús, paneles, formularios y otros componentes.
2. La interfaz no tiene una distribución profesional ni suficientemente intuitiva.
3. Los botones de ChatGPT y Claude no completan los editores de los proveedores reales.
4. Los flujos para utilizar prompts y ejecutar consultas mediante Qwen local no funcionan correctamente desde la experiencia normal del usuario.
5. Falta una organización por grupos personalizados, independiente de las categorías.
6. Las instrucciones reutilizables necesitan guardarse, organizarse y aplicarse a diferentes prompts.

Estos defectos tienen prioridad sobre nuevas funcionalidades secundarias.

## 2. Auditoría inicial obligatoria

Antes de modificar código:

- Inspecciona la estructura actual, archivos modificados, dependencias y configuración.
- Lee `PHASE03_ACCEPTANCE.md`, `PRODUCT_READINESS.md`, contratos del puente MV3 y evidencia E2E.
- Examina los componentes Blazor, ViewModels/servicios, CSS compilado, configuración Tailwind, SQLite y cliente Ollama.
- Identifica las causas raíz de las superposiciones y acciones que aparentemente no funcionan.
- Comprueba posibles fallos de eventos Blazor, asincronía, renderizado, estado de navegación, JavaScript interop y lifecycle.
- Verifica la estructura del código de la extensión y su comunicación real con QuickPrompt.
- Conserva los cambios existentes y las evidencias originales.

Abre la aplicación en su **modo normal**, no Demo, y reproduce los problemas con Edge.

Construye una matriz:

`Funcionalidad → comportamiento esperado → comportamiento observado → causa → corrección → prueba → evidencia`.

Si las pruebas existentes pasan pero la operación real falla, clasifica el escenario como FAIL y añade una prueba que reproduzca el defecto.

No avances sobre supuestos de funcionalidad.

## 3. Reparación integral del layout

Audita todos los componentes visuales, especialmente:

- Sidebar y navegación principal.
- Editor de prompts.
- Formularios de variables.
- Menús contextuales y desplegables.
- Selectores de proveedores de IA.
- Diálogos y confirmaciones.
- Toasts y notificaciones.
- Paneles de resultados.
- Modales de mejora de prompts.
- Estados de carga y skeletons.

Investiga stacking contexts, `z-index`, `position`, `overflow`, contenedores con transformaciones, scroll anidado y elementos posicionados incorrectamente.

Implementa una estrategia consistente para overlays mediante un host central, portales o el mecanismo apropiado de Blazor.

No soluciones las superposiciones aumentando arbitrariamente valores de `z-index`.

Los menús deben abrirse dentro del área visible, ajustándose al espacio disponible. Los modales deben mantener el foco correctamente y permitir cerrarse mediante Escape cuando corresponda.

Ningún componente debe ocultar acciones críticas ni tapar otros controles inesperadamente.

## 4. Modernización profesional de UI/UX

Usa Tailwind CSS 4.3.0, ya integrado en el proyecto, sin introducir otro framework visual innecesario.

Conserva la identidad original de QuickPrompt: azul profundo, teal, iconografía reconocible y estilo tecnológico.

Refina la aplicación con un sistema visual coherente:

- Tokens para colores, superficies, espaciado, tipografía, radios y elevación.
- Familia SVG de iconos consistente.
- Sidebar adaptable.
- Encabezados y jerarquía visual claros.
- Biblioteca de prompts fácil de recorrer.
- Formularios bien alineados.
- Botones con jerarquía de acciones.
- Menús compactos con etiquetas comprensibles.
- Skeletons y loading específicos por operación.
- Feedback inmediato ante acciones exitosas o fallidas.
- Estados vacíos útiles.
- Modales con dimensiones y comportamiento consistentes.
- Transiciones discretas y respeto a `prefers-reduced-motion`.
- Navegación completa mediante teclado.
- Temas claro y oscuro, ES/EN.

No utilices animaciones decorativas que afecten al rendimiento o retrasen acciones.

Prioriza velocidad de interacción, legibilidad, contraste y claridad operacional.

Valida en 320, 390, 768, 1024 y 1440 píxeles. Comprueba también ventanas de escritorio con altura reducida.

La experiencia principal debe poder entenderse sin instrucciones externas.

## 5. Organización mediante grupos personalizados

Las categorías actuales deben permanecer, pero añade un sistema independiente de grupos o colecciones creados por el usuario.

Ejemplos:

- Desarrollo de software
- Codex Goals
- Arquitectura de IA
- Trabajo
- Prompts personales
- Investigación

Un prompt puede pertenecer a varios grupos sin duplicar su contenido.

Permite:

1. Crear grupos con nombre, descripción opcional, icono y color.
2. Renombrar y editar grupos.
3. Agregar y quitar prompts de un grupo.
4. Seleccionar múltiples prompts y asignarlos a grupos.
5. Buscar y filtrar por grupo, categoría, favoritos y texto.
6. Reordenar o fijar grupos importantes.
7. Visualizar prompts sin grupo.
8. Eliminar un grupo sin eliminar sus prompts.
9. Mostrar contadores correctos.
10. Conservar grupos en respaldos, importaciones y restauraciones.

Diseña una relación persistente entre prompts y grupos, con claves foráneas, índices y restricciones de unicidad.

Implementa migraciones SQLite versionadas, transaccionales e idempotentes.

No alteres ni elimines los datos existentes.

El sistema deberá soportar cientos o miles de plantillas sin cargar innecesariamente todo el catálogo en memoria.

No implementes jerarquías complejas de carpetas hasta que exista una necesidad demostrada.

## 6. Biblioteca de instrucciones reutilizables

Añade un concepto de dominio separado de las plantillas: **Instruction Profile**.

Una instrucción puede definir comportamiento, rol, estilo, restricciones, formato de respuesta o criterios de ejecución.

Ejemplo:

Nombre: Software Architecture

Instrucciones:
"Actúa como arquitecto de software. Evalúa seguridad, contratos, mantenibilidad y pruebas. Evita modificaciones innecesarias y soluciones específicas por ejemplo."

Permite:

- Crear, editar, duplicar y eliminar instrucciones.
- Asignarles categorías y grupos personalizados.
- Buscar y marcar favoritas.
- Versionarlas.
- Seleccionar una instrucción para utilizarla con una plantilla.
- Guardar combinaciones reutilizables de instrucciones y prompts.
- Obtener una vista previa del contenido final.
- Copiar o ejecutar la composición.

Distingue explícitamente:

- Instrucción del sistema o perfil.
- Plantilla.
- Variables.
- Prompt compilado.
- Mensaje enviado.
- Respuesta obtenida.

Para Ollama, conserva los roles apropiados cuando el contrato lo permita.

Para sitios externos sin soporte de roles separados, construye una representación textual explícita y revisable. Nunca presentes esa representación como si fuera un mensaje de sistema privilegiado.

Define una precedencia determinista entre instrucciones generales, instrucciones seleccionadas y ajustes específicos del prompt. Ante conflictos o múltiples perfiles posibles, exige una selección explícita.

El contenido guardado nunca debe modificarse silenciosamente mediante IA.

## 7. Reparar el flujo con Ollama local

Realiza una prueba E2E real:

Biblioteca → seleccionar prompt → completar variables → seleccionar instrucción → compilar → previsualizar → ejecutar con Qwen3 → mostrar respuesta → guardar resultado.

Debe funcionar con `qwen3:8b` instalado y Ollama local.

Audita:

- Disponibilidad del servidor.
- Descubrimiento de modelos.
- Selección del modelo.
- Contratos del cliente HTTP.
- Streaming.
- Actualización del estado Blazor.
- Cancelación real.
- Timeouts.
- Reintentos controlados.
- Gestión del contexto.
- Errores de conexión.
- Persistencia de resultados.
- Recuperación después de navegar o reiniciar.

La interfaz debe distinguir entre preparar un prompt y ejecutar una consulta.

Las acciones de IA deben ofrecer:

- Mejorar prompt.
- Sugerir variables.
- Crear desde una descripción.
- Analizar el prompt.
- Ejecutar consulta.
- Guardar resultado.

Las propuestas de modificación deben mostrarse antes de aplicarse.

Un fallo de Ollama no debe impedir buscar, editar, completar, organizar ni copiar prompts.

No declares funcional el flujo solamente porque `/api/chat` responde. Comprueba que el usuario recibe el resultado completo en la interfaz normal.

## 8. Corregir la integración ChatGPT / Claude

Audita por completo:

- `PromptTransferRequest`.
- `IWebPromptTransfer`.
- Mensajería de extensión MV3.
- Service worker.
- Content scripts.
- Adaptadores individuales.
- Detección de extensión.
- Permisos de origen.
- Estado del formulario.
- Transferencias pendientes.
- Confirmaciones y fallbacks.

Objetivo:

El usuario selecciona un prompt terminado, pulsa ChatGPT o Claude y el sistema intenta transferir **exactamente ese texto** al editor autorizado del proveedor.

Implementa una máquina de estados explícita:

`CREATED → PREPARED → TRANSFERRING → INSERTED / FAILED / BLOCKED / UNKNOWN`

El envío, cuando esté permitido, será una transición adicional y explícita, nunca una consecuencia automática de preparar.

Distingue insertar texto de enviar un mensaje. Una confirmación de clic no equivale a entrega verificada.

La extensión debe:

- Verificar el origen exacto de la solicitud.
- Validar contenido, tamaño y caducidad.
- Utilizar identificadores únicos e idempotencia.
- No sobrescribir borradores existentes sin permiso.
- Esperar a que el editor esté disponible con límites de tiempo.
- Actualizar correctamente el editor y los eventos requeridos por la interfaz.
- Confirmar el texto insertado cuando sea verificable.
- Evitar inserciones repetidas.
- Informar los errores concretos.
- No registrar el contenido privado del prompt.

No utilizar scraping de conversaciones, extracción de cookies, automatización de autenticación, evasión de restricciones ni endpoints privados.

### ChatGPT

Verifica mediante una sesión del usuario autorizada, si está disponible.

Prueba primero la inserción, sin envío. El envío debe requerir una acción explícita separada.

Si el sitio devuelve 403 o no existe sesión, documenta `BLOCKED_REAL_PROVIDER` y conserva el fallback.

### Claude

Respeta las restricciones actuales del proveedor. No automatices interacciones prohibidas.

Si el flujo automatizado no está autorizado, deja disponible copiar y abrir, con indicación clara de que el usuario deberá pegar el contenido manualmente.

No marques fixtures como éxito de integración externa.

### Fallback obligatorio

Cuando la integración directa no esté disponible:

1. Copiar el prompt compilado completo al portapapeles.
2. Abrir el proveedor seleccionado por acción del usuario.
3. Mostrar confirmación de copia y una indicación clara para pegar.
4. Conservar el prompt sin modificaciones.
5. Registrar el resultado como `COPIED_OPENED`, nunca `SENT`.

No inventes una solución para saltar las protecciones del navegador.

## 9. Rediseñar la experiencia de uso de proveedores

En el editor final presenta acciones claras:

- Copiar prompt.
- Ejecutar con Qwen local.
- Preparar en ChatGPT.
- Preparar en Claude.
- Guardar como versión.
- Guardar como favorito.

Muestra estados reales:

- Disponible.
- Ejecutando.
- Preparado.
- Copiado.
- Conexión no disponible.
- Extensión no detectada.
- Integración externa bloqueada.
- Error recuperable.

No muestres botones que aparenten funcionar cuando no hay integración operativa.

Los botones deben estar correctamente habilitados o deshabilitados y explicar el motivo.

Si existe una sesión de IA en progreso, evita acciones concurrentes que puedan sobrescribir su estado.

## 10. Arquitectura y seguridad

Preserva ASP.NET Core, Blazor, SQLite y la integración actual de Tailwind.

Separa:

- Presentación.
- Aplicación.
- Dominio.
- Persistencia.
- Compilador de plantillas.
- Gestión de instrucciones.
- Proveedores de IA.
- Transferencia a navegadores.
- Historial y versiones.

No permitas que el LLM controle directamente operaciones CRUD, permisos, datos de grupos ni persistencia.

El compilador de variables debe ser determinista.

Usa contratos tipados, validación, CancellationToken, errores controlados e idempotencia donde corresponda.

Mantén el servidor limitado a loopback y no introduzcas servicios cloud.

No instales dependencias ni SDK nuevos, descargues modelos, publiques cambios ni modifiques datos privados sin autorización.

## 11. Pruebas obligatorias

Ejecuta y amplía las pruebas existentes. El baseline reportado es 81/81, pero debes verificarlo realmente.

### Dominio y SQLite

- CRUD de prompts.
- CRUD de grupos.
- Relaciones muchos-a-muchos.
- Eliminación de grupos sin pérdida de prompts.
- Edición y versionado de instrucciones.
- Compilación de variables.
- Composición de instrucciones.
- Importación y exportación.
- Migraciones y rollback.
- Reinicio y persistencia real.

### UI y accesibilidad

Prueba en Edge con capturas:

- Cinco anchuras de pantalla.
- Temas claro y oscuro.
- Español e inglés.
- Menús y modales abiertos.
- Editor con paneles expandidos.
- Estados de carga.
- Errores y notificaciones.
- Navegación por teclado.
- Foco y Escape.
- Ausencia de superposiciones no intencionadas.
- Ausencia de overflow horizontal.
- Ausencia de controles inaccesibles.

No basta con comprobar que no hay errores de JavaScript. Debes verificar geometría, visibilidad y operabilidad real de los elementos.

### Ollama

Probar con `qwen3:8b` real:

- Compilación y ejecución completa.
- Mejoras que conservan variables.
- Cancelación.
- Streaming.
- Modelo no disponible.
- Respuesta vacía.
- Timeout.
- Persistencia.
- Recuperación después de reiniciar.

### Proveedores externos

Separar tres niveles:

1. Contratos y pruebas unitarias.
2. Integración en fixtures reales de navegador.
3. Validación en sitios auténticos con sesión autorizada, cuando esté permitida.

Reportar resultados por separado.

No convertir un caso bloqueado en PASS.

## 12. Criterios de aceptación

El objetivo estará completado cuando:

1. El layout sea usable sin solapamientos inesperados en los cinco tamaños de pantalla.
2. Los botones tengan una acción comprensible y un estado verificable.
3. El usuario pueda crear grupos personalizados y gestionar sus prompts sin duplicar registros.
4. Las instrucciones reutilizables puedan guardarse, versionarse y aplicarse correctamente.
5. El flujo local con Qwen3 funcione de extremo a extremo desde la UI normal.
6. Los resultados y cambios autorizados sobrevivan a un reinicio.
7. La integración externa inserte contenido cuando sea técnicamente posible y permitido; en caso contrario, el fallback funcione y el bloqueo sea explícito.
8. Todas las regresiones anteriormente aprobadas sigan funcionando.
9. Las pruebas nuevas cubran los defectos reportados.
10. La evidencia incluya capturas reales, logs, resultados de pruebas y escenarios manuales.

## 13. Entregables

Crear o actualizar:

- `docs/GOAL04_ACCEPTANCE.md`
- `docs/UI_UX_AUDIT.md`
- `docs/PROMPT_GROUPS.md`
- `docs/INSTRUCTION_PROFILES.md`
- `docs/LOCAL_AI_WORKFLOWS.md`
- `docs/PROVIDER_INTEGRATION_STATUS.md`

Registrar cambios, causa raíz, pruebas ejecutadas, resultados y pendientes.

Estado de aceptación por subsistema:

- `PASS`
- `FAIL`
- `BLOCKED`
- `NOT_TESTED`

Estado global:

- `PASS_LOCAL_MVP`
- `PARTIAL_PROVIDER_BLOCKED`
- `FAIL_NO_GO`

No declares la solución comercialmente lista.

## 14. Orden de ejecución obligatorio

Trabaja incrementalmente en este orden:

**Etapa 1:** auditar y reproducir los defectos.

**Etapa 2:** corregir layout, overlays, navegación y estados visuales.

**Etapa 3:** completar los flujos reales de biblioteca, grupos e instrucciones.

**Etapa 4:** reparar integración con Qwen local y validar todo el recorrido.

**Etapa 5:** diagnosticar y mejorar el puente MV3, preservando permisos y restricciones.

**Etapa 6:** pruebas de regresión, revisión visual y aceptación.

Después de cada etapa, ejecuta las pruebas relevantes antes de continuar.

Corrige causas raíz, no casos individuales de prueba.

No detengas el trabajo tras generar documentación o interfaces. Implementa y verifica los flujos de principio a fin.

**Comienza ahora por la reproducción de los defectos observados en la aplicación normal y continúa hasta entregar un MVP local realmente utilizable, con las limitaciones de proveedores externos claramente identificadas.**