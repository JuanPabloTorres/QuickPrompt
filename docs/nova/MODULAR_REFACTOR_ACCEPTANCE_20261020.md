# Refactorización modular — aceptación del incremento 2026-10-20

Estado: PARCIAL. La base de conexión/composición y la retirada comprobada de vistas
huérfanas funcionan; la sustitución del núcleo semántico y la refactorización
global solicitada no están terminadas. La versión no se cierra.

Alcance vigente: **versión web únicamente**, por respuesta del usuario a la propuesta
MAUI. [Decisión](WEB_SCOPE_DECISION_20261020.md). SDK/MAUI no son aceptación pendiente
de la meta web; los cambios nativos históricos siguen sin validar.

## Hallazgos observados y arquitectura seleccionada

Existían interfaces de dominio, herramientas reflejadas y verificación de efectos,
pero las conexiones HTTP, rutas y esquemas de biblioteca estaban repartidas entre
componentes. Infrastructure compilaba diez archivos enlazados desde el frontend
Blazor anterior. El candidato del agente acumulaba otra caché de conversación y
su contrato de autoría ofrecía valores que pertenecían a extracción.

Se extendieron propietarios existentes y se introdujeron únicamente puertos de
conexión con variación demostrable: transporte HTTP, biblioteca, ejecución y
preparación. Las fábricas backend agrupan la composición existente. Se rechazaron
una reescritura de React en clases, jerarquías sin variación real, repositorios
genéricos que oculten las reglas y otro árbitro semántico. El uso de reflection se
limita a métodos/proveedores permitidos y propiedades con atributos; no descubre
ni ejecuta código arbitrario enviado por el modelo.

El candidato de contenido se evaluó y rechazó: el modelo todavía mezcla órdenes
actuales, tarea futura y metadatos. Su autorrevisión no constituye una aceptación
independiente. Esa limitación se observa en los tres casos originales, no se deduce
del resultado de las pruebas deterministas.

## Contratos, propietarios y ciclo de vida

| Frontera | Implementación y contrato | Invariantes / errores |
|---|---|---|
| React → transporte | `createApiClient`, `ApiClient`, `HttpTransport` | Origen API local, cabeceras de protocolo/capacidad, AbortSignal, ApiError y respuesta validada; streaming sin consumir |
| React → biblioteca | `createLibraryServices`, PromptLibraryClient, TemplateClient, CollectionClient | Esquemas/rutas fuera de componentes; revisiones, confirmación y transacciones siguen en backend; no repositorio genérico |
| Biblioteca → hook | `useLibrary` consume sólo list | Sustitución aborta lectura previa e invalida su estado listo desde el render; respuestas anteriores no se publican |
| React → ejecución | `createLiveClient`, LiveClient y protocolo UiObservation/LiveUiAction | Capacidad explícita por run, ID/secuencia originales; no reintenta efectos ni recibe autoridad ejecutable del SSE |
| React → preparación | `createPreparationClient`, PreparationClient | Identidad/confirmación explícitas; streams intactos para sus verificadores; sin memoria duplicada |
| API → propietarios | ApplicationModules: biblioteca/agente/ejecución visible | Fábricas de DI preservan singletons, alias y políticas; autorización no vive en fábrica ni en UI |
| Persistencia/inferencia compartidas | Infrastructure/Local | Diez archivos movidos con iguales bytes; el frontend anterior referencia el ensamblado propietario |
| Núcleo candidato → inferencia | IAgentModelTransport y registro reflejado | Máximo tres consultas por fase; contenido/revisión comparten presupuesto; cancelación y fallos conservan originales |
| Tarea → contenido | TaskGoal.WORK_ORDER | Petición completa sin brief interpretado mezclado; hash de evaluación incluye cláusulas; ninguna evaluación concede aprobación |

`AgentSessions`, `TaskSourceResolver`, `AgentOrchestrator`, `SameTabUiDispatcher`,
`GoalCompletionValidator`, `LiveRuns` y los escritores actuales conservan estado,
revisiones, reglas y efectos. No se duplica su autoridad en los nuevos clientes.

## Implementación y retirada

- Backend: `ApplicationModules.cs`, `IAgentModelTransport.cs`, alias de transporte,
  diez servicios en `Infrastructure/Local`, referencias de proyecto corregidas.
- Frontend: servicios `api.ts`, `library.ts`, `live.ts` y `preparation.ts`, con
  consumidores de CRUD, editor/variables, versiones/compartir/grupos, contexto,
  revisión/commit y ejecución del agente conectados a sus respectivos puertos.
