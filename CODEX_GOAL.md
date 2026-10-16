# CODEX GOAL — QuickPrompt Web Local-First

## Autoridad y objetivo

Construye una **aplicación web real** llamada QuickPrompt Web, no una aplicación de escritorio, móvil, Electron ni MAUI Hybrid. Usa como fuente de verdad el repositorio original `https://github.com/JuanPabloTorres/QuickPrompt` (rama `master`) ya clonado en este workspace. La app original es referencia funcional, visual y de datos; se desarrolla una aplicación web independiente dentro de `src/`, en la rama de trabajo `feature/web-local-first`. No borres ni alteres indiscriminadamente el legado.

**Problema de usuario:** evitar reescribir prompts y almacenarlos en documentos externos. El producto permite crear una plantilla reutilizable una vez, buscarla al instante, completar variables, guardar versiones y usarla en segundos. La IA local agrega valor al proponer, corregir y completar; el programa sigue funcionando sin IA.

**Objetivo final del MVP:** desde un navegador en Windows, poder crear/importar plantilla → encontrarla → completar variables → generar texto final → copiarlo → ejecutar consulta en Qwen3 local opcionalmente → revisar respuesta y guardar historial. En un flujo aparte, mejorar el prompt con IA sin sobreescribir el original sin aceptación explícita.

### Reglas no negociables

- Enfoque 100% local inicialmente. **No** conectes Firestore, anuncios, telemetría, sincronización cloud, proveedores remotos, analytics ni herramientas de pago. No descargues modelos ni instales componentes del sistema sin autorización. La autorización para clonar el repositorio original está dada.
- No envíes prompts a proveedores externos. Proveedores externos podrán usarse **solo como exportación manual mediante copiar texto / abrir URL**, sin inyección automatizada ni auto-submit.
- No publiques el repositorio, hagas push, crees PR ni subas archivos o datos sin aprobación. Crea cambios locales y evidencia reproducible.
- No conviertas la aplicación en otro chat. La pantalla primaria es la biblioteca y el flujo de reutilización. La ejecución local de prompts es una función adicional.
- La IA interpreta, propone y redacta; el código valida, versiona, autoriza, guarda y aplica cambios. Preguntar no autoriza mutaciones.
- No reescribas un módulo existente que ya funcione sin auditar contratos y pruebas. No alteres pruebas o thresholds para obtener PASS.
- Si no tienes SDK o un recurso necesario, registra `BLOQUEADO` con comando/error exactos, y continúa con el trabajo que sí sea posible. No reportes acciones no ejecutadas.

## Fase 0 — Descubrimiento y línea base (obligatoria)

1. Confirma `git remote -v`, `git status`, commit y rama. Inventaría los componentes y su función real: `AppShell.xaml`, `Pages/MainPage.xaml`, `Pages/PromptBuilderPage.xaml`, `Features/Prompts/**`, `ApplicationLayer/**`, `Core/**`, `Infrastructure/**`, `Resources/Styles/**`, test suites y `QuickPrompt.csproj`.
2. Lee README, auditoría histórica y código efectivo; identifica diferencias entre documentación y ejecución. La cobertura histórica de 77 pruebas/100% no equivale a pruebas actuales.
3. Ejecuta `dotnet --info`, `dotnet --list-sdks`, `ollama --version`, `ollama list`, `ollama ps`. No des por presente .NET 10 hasta verificarlo. Haz una línea base de build/tests **acotados al Windows/SDK realmente instalado**, registrando bloqueos de workloads antiguos.
4. Crea `docs/BASELINE.md`, `docs/FEATURE_PARITY.md`, `docs/DESIGN_AUDIT.md`, `docs/ADR/0001-web-stack.md`. En la matriz separa funcionalidades verificadas de afirmaciones de README. Incluye visuales/espaciado/tokens originales: colores principales `#23486A`, `#3B6790`, `#4C7B8B`, acento `#EFB036`, bordes, tipografías, estados y animaciones.
5. Audita la presencia de `QuickPrompt.db3` versionado. **Trátalo como datos potencialmente privados:** no lo distribuyas, no lo uses como seed público ni lo incluyas en artefactos. Elabora método seguro de importación desde DB seleccionada por usuario o exportación JSON compatible. Nunca modifiques el archivo de origen.

## Fase 1 — Solución web mínima

**Selección principal:** .NET 10 LTS, `ASP.NET Core Blazor Web App` con interactividad de servidor (`InteractiveServer`), CSS propio/componentes Razor reutilizables, servicios .NET, SQLite. Un solo backend hospedado localmente. Si .NET 10 no está disponible, no instalar automáticamente: reportar bloqueo y solo usar otra versión si se justifica y documenta la desviación explícitamente.

