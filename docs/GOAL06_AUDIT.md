# GOAL 06 — auditoría y decisiones

Fecha del informe: 2026-10-16, America/La_Paz. [Solicitud conservada](GOAL06.md). Auditoría realizada antes de modificar implementación; no se movieron proyectos ni se cambió el esquema SQLite.

## Baseline observado

- Rama `feature/provider-bridge-tailwind`; HEAD `474f2b203af207d7310a60787a5b946974817087`. `git ls-remote origin refs/heads/master` devolvió el mismo SHA. El remoto es [JuanPabloTorres/QuickPrompt](https://github.com/JuanPabloTorres/QuickPrompt); se auditó el código móvil clonado de ese commit, sin descargar su DB.
- El árbol ya contenía cambios en `.gitignore`, `QuickPrompt.csproj`, README y directorios web/pruebas/documentación/extensión sin seguimiento. No representan cambios exclusivos de GOAL06. [146 hashes iniciales de fuentes](evidence/goal06/baseline/source-hashes.json); originales de trabajo conservados fuera del control de versiones en `.playwright/goal06-original/`.
- SDK instalado: 8.0.425. Ollama 0.40.4, modelos existentes Qwen3:8b, Qwen2.5:7b-instruct y Nomic. No se instaló SDK, paquete nuevo, modelo ni servicio. Las restauraciones del build aislado usaron únicamente caché NuGet y una fuente local vacía.
- [127/127 pruebas](evidence/goal06/baseline/tests.log); CSS reproducible y seis contratos de puente PASS. [Recorrido Edge sin Ollama](evidence/goal06/baseline/users/users.json): crear, completar, copiar, favoritos, grupos y copiar/abrir funcionaban.
- Una instancia normal existente, PID23488 al inspeccionarla, mantenía bloqueadas las DLL del output convencional. El build convencional encontró MSB3026; se utilizó `--artifacts-path .playwright/goal06-build`, sin detener aquella instancia ni leer su biblioteca. Es un aislamiento de pruebas, no un cambio de despliegue.

## Paridad efectiva

| Flujo | Evidencia móvil | Baseline web | Cambio GOAL06 |
|---|---|---|---|
| Crear/guardar | `Pages/MainPage.xaml`, `MainPageViewModel` | Título y contenido; metadatos opcionales | Conservado |
| Seleccionar → variable | `MainPage.xaml.cs`, `OnCreateVariableFromSelection`: pide/sanea nombre y sustituye selección | `qp.wrap` solo insertaba `<`/`>`; texto multilineal/puntuación podía producir un marcador inválido | Nombre propuesto editable y validación C#; selección real en Edge |
| Variable → texto | `OnUndoVariableFromSelection`: selección completa, quita marcador | Sin acción equivalente | Recupera original de sesión o nombre de marcador manual; reversible |
| Renombrar/Undo/Redo | Edición de chip móvil; sin evidencia de historial general robusto | Modificación manual, sin historial propio | Repetidos/defaults/escapes consistentes e historial de 100 estados |
| Sin variables | Generación en `Features/Prompts/ViewModels/PromptDetailsPageViewModel.cs` | Copia directa, sin campos obligatorios | Conservado y probado |
| Biblioteca/favoritos | `QuickPromptPage.xaml`, `QuickPromptViewModel` | Lista + uso, filtros y grupos ya presentes | Búsqueda también por nombre de categoría/grupo |
| Final/destinos | `PromptDetailsPage.xaml`, `GeneratePromptAsync`, selección de motor | Un CompiledPrompt compartido por acciones; Values aún mutable | Copia defensiva y diccionario de solo lectura; JSON compatible |
| Compartir | `SharePromptService`, `SharePromptAsync` | Copia final y respaldo completo | Plantilla portátil separada, copiar texto y Web Share con fallback |
| Importar | JSON histórico y DB móvil | Validación/transacción; guardado inmediato | Preview/cancelar/confirmar, detección explícita de duplicados/conflictos |
| IA local | No equivalente local comprobado en móvil | Ollama, mejora/creación/sugerencias/streaming existentes | Quick Assist: comandos deterministas y planes tipados con revisión |
| Navegador | WebView legado | MV3 de transferencia ya existente | Documento de panel lateral, biblioteca local y compilación C# |

La verificación del móvil es estática. Su runtime MAUI requiere un SDK/workloads ausentes; ejecución móvil: **BLOCKED**. No se equipara lectura de XAML con un recorrido móvil ejecutado.

## Arquitectura actual y riesgos

Blazor InteractiveServer → dominio puro y `LocalStore` → SQLitev2; `ILocalLlmClient` → Ollama loopback. El puente de proveedores sigue separado. Biblioteca, motor, asistencia y conexión son responsabilidades internas de los proyectos existentes, sin crear Application/Infrastructure adicionales ni mover el legado.

Fortalezas: transacciones SQLite, control optimista por revisión, parser determinista, CSP y host/origin locales, propuestas revisadas, historial independiente, salida manual universal y fixtures aislados. La biblioteca no requiere inferencia.

| Hallazgo | Impacto / riesgo | Decisión y evidencia |
|---|---|---|
| Conversión textual ingenua y operaciones inversas ausentes | Alto para paridad; cambio acotado | `TemplateEditor` + componente específico; pruebas de texto original, Unicode, escapes, colisiones, repetidos e historial |
| Intercambio individual confundido con respaldo privado | Alto para privacidad | Proyección allowlist `TemplatePackage`, sin defaults, valores, historial, versiones, favoritos, IDs ni configuración |
| Preview ausente | Medio para control de importación | Parse/validación compartidos; confirmar usa la misma cadena, vuelve a validar y escribe transaccionalmente |
| Values mutable dentro de record | Medio para integridad del snapshot | `CompiledPrompt.Values: IReadOnlyDictionary`; copia defensiva y roundtrip probado |
| Home concentra orquestación | Medio para mantenimiento | Parciales de asistencia/intercambio y editor encapsulado; sin reescritura del flujo existente |
| Buscar no incluía categoría/grupo | Medio para encontrar plantillas | Consulta SQL parametrizada con categoría enum y nombres de relaciones; prueba cruzando campos |
| Cabecera de Settings desbordaba en320px con acciones contextuales | Medio para móvil | Reproducción [FAIL](evidence/goal06/new-ui/failure.png); flex-wrap y [30 combinaciones PASS](evidence/goal06/new-ui/layout.json) |
| Cambio de valores mientras el panel esperaba compilación | Medio: copia de valores anteriores | Generación de entrada capturada y comprobada antes de copiar; regresión con respuesta demorada |

## Límites de seguridad comprobados

- No nuevas dependencias ni endpoints HTTP; no CORS/iframe relajados. El panel habla con un content script del origen exacto y una referencia .NET de métodos tipados. Valida plantilla, revisión, campos y límites. No lee defaults, historial, cookies ni conversaciones.
- El registro de una pestaña local guarda solo su ID en `chrome.storage.session`. Conectar es explícito; las plantillas y valores viven en memoria del panel. El permiso añadido es `sidePanel`; no `tabs`, cookies, debugger ni hosts nuevos. La biblioteca se comparte con la extensión únicamente al conectar/buscar; compilar registra un uso tras Copiar.
- Quick Assist no tiene acciones enviar, borrar, ejecutar código o autenticar. Máximo cuatro pasos, selección única para mutaciones, revisión de plantilla y transacción para cambios persistentes. Interpretar/proponer no autoriza guardar. Las sugerencias se revisan otra vez antes de usarlas.
- Un plan puede completar los cambios persistentes aprobados y luego fallar al pedir una sugerencia a Ollama. No se promete transacción distribuida con el modelo; el error es visible y no se guarda una respuesta parcial.
- Playwright interpreta una lista cerrada de acciones. QA solo loopback; supervisión con orígenes exactos, máximo20 pasos/120s, contexto nuevo, rutas restringidas, cancelación y confirmación por interacción externa. Sin eval, estado de autenticación ni scripts del modelo. Etiquetas de autenticación se bloquean; los workflows se revisan antes de ejecutarlos. No se habilitó ningún sitio externo en la política entregada.
- Plantillas pueden contener información privada escrita por su autor: revisar su contenido antes de compartir. Un respaldo completo sigue siendo privado deliberadamente. No se promete detectar secretos en texto libre.

Riesgos restantes: single-user local, disponibilidad de Ollama/navegador, paridad de docking no probada, condiciones DOM externas cambiantes, y ausencia de evaluación humana. No se reclama auditoría exhaustiva de vulnerabilidades o certificación WCAG.