- Protocolo UI trasladado fuera del driver DOM; se conserva su reexportación de
  tipos para los consumidores existentes.
- `NovaAssist.tsx` y la vista `NovaCore.tsx` retirados tras inventario de referencias:
  cero consumidores productivos; NovaState permanece en preparación. Copia exacta,
  hashes y motivo en `retired-ui.json`. Se retiró un montaje de prueba exclusivo de
  la vista huérfana después de demostrar su PASS. No se retiró su API backend.
- Candidato reflejado trasladado a `Evaluation/Experiments`, fuera del grafo de
  producción. No está activado ni existe un fallback nuevo.
- `Check-ModuleBoundaries.cjs` amplía controles estáticos en cada incremento;
  los cinco iniciales se conservan como evidencia histórica. No acredita todas
  las dependencias dinámicas ni la fiabilidad del modelo.
- Descubrimiento de pruebas corregido para incluir tanto `.test.ts` como `.tsx`.

No hubo migración SQLite, cambio público de API, instalación de paquetes/modelos,
nube, commit, push ni reinicio de los servicios personales en 5190/5191. Su
comportamiento en ejecución no se atribuye a los builds sintéticos nuevos. Las
aplicaciones nativas/anteriores y extensión no se declararon huérfanas ni se borraron
sin paridad. Tampoco se retiraron validaciones de permisos por contener condiciones.

## Verificación inicial del primer incremento (histórica)

| Matriz / comprobación | Resultado | Evidencia y alcance |
|---|---|---|
| Backend final | PASS 779/779, 0 omitidas | `.playwright/core-replacement-20261020/tests/backend-final-content-owner.trx`; incluye contratos y TaskGoal, no inferencia real |
| Consumidores de servicios trasladados | PASS 151/151 | `tests/shared-service-consumers-serial.trx`; se conserva un intento anterior fallido por compilación concurrente |
| Frontend completo final | PASS 146/146, 0 omitidas | `frontend-retired-ui-final.json`; incluye los diez controles `.ts` de transporte/clientes antes omitidos por descubrimiento |
| Baseline antes de retirar vistas | PASS 147/147 | `frontend-agent-ports-discovery-final.json`; no se eliminó una prueba fallida |
| Build API final | PASS, 0 errores/advertencias | `final-modular/api`; grafo productivo sin Evaluation |
| Build React final | PASS | `final-retired-ui/web`; advertencia conservada de chunk principal >500 kB, sin afirmar mejora de latencia |
| Límites arquitectónicos | PASS 5/5 | `module-boundaries-final.json`; análisis estático, alcance explícito |
| UI CRUD/favoritos/tarjetas/filas/cancelación/settings/móvil | PASS 7 controles | `docs/evidence/workspace-1792501343232/validation.json`; Edge visible, datos sintéticos, sin modelo |
| UI actual del agente, abrir/llenar/compilar/destino/guardado manual/reapertura | PASS 7 controles | `docs/evidence/ui-surface-20261019/1792501943614/results.json`; comandos tipados, no interpreta texto libre; hashes de build y web registrados |
| Candidato semántico con Qwen real | FAIL 3/3 en revisión independiente | `content-owner-v2-preparation.json` y `content-owner-v2-review.json`; flujo Prepare real, sin UI; modelo/dataset/binario congelados |
| Puerta original de versión | NO_CERRADA, matriz válida | 34 CERRADO, 32 PARCIAL, 12 FALLA, 7 PENDIENTE y 3 SIN_VERIFICAR; 54 abiertos |

Los nombres abreviados de evidencia anteriores viven en
`docs/evidence/core-replacement-20261020` o, para TRX/resultados de Vitest/builds,
`.playwright/core-replacement-20261020`. La tanda concurrente frontend anterior
conserva 136 PASS y un timeout de 5 segundos. La ejecución final limita workers a
dos, conserva el mismo timeout y no cambia expectativas. Captura `filled.png`
inspeccionada: campo tema con energía solar y preview exacto, controles accesibles
y panel NOVA disponible en su esquina.

Los tres casos del candidato consumen 128093, 62069 y 94885 ms. El segundo devolvió
borrador técnicamente, pero perdió `<mensaje>` y copió un control de esta sesión:
se conserva como FAIL, no como éxito parcial de fidelidad. El tercero exige título
en el cuerpo y confunde delimitadores de cita con el valor. No se cualificó UI del
candidato que falla la puerta anterior. No se abrió holdout ni se afirmó fiabilidad
general a partir de contratos o comandos rápidos.