No crear una app de escritorio, ni una arquitectura doble React+Blazor, ni microservicios, ni LangChain/LangGraph. Puedes utilizar componentes/accesibilidad nativos de Blazor y librerías de mantenimiento claro si se justifican y autorizan sus dependencias. Priorizar proyectos como `QuickPrompt.Web`, `QuickPrompt.Application`, `QuickPrompt.Domain`, `QuickPrompt.Infrastructure`, `QuickPrompt.Tests`; puedes reducirlos si la separación lógica está cubierta sin sobreingeniería. Mantén la solución web separada del .sln legado.

- Servidor vinculado por defecto a `127.0.0.1` / localhost; no habilitar públicamente LAN en el MVP. `Ollama` accesible **solo desde backend** en `http://127.0.0.1:11434`, configurable y restringido a loopback; nunca exponerlo al JavaScript del cliente.
- Persistencia SQLite transaccional con migraciones versionadas, índice de búsquedas adecuado (FTS5 si mejora frente a LIKE), respaldo/restauración y comprobación de consistencia.
- Contratos typed (DTO/records), validación servidor, errores controlados, cancelación y timeouts, accesibilidad y soporte ES/EN.
- Local single-user para MVP: documentar explícitamente que no es una solución multiusuario/Internet. No simular autenticación empresarial inexistente. Si habilitas acceso desde otro dispositivo posteriormente, exigir autenticación real, HTTPS, CSRF/antiforgery y política de sesiones, además de autorización por usuario.

## Fase 2 — Paridad funcional obligatoria (sin depender de IA)

1. Biblioteca navegable: búsqueda inmediata, categorías, etiquetas si proceden, favoritos, recientes, ordenación, paginación/virtualización, estados vacíos y edición/borrado con confirmaciones.
2. Crear/editar/duplicar/archivar/restaurar plantilla; título, descripción, categoría, texto, variables, fechas, uso y favoritos.
3. Analizador de variables determinista, compatible con los marcadores legados `<variable>`; documentar escapes y nombres permitidos. Compilar con parser/tokenizador, no sustitución global ingenua. Campos obligatorios, valores por defecto, plantillas vacías, repetidos, Unicode, multilinea y caracteres especiales. Jamás interpretar valores como instrucciones de sistema.
4. Vista dual del editor visual/chips y editor textual. Mantener interacción clara y accesible; controles con teclado.
5. Flujo `Usar`: variables → preview completo → copiar con feedback → guardar ejecución finalizada/historial; sugerencias de valores previos opt-in y posibilidad de borrar datos. Separar `plantilla`, `versión`, `render compilado`, `ejecución del LLM` y `propuesta IA` como entidades distintas.
6. Importar y exportar JSON versionado y validado, protegiendo contra payloads corruptos, duplicados, sobrescritura y rutas arbitrarias. Compatibilidad de datos migrados, tests ida-vuelta. Nunca usar `QuickPrompt.db3` del repo como DB nueva.
7. Preferencias (ES/EN, tema claro/oscuro, modelo local seleccionado) y base de ayuda. Interface responsiva (desktop y móvil navegador).

## Fase 3 — IA local y control de cambios

Crea `ILocalLlmClient` detrás de un adaptador Ollama HttpClient con `CancellationToken`, timeouts, límites de salida/contexto, manejo de ausencia de modelo, errores de formato y respuestas parciales. Modelo principal `qwen3:8b`, alternativo `qwen2.5:7b-instruct`. Embeddings `nomic-embed-text-v2-moe:latest` solo tras justificar necesidad (baseline léxico primero).

Funciones independientes:

- **Mejorar**: Qwen devuelve propuesta tipada (texto, razones, cambios, advertencias, variables preservadas). UI diff lado a lado. Nunca persistir ni reemplazar sin confirmación. Guardar versión previa y nueva atómicamente.
- **Crear desde intención**: texto libre breve → borrador propuesto → usuario edita/aprueba → guardar. No crear plantillas a escondidas.
- **Autocompletar variables**: sugerencias por contexto autorizado y valores previos; señalizar incertidumbre y origen, no inventar nombres/datos privados ni asumir autorización de guardar.
- **Ejecutar localmente**: usar prompt final explícitamente confirmado; respuesta con streaming si se puede, botones detener/reintentar/copiar/guardar, prompt y modelo registrados según política de privacidad. No reutilizar automáticamente un resultado como variable.
- **Analizar prompt**: validación estática de variables, objetivos, restricciones y campos faltantes; distinguir reglas deterministas de observaciones generadas por LLM.
- **Búsqueda semántica**: fase posterior y opcional. Solo implementar con Nomic después de probar recuperación BM25/FTS5 en biblioteca suficientemente grande. No instalar base vectorial nueva para el MVP.

Prohibir auto-submit a terceros y ejecución de herramientas arbitrarias. No convertir prompts almacenados en instrucciones privilegiadas, no interpolar respuesta LLM en SQL/HTML sin escapado. Limitar tamaño de campos y evitar introducir datos confidenciales en logs.

## Fase 4 — Diseño web fiel pero modernizado