## Compatibilidad y reversión

Rutas, formatos públicos, namespaces de servicios locales y esquemas persistidos
se conservan. Las referencias de proyecto sustituyen los Compile enlazados; los
consumidores antiguos siguen compilando y probados. El cambio de composición
semántica productiva no se realizó. Para revertir este incremento deben restaurarse
sus archivos concretos y las referencias desde las copias/versiones apropiadas,
conservando el trabajo previo del usuario; no procede un reset global ni una
migración de datos. Las fuentes originales de experimentos y componentes retirados
quedan conservadas con hash en el área QA de este incremento.

## Pendientes y próxima frontera justificada

1. Sustitución funcional del núcleo semántico, con separación demostrada de
   petición actual, instrucción futura y metadatos. La hipótesis evaluada agotó sus
   dos variantes; no se repetirá sin causa nueva. El núcleo anterior sigue activo.
2. Retirar esa implementación anterior y sus dependencias sólo después de que el
   reemplazo pase los fallos conservados, los recorridos UI afectados y permisos.
3. Completar el inventario/paridad de aplicaciones anteriores y demás capacidades
   antes de afirmar una refactorización global de todo el repositorio.
4. Los 54 criterios abiertos de versión permanecen sin rebajar sus condiciones;
   una puerta estática o un CRUD funcional no cierra fidelidad del agente.

Ver [mapa modular](MODULAR_SYSTEM_MAP.md) y
[diagnóstico del candidato](AGENT_CORE_REPLACEMENT_20261020.md).

## Observación del informe

El informe estructurado local se preparó, pero el helper del observador rechazó
su registro con «No hay una consulta identificada». El relanzamiento con el ID
real de conversación confirmó que no había consulta observada a la que atribuirlo.
No se inventó un turno ni se registró en otro histórico. Las evidencias de pruebas
y este informe permanecen locales; la atribución al panel no está confirmada.

## Incremento posterior: archivos y proyección portátil

El contrato y la aceptación ejecutada están en
[ARCHIVE_PORTS_20261020.md](ARCHIVE_PORTS_20261020.md). API/Blazor usan la misma
fábrica y puertos de archivo/compartir; React elimina conexiones duplicadas en
Configuración/Ayuda. La restauración Blazor pasa por snapshot/respaldo previo.
789 backend, 151 compartidas, 151 frontend y 14 controles en navegador pasan.
El estado del goal global y la versión permanece abierto.

## Incremento posterior: proveedores, preferencias y conexión React

Contrato: [PROVIDER_PORTS_20261020.md](PROVIDER_PORTS_20261020.md).
791 backend y 159 frontend pasan; nueve checks UI reales incluyendo Qwen local y
borrador/editor. No quedan conexiones request/openApiResponse en componentes,
hooks ni App. La verificación estricta de respuesta literal sigue fallando; no se
promueve semántica ni se declara cierre global. Quedan consumidores anteriores.

## Incremento posterior: núcleo de consumidores server-rendered

[LOCAL_WORKSPACE_PORTS_20261020.md](LOCAL_WORKSPACE_PORTS_20261020.md) documenta los
puertos y límites. 155 compartidas, 23 backend del bloque y nueve checks UI reales
pasan. Compilación/borrado obsoletos no escriben. Persiste el alcance de organización,
Quick Assist, bridge/panel, auditoría MAUI y sustitución semántica. No hay cierre global.

## Incremento posterior: organización y retirada real

[ORGANIZATION_PORTS_20261020.md](ORGANIZATION_PORTS_20261020.md): puertos para grupos,
relaciones, perfiles/versiones y combinaciones; dos firmas de borrado desprotegido
retiradas con migración C# explícita. 160 compartidas, 23 backend del bloque, 13
checks UI y ocho controles estáticos pasan. Persisten Quick Assist, bridge/panel,
acoplamientos técnicos, revisión MAUI y sustitución semántica. Sin cierre global.

## Incremento posterior: runtime/panel e inversión de contratos

[RUNTIME_PORTS_20261020.md](RUNTIME_PORTS_20261020.md): componentes/panel sin almacén
concreto; puertos de workflow/transferencias/diagnóstico y contratos local/browser
propiedad de Application. 791 backend, 166 compartidas y seis checks UI reales pasan;
una petición Qwen se verificó sin generalizar su resultado. Revisión MAUI sin build
por entorno SDK8 y cero workloads; no declara conformidad. Restan semántica y auditoría
global. Los contratos C# de panel y archivo tienen migración explícita.

## Incremento posterior: ciclo MAUI

[NATIVE_LIFECYCLE_PORTS_20261020.md](NATIVE_LIFECYCLE_PORTS_20261020.md): interfaces
segregadas para arranque y restauración, mismo coordinador y singleton. Se retira
un miembro/constructor sin consumidores. Cinco comparaciones de fuente y diez
controles estáticos pasan; el primer comparador falló por asumir CRLF en un archivo
LF y se corrigió su lectura sin tocar producto. Build/UI nativos NOT RUN por falta
de SDK9/workloads. No se reejecutaron matrices web/modelo ajenas al incremento.
La conformidad global MAUI y semántica siguen pendientes, sin rebajar la matriz.

## Incremento posterior: transporte de modelo independiente

[MODEL_TRANSPORT_PORTS_20261020.md](MODEL_TRANSPORT_PORTS_20261020.md): el transporte
y caché dejan de ser propietarios dentro del intérprete. Puertos compartidos y
fábrica independientes; validadores conservan invalidación por respuesta. Evaluadores
usan el adaptador nuevo. 46 dirigidas y 794 backend PASS, cero omitidas; API/Blazor
sin warnings/errores, DLL compartidas idénticas. Once controles estáticos pasan.
La comprobación de fábrica se ejecuta aunque construir el intérprete esté prohibido.
No se repite UI/modelo ajenos al cambio ni se acredita fidelidad por estos resultados.
MAUI espera autorización de herramientas y validación; semántica/versión siguen abiertas.

## Incremento posterior: errores de almacenamiento

[STORAGE_FAILURE_BOUNDARY_20261020.md](STORAGE_FAILURE_BOUNDARY_20261020.md): puerto
pequeño para seis consumidores, adaptador SQLite en Infrastructure y composición
compartida. 168 compartidas y seis recorridos Edge/SQLite PASS; doce checks estáticos,
comparaciones de fuente y builds API/Blazor con DLL iguales. Se prueban abortos de
creación y recuperación de plantilla/versión, grupo e instrucción, no todo modo de
fallo ni todas las operaciones. El primer harness fallido se conserva.

El experimento independiente de recursos de autoría fue rechazado y retirado tras
un fallo de fidelidad de los tres encargos; no se compensa con estas pruebas de UI.
MAUI sigue sin herramientas autorizadas/build. Goal y versión permanecen abiertos.

## Incremento posterior: fábrica del host e inicio sin identidad fantasma

[HOST_COMPOSITION_20261020.md](HOST_COMPOSITION_20261020.md): arranque original
extraído con hook confiable previo a Build, sin dependencia productiva de QA.
794 backend y 164 frontend PASS; 15 checks HTTP/Edge prueban CLI normal, ausencia
de inferencia, sustitución del intérprete por endpoint/planificador y CRUD UI.
Trece controles estáticos y comparación de secuencia PASS. Un fallo real de inicio
503/recibo404 se corrige en startVisible sin retirar recuperación de respuestas
perdidas. Las tandas fallidas y dos timeouts frontend iniciales se conservan; con
dos workers pasan los mismos límites. No es precisión del modelo ni cierre.

API_CODE ahora apunta a QuickPromptHost; filas/criterios/estados canónicos intactos.
Restan adaptadores antiguos aún no compartidos, semántica no cualificada y MAUI
sin herramientas autorizadas/build. El objetivo global permanece activo.

## Incremento posterior: adaptador y metadatos con propietario único

[NOVA_INFERENCE_PORT_20261020.md](NOVA_INFERENCE_PORT_20261020.md): el adaptador
recibe el intérprete compartido y el cliente local; retira construcción privada,
constructor endpoint/timeout e IDisposable. Proyección y propuestas conservadas;
30 pruebas dirigidas finales PASS, cero omitidas. API/Blazor compilan sin warnings,
DLL compartidas iguales; Evaluation compila como dependencia de pruebas.

[CONTRACT_METADATA_SOURCE_20261020.md](CONTRACT_METADATA_SOURCE_20261020.md):
exportador aislado de política/enums, JSON validado y generación de las mismas 75
operaciones. Siete comprobaciones finales y quince controles estáticos PASS.
Fallo inicial de una lista LiveStart no migrada conservado; prueba de sustitución
ampliada cubre las cinco posiciones y el default, restaurando el artefacto original.

No son recorridos de Qwen ni validación global. Frontend conserva su evidencia previa;
MAUI y sustitución semántica siguen pendientes. No se reclasifica ningún requisito
canónico ni se cierra el objetivo general.

## Incremento posterior: selección UI independiente