Recrear el lenguaje visual del original, no copiar XAML literalmente a la web: azul profundo, teal, acento amarillo moderado, tipografía legible, tarjetas coherentes, chips variables, iconografía consistente, light/dark, espaciado, sombras discretas y microinteracciones suaves con `prefers-reduced-motion`.

Layout desktop: navegación biblioteca/categorías a la izquierda, listado central y editor/preview lateral; adaptable a tamaños pequeños sin paneles ilegibles. Páginas/componentes: Biblioteca, Editor, Detalles/Usar, Resultados/Historial, Configuración y diálogo Mejorar con comparación diff. `Quick Launcher` como modal web por atajo **dentro del navegador** (por ejemplo Ctrl/Cmd+K), no como shortcut global de Windows. Para lanzar desde otras aplicaciones se requeriría extensión/nativa posterior, fuera del MVP.

Soporte keyboard-first, foco visible, contraste WCAG AA, labels accesibles, eventos loading/error/empty/success, prevent accidental delete, navegación coherente y pruebas en viewport móvil.

## Fase 5 — Pruebas integrales y aceptación

Antes de optimizar, conservar un baseline y dataset de pruebas reproducible. Ejecutar y mostrar evidencia real:

- Unitarias dominio: variables, render, duplicados, versiones, favoritos, búsqueda, permisos locales, serialización/import/export.
- Integración SQLite real: transacciones, migraciones, no regresión, backup/restore, idempotencia y datos corruptos.
- Contratos API/IA: JSON válido, propuesta no altera variables, fallback modelo fuera de servicio, timeout, cancel, prompt injection y no ejecución sin confirmación.
- E2E navegador: crear → editar → completar → copiar → mejorar/rechazar → mejorar/aceptar → ejecutar Qwen3 → guardar resultado → reiniciar → recuperar datos; importar/exportar/migrar. Al menos Edge y viewport móvil.
- Verificar accesibilidad, teclado y estados no ideales.
- Medir latencia p50/p95 en hardware real de Qwen3; no trasladar p95 del benchmark sintético de auditoría a esta interfaz ni afirmar performance sin ejecutar.
- Reutilizar `C:\Projects\Repos\LocalAI-Audit-Lab` solo si existe en equipo y sus tests/contratos son aplicables; no convertirlo en runtime dependency obligatoria.

Criterios `PASS_MVP_LOCAL`:
1. App web abre con `dotnet run` sobre localhost en navegador, sin necesitar MAUI.
2. El flujo primario de plantilla reutilizable funciona completo con Ollama APAGADO.
3. AI mejorar/autocompletar/consultar funciona con Qwen3 encendido y maneja errores apagado.
4. Migración/Import-export conserva datos y no sobrescribe el origen; backup/restore probado.
5. Ninguna mejora sustituye el original sin aceptar; versionado auditable.
6. Ninguna solicitud no autorizada se envía fuera del host; no hay Firestore/AdMob/telemetría.
7. Unit + integración + E2E + pruebas adversariales necesarias PASS real; controles de seguridad exigibles PASS.
8. UI responsiva y teclado, con contraste y errores controlados, validada.
9. Documentación reproducible: comandos, requisitos, estado por requisito, limitaciones y pruebas.

Un fallo de seguridad o pérdida de datos significa `FAIL_NO_GO` aunque build y tests unitarios pasen. Si no se pudo ejecutar, `BLOQUEADO`.

## Fase 6 — Entregables y forma de trabajo

Entregar en repo:
- `docs/BASELINE.md`, `docs/FEATURE_PARITY.md`, `docs/DESIGN_AUDIT.md`
- `docs/ARCHITECTURE.md`, `docs/ADR/0001-web-stack.md`, `docs/SECURITY.md`, `docs/DATA_MIGRATION.md`
- `docs/MVP_ACCEPTANCE.md` con comandos, tabla por requisito, pruebas ejecutadas, errores, métricas, evidencia y GO/NO-GO.
- Solución .NET web runnable, datos demo ficticios, migraciones, tests y README actualizado.

Trabaja por verticales completas: antes de iniciar una, deja la anterior funcional y cubierta. Audita → implementa → prueba → corrige → regresión. No repitas preguntas ya resueltas. Si falta un permiso/herramienta, documéntalo y sigue con lo ejecutable. No declares PASS por build aislado, endpoints vivos o capturas conceptuales.

## Comandos orientativos (comprobar SDK y plantilla antes)

```powershell
dotnet --list-sdks
ollama list
git status --short
dotnet new blazor --help
# Si net10.0 está disponible y el template lo soporta:
dotnet new blazor -n QuickPrompt.Web -o src/QuickPrompt.Web -f net10.0 -int Server
# Crear proyecto tests separado; usar solución nueva para web sin mezclar MAUI.
```

**Comienza ahora, inspecciona el repositorio original clonado, fija evidencia de línea base y ejecuta la primera vertical completa (biblioteca → plantilla → completar → copiar), luego integra Ollama.**