[UI_DECISION_MODULE_20261020.md](UI_DECISION_MODULE_20261020.md): la fábrica resuelve
IUiDecisionModel sin construir PreparationInference ni ILocalLlmClient. El selector
reutiliza transporte, catálogo y contexto; no ejecuta ni autoriza efectos. Algoritmo
extraído conservado, puente compatible único y tamaño de contexto en su política.
Sólo se renombra la identidad del dueño en la guía y su hash de versión.

174 dirigidas recompiladas PASS, cero omitidas; seis variantes de HTTP real cubren
reselección con y sin puente antiguo. API/Blazor sin warnings/errores y DLL compartidas
idénticas a las probadas; 16 límites estáticos PASS. El primer intento no compiló
por dos fixtures; se conservan los fallos. La cohorte previa de 30 se vuelve a
validar dentro de las 174; su TRX anterior no se acepta como prueba de la fuente
que no compilaba. No se reejecutan frontend ni Qwen ajenos a esta separación.
Versión/goal siguen abiertos y los 88 criterios no cambian.

## Incremento posterior: selector de candidatos y retirada de ranking compatible

[CANDIDATE_SELECTION_MODULE_20261020.md](CANDIDATE_SELECTION_MODULE_20261020.md):
ICandidateSelector independiente del intérprete/editor; respuesta validada por lector
compartido, algoritmo/prompt/parsing conservados y resolución en TaskSourceResolver.
RankedCandidateCompatibility retirado; ausencia de capacidad falla explícitamente,
identidad seleccionada funciona y pertenencia publicada/inyectada prevalece al score.

625 dirigidas compiladas de 48 clases, cero omitidas; 18 variantes HTTP controladas
y seis pruebas nuevas de composición/autoridad/entradas. API/Blazor sin advertencias
y DLL compartidas idénticas a las probadas; Evaluation compilado con pruebas; 17
controles estáticos PASS. OpenAPI idéntico. La primera tanda 220/222 y una compilación
fallida posterior se conservan; dos fixtures publican ahora las coincidencias que
antes presuponían por score, sin debilitar assertions. No es aceptación semántica.
La migración compatible está documentada; goal/versión globales siguen abiertos.

## Incremento posterior: extracción de campos por puerto explícito

[INPUT_EXTRACTION_MODULE_20261020.md](INPUT_EXTRACTION_MODULE_20261020.md): proveedor
independiente sobre transporte; orquestador lo recibe sin exigir extracción en el
generador. Autoría/alcance compatibles delegan al mismo proveedor registrado. Método,
prompt, schema y observación conservados. Validación literal/permisos siguen en dominio.

582 dirigidas compiladas de 44 clases PASS, cero omitidas, incluidas cinco pruebas
nuevas. Fábrica con intérprete/editor prohibidos, puente prestado sin disposición,
completado/compilado con generador sin extracción y rechazo de valor inventado.
API/Blazor sin warnings/errores, DLL compartidas iguales a las probadas; Evaluation
compila con tests; 18 controles estáticos PASS. Advertencia inicial del fixture
conservada y corregida antes de congelar. OpenAPI idéntico. No es aceptación Qwen
ni UI nueva; migración de constructores documentada, goal/versión siguen abiertos.

## Incremento posterior: alcance de campos independiente

[INPUT_SCOPE_MODULE_20261020.md](INPUT_SCOPE_MODULE_20261020.md): proveedor independiente,
puerto explícito en planificador/resolvedor y proyecciones compartidas en SemanticContext.
Parcial viejo retirado; reglas, corrección acotada y propiedades JSON conservadas.
662 dirigidas compiladas de 53 clases y 19 controles estáticos PASS. Pruebas de
fuente recién ligada y cambio de revisión; API/Blazor sin warnings/errores y DLL
iguales a las probadas. Se conserva un timeout de prueba con duración anómala de
3:02:27.57; repetición aislada 1.27 s, causa no confirmada, sin aceptación de latencia.
La versión web sigue abierta por sus criterios; MAUI está fuera del alcance actual.

## Incremento posterior: revisión semántica con puerto único

[FIDELITY_REVIEW_MODULE_20261020.md](FIDELITY_REVIEW_MODULE_20261020.md): se mueve la
revisión completa a IDraftFidelityReviewer/DraftFidelityReviewer y se retira el parcial.
Preserva reglas, errores, corrección, transporte/cache y diagnóstico por llamada.
AgentStagePacket comparte el codificador reflejado; cambia consumer/version del
perfil opcional al nuevo dueño, no se presume mejora de significado.

666 dirigidas compiladas de 54 clases y 20 controles estáticos PASS. API/Blazor
sin advertencias y DLL compartidas iguales a las probadas; Evaluation compilado
con tests. Cuatro nuevos controles de fábrica/perfiles/cancelación/delegación.
Intento fallido de comparación por finales de línea e invocación sin filtro
rechazada por MSBuild conservados; no ejecutaron una tanda de aceptación falsa.
OpenAPI idéntico. No es precisión Qwen, autoría sigue FAIL en su matriz y versión
web permanece abierta. MAUI fuera de alcance por instrucción vigente.

## Incremento de generación independiente — 2026-10-20

IPromptGenerator se compone como PromptDraftGenerator sobre transporte validado,
editor, extractor y revisor prestados. PreparationInference conserva delegación y
schemas públicos como alias; su parcial Authoring se retira con archivo exacto.
El puerto conserva el método anterior y admite diagnóstico por llamada mediante
sobrecarga predeterminada; no exige modificar implementaciones existentes.
[Contrato y migración](DRAFT_GENERATION_MODULE_20261020.md).

676 pruebas dirigidas compiladas de 55 clases y 21 controles estáticos PASS.
Diez casos nuevos prueban fábrica con intérprete prohibido, perfiles de metadatos,
identidad/revisión, fallo/cancelación, rechazo, compatibilidad y concurrencia.
API/Blazor sin errores/advertencias y DLL compartidas iguales a las probadas;
Evaluation compila con tests. Cuerpos/instrucciones/schema preservados, una única
revisión y mismo presupuesto. OpenAPI y 88 criterios canónicos sin cambios.

Estos resultados controlados no acreditan precisión Qwen ni nuevos recorridos UI.
Autoría conserva FAIL; interpretación principal y auditoría web siguen pendientes.
Meta ACTIVA, versión NO_CERRADA con 34 cerradas/54 abiertas. MAUI fuera de alcance.

## Interpretación independiente — 2026-10-20

SemanticIntentInterpreter recibe transporte validado, selección prestada, política
 y conocimiento opcional. La fábrica comparte IIntentInterpreter,
ISemanticIntentInterpreter e ISemanticContentInterpreter sin construir generación,
edición ni campos. PreparationInference es fachada no parcial; seis archivos
anteriores retirados/archivados, algoritmos e instrucciones preservados al cambiar
propietario. [Contrato y migración](SEMANTIC_INTERPRETER_MODULE_20261020.md).

688 pruebas dirigidas compiladas de 56 clases y 22 controles estáticos PASS.
Seis nuevas de composición y doce recorridos HTTP de autoría por proveedor/fachada
validan límites y compatibilidad; respuestas controladas, no Qwen real. API/Blazor
sin errores/advertencias y DLL compartidas iguales a las probadas; Evaluation
compila con tests. OpenAPI y 88 criterios canónicos sin cambios.

La identidad/version del metadato opcional migra al nuevo propietario; no se
presume mejora lingüística ni se promueve el candidato rechazado. Autoría y
sustitución semántica siguen FAIL. Meta ACTIVA, versión NO_CERRADA con 34/54;
auditoría web y cualificación semántica pendientes. MAUI fuera del alcance actual.

## Ciclo de vida del cliente local — 2026-10-20

OllamaClient.CreateOwned centraliza construcción HTTP para API y Blazor y declara
propiedad mediante IDisposable. El constructor anterior conserva HTTP prestado;
no lo libera al disponer el adaptador. No se añade otro wrapper ni HttpClient
global. [Contrato y evidencia](LOCAL_CLIENT_LIFECYCLE_20261020.md).

Fallo reproducido en el registro API antes de corregir: su HttpClient seguía vivo
tras cerrar el contenedor. La misma prueba pasa después. 152 backend y 44 pruebas
compartidas dirigidas PASS, cero omitidas; seis casos de ciclo de vida, HTTP real
loopback y perfiles 8192/12288. API/Blazor sin advertencias/errores y DLL compartidas
iguales a ambos proyectos probados; Evaluation compila con backend. Veintitrés
controles estáticos PASS. Fallo inicial y compilación fallida del fixture conservados.

Métodos/instrucciones de inferencia y constructor prestado idénticos. No cambia
precisión del agente ni acepta latencia/UI; servicios personales sin reiniciar.
Estado canónico 34/54 intacto; meta web ACTIVA y versión abierta. MAUI excluido.

## Composición frontend conectada — 2026-10-20

createApplicationServices comparte ApiClient entre puertos y ServiceContext los
entrega a 18 consumidores. VisibleSessionClient posee capacidades por instancia,
con inicio/recuperación existentes y exports compatibles delegados. Sin otro
store de dominio ni nuevo árbitro. [Contrato](FRONTEND_COMPOSITION_20261020.md).

171 frontend y typecheck PASS; siete casos nuevos, 29 archivos, cero omitidas.
Veintidós controles Edge/HTTP PASS: quince de composición y siete workspace,
CRUD/revisiones/cancelación, interfaz persistente/settings/móvil. Cuerpos de
consumidores e inicio/observación preservados por comparación AST. Build web
conserva advertencia >500 kB; evaluador sin advertencias, DLL productivas iguales.
Veinticuatro controles estáticos PASS. Fallos de binding/fixture y perfil QA
conservados; expectativas no rebajadas. No nueva comprensión Qwen ni latencia.

Hot-swap activo y múltiples apps DOM en una ventana no cualificados; composición
estable por montaje. Servicios personales no reiniciados. Autoría/semántica y
resto de auditoría web pendientes; meta ACTIVA, versión 34/54 abierta. MAUI excluido.

## Política compartida de modelos — 2026-10-20

LocalModelPolicy pertenece a Domain; Application/API, telemetría, defaults y
exportador consultan al mismo propietario inmutable. Namespace público conservado
y tipos reenviados desde Infrastructure; exportador sin dependencia de proveedor.
Sin otro registro, modelo instalado, configuración ni permiso nuevo.
[Contrato y evidencia](MODEL_POLICY_OWNERSHIP_20261020.md).

550 backend y 112 compartidas PASS, cero omitidas; 17 casos nuevos. Cuatro controles
HTTP reales de admisión y siete del exportador PASS, contrato público idéntico.
API/Blazor sin advertencias/errores, DLL compartidas iguales a ambos proyectos
probados. Consumidor binario antiguo funciona sin recompilar; su fixture conserva
dos advertencias MSB3277. Veinticinco controles estáticos PASS. Fallo de expectativa
antigua del exportador conservado y migrado al nuevo límite.

No acepta comprensión Qwen ni latencia; matriz canónica 34/54 intacta, versión
abierta y meta ACTIVA. MAUI excluido; servicios personales sin reiniciar.

## Capacidades semánticas conectadas por puerto — 2026-10-20

La fábrica registra explicación, referencia, revisión de cláusulas y selección
contextual como alias del intérprete existente. AgentOrchestrator recibe explicación;
NovaTaskPlanner transmite los otros puertos al resolvedor. Se elimina exigir esas
capacidades al generador o proveedor UI/selector básico. Compatibilidad al construir;
sin nuevos propietarios, algoritmos, schema o permisos.
[Contrato y evidencia](SEMANTIC_CAPABILITY_WIRING_20261020.md).

Fallo reproducido: cuatro alias ausentes; mismo binario de 17 tests falla con cores
anteriores y pasa con los corregidos. Tanda final 388 backend/19 clases PASS, cero
omitidas y 19 casos nuevos, incluyendo propietario real/transporte controlado.
Quince controles Edge/HTTP de host normal/desactivado/sustituido PASS; perfil QA
migrado y error visible revisado. API/Blazor/evaluador sin advertencias ni errores;
DLL compartidas idénticas a las probadas. Veintiséis controles estáticos PASS.

Pruebas de conexión y efectos con respuestas controladas: no cualifican Qwen ni
latencia. Fallos originales y compilación inicial del fixture conservados. Matriz
canónica 34/54 intacta; versión abierta y meta ACTIVA, MAUI excluido.

## Fachada histórica retirada del runtime — 2026-10-20

PreparationInference ya no se compila en Infrastructure ni se registra en API. Sus
consumidores restantes son QA; archivo idéntico trasladado a Evaluation/Compatibility.
AgentStagePacket es un codec público puro compartido, sin duplicación ni friend
assemblies. Hashes QA apuntan a los propietarios reales y separan compatibilidad.
Cambio intencional del API .NET antiguo; puertos productivos y HTTP conservados.
[Contrato, migración y evidencia](LEGACY_INFERENCE_RETIREMENT_20261020.md).

457 backend en 46 clases PASS, cero omitidas; dos pruebas de retirada fallan antes
y pasan después. Quince controles Edge/HTTP de host normal/desactivado/sustituido
PASS. API/Blazor/evaluador sin advertencias/errores y DLL comunes iguales a las
probadas; artefactos productivos sin DLL/dependencia Evaluation. Cuatro contratos
exportados y OpenAPI idénticos; siete controles de metadata y 27 estáticos PASS.
Fallo inicial y guarda de inyección antigua conservados.

No cambia el algoritmo semántico ni cualifica Qwen/latencia. Versión y meta siguen
abiertas; matriz 34/54 intacta. MAUI excluido, servicios personales sin reiniciar.

Perfil técnico inmutable por composición implementado: 84 pruebas dirigidas, cuatro
regresiones corregidas, 15 controles Edge/HTTP y 28 estáticos PASS; tres builds sin
advertencias y defaults conservados. [Evidencia y límites](RUNTIME_SETTINGS_SNAPSHOT_20261020.md).
No cierra fidelidad Qwen/latencia ni requisitos canónicos; 34/54 intacto.

Ownership de propuestas UI implementado: tres fallos originales reproducidos,
180 frontend/31 archivos y 22 controles Edge/HTTP PASS; 29 controles estáticos.
Typecheck PASS, warning de bundle conservado, backend recompilado con hashes
idénticos. [Evidencia y límites](UI_PROPOSAL_SESSION_20261020.md). Versión abierta.

Despacho UI modular: 21 IDs y once primitivas conservadas por AST, cohorte original
39/39 antes/después y suite final 189/189 PASS. Quince controles built-host y smoke
headed normal/rápido PASS, 30 guardas estáticas; warning de bundle retenido.
[Provenance y límites](UI_COMMAND_REGISTRY_20261020.md). Backend reutilizado sin
repetir matriz ajena; no cambia cierre canónico ni acepta Qwen/latencia.

Presentación huérfana retirada: dos vistas/contrato muerto y seis reglas CSS;
esquema de propuestas conserva su forma en preparation. 51/51 módulos alcanzables,
189 frontend antes/después y siete recorridos headed Edge PASS, 30 controles
estáticos. [Evidencia y límites](WEB_RETIRED_PRESENTATION_20261020.md). Sin inferencia
ni cierre de requisitos; matriz canónica 34/54 intacta.

Propietario de borrado UI implementado: App compone interfaces estrechas y deja
identidad/revisión/confirmación en usePromptDeletion. Dos fallos originales
reproducidos y corregidos; nueve casos dirigidos, 192 frontend y siete controles
Edge PASS; 31 guardas estáticas. [Contrato y límites](PROMPT_DELETION_OWNER_20261020.md).
Sin atribuir fiabilidad del modelo ni cierre canónico (34/54 intacto).

Sesión modular de borradores implementada: interfaz, fábrica y clase privada por
montaje, ocho accesos migrados y snapshots con revisión/copia propia. El mismo
recorrido Edge falla en el build anterior y pasa cinco comprobaciones en el actual;
197 frontend y 32 guardas estáticas PASS. [Contrato y límites](EDITOR_DRAFT_SESSION_20261020.md).
No atribuye comprensión del modelo ni cierra versión; 34/54 intacto.

Preparación de contexto unificada: interfaz/fábrica/clase privada sobre inspect y
editorDraft. Ocho escenas conservan payload y resto de App; 209 frontend, 33 guardas
y cuatro controles Edge/API/SQLite en build anterior/candidato PASS. [Contrato y límites](EDITOR_CONTEXT_PREPARATION_20261020.md).
Backend reutilizado, signal opcional probado sin reclamar lifecycle global, sin
aceptación Qwen/latencia ni cierre canónico (34/54 intacto).

Caso de uso del editor separado del adaptador HTTP: interfaz/DI y propietarios
existentes para evaluar, guardar recibo y observar sesión. Baseline 88 y final 109
pruebas backend PASS; 21 comparaciones HTTP por build, cuatro recorridos del editor
y 15 de composición de host PASS. Tres builds sin advertencias, DLL comunes
verificadas y cuatro artefactos reflejados idénticos. [Contrato y límites](EDITOR_PREPARATION_CASE_20261020.md).
Frontend congelado reutilizado, sin campaña Qwen ni cierre canónico (34/54 intacto).

Ciclo de vida preparado segregado: completar y guardar fuera de HTTP, guardado
sin control visual/modelo y conflicto explícito para recibo cuyo contenido falta.
Fallo original real conservado; 164 backend, siete HTTP, dos UI con preparación
controlada y 15 de host PASS; tres builds sin advertencias, metadata idéntica y
35 guardas. [Contrato y límites](PREPARED_DRAFT_LIFECYCLE_20261020.md).
No se infiere planificación Qwen, latencia ni cierre canónico (34/54 intacto).
