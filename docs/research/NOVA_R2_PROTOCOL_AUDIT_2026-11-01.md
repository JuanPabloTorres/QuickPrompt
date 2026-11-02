# NOVA R2: auditoría del contrato de revisión

## Actualización ejecutada — 02/11/2026: primitiva nativa rechazada

El prototipo congelado `nova-native-tools-primitive-01` ya se ejecutó una vez.
Doce controles offline PASS y once hashes de fuentes/snapshots verificados antes
de inferir. Seis POST con el pin y perfil DMR existentes devolvieron seis llamadas
nativas válidas, cero retries y cero efectos. Esto sí comprueba esa capacidad
para estos paquetes; los antiguos `tool_calls` reconstruidos siguen siendo un
protocolo diferente. No prueba capacidad general ni significado correcto.

Replay con los DLL actuales, usando el puente UTF-8 con sólo ruta/hashes de
ensamblados actualizados: cuatro propuestas compuestas, una rechazada por perder
la entrada `<resumen>` y una por conservación numérica por campo del ensayo.
El código fuente `AuthoringEdits` sigue idéntico al propietario congelado.
Dos propuestas quedaron idénticas, dos acortaron sólo parcialmente y los dos
controles únicos fallaron: **0/6 contra los oráculos completos originales**.
La revisión es del agente, no aceptación humana independiente. Los parciales no
se convierten en completos y no se mezclan con la campaña principal 14/20.

El diseño nativo exigía aprobar QA6 antes de implementarse. Ese prerrequisito
falló, por lo que no se cambia Core, Companion, el editor activo ni el protocolo
público. La diferencia de canal nativo no es explicación suficiente de la
divergencia semántica; la causa interna continúa sin confirmar. No se infiere
incapacidad universal de Qwen ni se inicia otra cadena de prompts/retries.

Recibos: `.playwright/nova-native-tools-primitive-01/capture-01/` y
`.playwright/closure-r2-native-20261102/{RESULT.md,semantic-assessment.json,replay-result.json,replay-provenance.json}`.
La evidencia fechada inferior describe los estados de cada intento y se conserva.

Alcance: R2 de `docs/development/GO_TO_MARKET.md`. No es otro roadmap ni una aprobación de publicación. La campaña final exige 18/20, 9/10 por grupo y 80% por familia, con variables, restricciones y aprobaciones conservadas. Los diez casos nuevos congelados no se han utilizado para desarrollo.

## Responsabilidades observadas

| Propietario | Método/contrato | Qué puede demostrar |
| --- | --- | --- |
| Aplicación | `AuthoringBriefs.Confirm` y revisión final | Confirmación humana del contenido/revisión exactos; no permiso de guardar o ejecutar |
| Autoría publicada pendiente | Crear con contrato establecido | El cambio local conserva contenido literal y no llama al escritor; falta verificar la versión publicada |
| `PromptDraftGenerator` | Pipeline activo de adaptación | Todavía usa discovery y definición anteriores; el candidato nuevo no está integrado |
| `AuthoringEdits` candidato | Snapshot/hash, IDs, variables tipadas, composición transaccional | Direcciones válidas, límites, metadatos/referencias y variables conservadas. No demuestra equivalencia semántica |
| `AuthoringRevisionEditor` candidato | Decisión + clasificación/rewrite | Propuestas sin efectos. Su calidad semántica todavía no aprueba |
| `SemanticCandidateSelector` | Pertenencia al catálogo completo | Una llamada, mapa exacto de candidatos autorizados y ambigüedad preservada; 50 pruebas controladas, campaña real final pendiente |
| Core + DMR | Validación estructural y transporte | Pin/modelo, esquema, tokens, finalización completa, cancelación y presupuesto; no fidelidad semántica |

## Valores y autoridad

| Parámetro | Valor observado | Decisión |
| --- | --- | --- |
| Proveedor/modelo | Docker Model Runner / qwen3:8b, SHA256 977d1540140cd0d5437d2e22ffc6853714bef103bda39754edd0f2d9e2aabb82 | Conservar |
| Contexto | 12288 | Conservar; verificar paquete completo, nunca truncar |
| Tiempo de autoría | 90 s compartidos por el caller | Conservar; no reiniciar por etapa |
| Solicitudes | 2 como máximo, una por etapa, cero retries | Conservar |
| Salida | 1400 + 3500 | Límite, no prueba de calidad ni evidencia de necesidad de aumentarlo |
| Temperatura | 0 | Sin evidencia para cambiarla: contraste previo documentado de muestreo no mejoró las cuatro tareas |
| Decoding | native en candidato | Contraste prompted rechazado: recuperó reducciones pero eliminó requisitos únicos |
| Pensamiento | false; reasoning-budget 0, parallel 1, keep-alive 5m | Contraste real posterior de tres casos con razonamiento: 1/3, rechazado; perfil original restaurado, sin cambio de configuración del producto |
| Variables | Nombres elegidos y tokens canónicos, separados de valores del caso | Host controla allowedInputs. Existentes deben conservarse; elegidas ausentes pueden incorporarse |
| Preferencias | Language/Format aprobados y restricciones de contenido | No son proveedor, credenciales, permiso de editar metadatos ni autorización de efectos |

El transporte exige autorización local explícita para solicitar pensamiento. Cambiar sólo el flag del mensaje no basta y no autoriza ampliar recursos. Una configuración compatible no demuestra una mejora; no se atribuye el fallo a la capacidad de Qwen.

## Primera divergencia y límites de las inferencias

1. El escritor anterior reconstruye todos los campos y puede copiar sin cambiar o perder contenido. El candidato reduce el espacio de escritura mediante operaciones tipadas; eso mejora controles, no certifica semántica.
2. En operator02 se capturaron 12/12 respuestas y se redujeron 8/8 tareas, pero desaparecieron dos condiciones únicas y una traducción amplió el alcance temporal. Rechazado.
3. En operator03 el comparador recibió también el documento completo, incluida la unidad que pretendía evaluar. Eso viola el diseño de una comparación independiente. Retirarlo conservó cuatro contrastes, pero redujo utilidad a 5/8 en operator04. Es evidencia de sensibilidad al montaje; no demuestra la causa interna de la decisión del modelo.
4. Restaurar los nombres asociados a tokens en operator05 no recuperó las cuatro adaptaciones conocidas. La hipótesis de que faltaba únicamente ese mapa queda refutada como explicación suficiente.
5. La clasificación de papeles de unidad de operator06 obtuvo 7/8 adaptaciones y 4/4 contrastes; es una capacidad acotada, no la aceptación R2. Aclarar el contrato de resultados en operator07 no mantuvo la utilidad y mostró un fallo adicional: con idioma inglés aprobado el plan eligió COMPRESS y dejó el texto en español.
6. `revisionKind` es una selección de algoritmo, no una acción del usuario. Hacer competir COMPRESS/REWRITE permite ignorar una combinación legítima de objetivos. Un algoritmo que sólo quita texto no tiene la capacidad de traducir; las restricciones explícitas deben limitar los algoritmos elegibles.
7. Un ID, hash, etiqueta o testigo existente no demuestra que todo el contenido de la unidad sea redundante. La evidencia de diferencia semántica debe ser un artefacto separado y revisable; no se usará un indicador booleano como certificado.

## Corrección del método antes de integrar

- Mantener un único candidato y contrato congelados por contraste; no modificar expectativas tras ver respuestas ni seleccionar el mejor intento por caso.
- Separar condiciones de aplicación conocidas, datos de plantilla, petición de edición, propuesta y autorización.
- Tratar objetivos combinados como restricciones simultáneas. Elegir sólo herramientas capaces de cumplir todas las restricciones explícitas del host, sin detección por palabras.
- Para una omisión propuesta, describir de forma breve el requisito del texto candidato y el que quedará disponible, y señalar un requisito adicional con cita literal o un testigo retenido. Las descripciones son artefactos públicos del análisis, no permisos ni pruebas semánticas automáticas.
- El código valida direcciones, citas, límites, variables y atomicidad. La evaluación independiente valida significado y utilidad. La persona confirma contenido y los efectos siguen requiriendo su autorización propia.
- No integrar hasta ejecutar contratos deterministas, contrastes positivos/negativos y la campaña completa en el mismo candidato. El código de otras áreas permanece fuera de este carril.

## Evidencia

Los paquetes y resultados se conservan en `.playwright/priority-closure-20261101/`, subdirectorios `nova-revision-operator-01` a `07`, `nova-decoding-contrast-01`, `full-catalog-selector` y el baseline histórico `mvp20-01`. `CAPTURED` o un test de captura aprobado nunca significa PASS semántico.

AI Delivery Lab se abrió para este proyecto, pero la vista SIN CAPTURA no acredita instrumentación del producto. Los archivos de evidencia son la fuente actual; observador y recibos no certifican por sí mismos el resultado.

## Herramientas y candidato de alternativas literales

La auditoría del decisor UI encontró una divergencia verificable: los mensajes
incluían nombre, permiso y plazo, pero no descripción ni esquema de argumentos.
El esquema nativo de salida no sustituye el manual en los mensajes. Se completó
la proyección desde el mismo catálogo elegible, conservando permisos, preflight
de 32000 bytes y ejecutor. Contratos: rojo 3/7, verde 7/7. Contraste Qwen real:
6/6 propuestas correctas, seis llamadas internas, sin reintentos ni efectos.
Estados sintéticos y conjuntos de una o dos herramientas: no acredita el recorrido
React, una biblioteca privada ni el cierre comercial. Guía propietaria:
[herramientas de NOVA](../development/NOVA_TOOLS.md).

El nuevo editor entrega alternativas concretas de subsecuencias de cada campo,
calculadas por el host. Qwen elige una alternativa por campo; una segunda etapa
puede traducir o reformular sobre el hash recompuesto. El catálogo completo cuenta
en el presupuesto de 26000 bytes. Si no cabe, queda totalmente no disponible antes
de inferir y se conserva la fuente completa para transformación; nunca se ofrece
un menú parcial ni se cambia de algoritmo después de un fallo. Las alternativas
son propuestas sin certificación semántica ni aprobación.

El primer contraste obtuvo 15/17: las ocho adaptaciones se redujeron, pero una
omisión perdió «exactamente cinco secciones» y una traducción amplió una operación
concreta. Rechazado, sin integrar. La revisión siguiente añade conservación léxica
de cantidades por campo en compresión y una herramienta de traducción con contrato
propio. El repertorio numérico declarado ES/EN es conservador y limitado; conjuntos
iguales no prueban que una cantidad siga ligada a la misma obligación. Una
reformulación autorizada puede proponer cambios numéricos para revisión humana.

La generación del catálogo poda unidades que son únicas propietarias de variables
o cantidades antes de enumerar. Esto evita agotar el plazo explorando combinaciones
que el contrato rechazaría, sin suprimir alternativas admisibles. Las condiciones,
operaciones, destinatarios y ámbitos siguen requiriendo evaluación semántica. El
nombre TRANSLATE no certifica que la selección sea correcta ni que toda traducción
recorra esa rama; se evalúan el wire y el contenido final.

Evidencia nueva: `ui-capability-packet`, `ui-capability-real-01`,
`nova-revision-operator-08`, `nova-revision-operator-09`,
`nova-thinking-profile-smoke-01`, `nova-preview-operator-01` y
`nova-preview-operator-02`. Los siete contrastes iniciales y la campaña de 21
de desarrollo del segundo candidato están congelados por separado de la campaña
final de 20. El resultado técnico de captura no sustituye su evaluación.

La revisión numérica/referencias pasó 118 controles deterministas. El smoke del
segundo candidato obtuvo 6/7 semánticos y se detuvo: cantidades y firma conservadas,
pero TRANSLATE volvió a producir `finalizing` para una operación de datación.
La primera divergencia está en el texto RAW de la segunda llamada; ni el decoder
ni el dominio introdujeron esa sustitución. No se ejecutaron los 21 completos ni
se integró el escritor. `nova-preview-operator-02/smoke/ASSESSMENT.json` conserva
cada resultado, el wire y los hashes.

Dos contrastes posteriores separan hipótesis de evidencia:

- `nova-translation-alignment-probe-01`: una llamada, diez frases, 7/10; también
  fallan frases atómicas sin contexto. No queda confirmada la capacidad necesaria
  para proteger operaciones mediante glosas del mismo modelo.
- `nova-translation-language-pair-01`: diez originales con idioma fuente explícito
  y dos controles portugueses, 9/12. Las diez respuestas emparejadas permanecen
  idénticas. El idioma fuente es una precondición útil del contrato, pero añadirlo
  no resuelve el fallo observado y no se integra como corrección suficiente.

Se evalúa grounding mediante datos léxicos generales, sin otro proveedor de IA.
El corpus TEI completo de WikDict 2025.11.21 fue rechazado antes de integrar:
22.552 entradas, sin los headwords necesarios `fechar`, `firmar`, `sellar`,
`archivar` ni `finalizar`; no contiene formas flexionadas. Su cabecera declara
CC BY-SA 3.0, mientras la página actual enlaza 4.0. No se cambia la licencia del
archivo para hacerla coincidir con la web. Hash, cobertura, parseo offline y
descarga parcial fallida se conservan en `lexical-resource-audit-01`. La existencia
de una fuente pública no acredita su cobertura, ni autoriza recuperar URLs del
texto humano desde las interfaces de referencias existentes.

## Grounding y composición: contrastes posteriores

El corpus completo inverso EN→ES de FreeDict+WikDict, edición 2025.11.23, sí
superó la auditoría de cobertura necesaria: 64.258 entradas, 133.073 aristas y
los ocho verbos del contraste. El archivo se verificó con el SHA512 publicado;
se conserva completo, comprimido de forma determinista y con su aviso de licencia
CC BY-SA 3.0. No es un modelo ni un servicio de red. Su carga local retuvo unos
70,5 MiB administrados; pico no medido. El aviso acompaña salida y publicación.

La primera conexión del recurso al candidato produjo cinco propuestas y dos
rechazos antes de la segunda llamada: el modelo incluyó variables, duplicados
y oraciones en consultas que requerían palabras literales. El contrato libre de
consultas se retiró. La extracción pertenece ahora al host: todos los segmentos
literales del preview, variables excluidas, NFC y minúsculas sólo para consultar,
sin cambiar el contenido humano. Se agrupa por par de idiomas y no se escoge un
primer sentido. Contratos combinados: 165/165. Artefactos:
`nova-translation-grounded-smoke-01` y `nova-translation-host-extraction-contract-01`.

La proyección compacta conserva todos los sentidos recuperados, definiciones,
headwords, categorías, índices y multiplicidad de las aristas consultadas. Omite
únicamente sinónimos españoles no consultados del mismo sentido. El formato
completo sigue disponible para auditoría; la reconstrucción de cada coincidencia
se contrasta contra él. La fuente completa no se recorta para caber.

El siguiente smoke alcanzó 7/7 capturas técnicas, pero 6/7 semánticas. Una
auditoría independiente de 36 controles confirmó fuente, idiomas, variables,
acepciones y direcciones completas, con máximo 22.624 bytes. `fechar` recibió
`date` y `slate` con sus definiciones; la respuesta RAW volvió a usar `finalizing`.
El resultado refuta que disponer del recurso baste para utilizarlo correctamente.
No se ejecutó qualification24 ni la campaña final. Evidencia:
`nova-translation-host-extraction-smoke-01/ASSESSMENT.json` y `GROUNDING_AUDIT.json`.

Se prepara un reemplazo de TRANSLATE mediante composición de gaps y slots en
orden fuente, con elecciones de acepción identificadas y composición del host.
La elegibilidad léxica requiere coincidencias no vacías y categorías inglesas
exclusivamente verbales; no demuestra su función gramatical española en contexto
ni que el diccionario sea exhaustivo. El catálogo conserva también candidatos
inadecuados para el caso, como `slate` o `cosign`; corresponde al modelo elegir y
a la evaluación independiente rechazar una elección equivocada.

Los slots no prueban agentes, objetos, condiciones o alcance. Los gaps siguen
generativos. KEEP_SOURCE puede conservar un nombre literal; dejar sin traducir
un verbo ordinario no aprueba una petición inglesa. Los nuevos controles de
qualification28 incluyen nombre citado, actor explícito, dos objetos y sello de
recepción, junto con los 24 anteriores intactos. Las pruebas controladas del
compositor no sustituyen su ejecución con Qwen ni habilitan publicación.

El compositor de gaps obtuvo 216/216 controles de contrato y siete capturas,
pero sólo **5/7 semánticos**. En ambos casos de traducción eligió correctamente
`date`/`sign`. La primera divergencia observada aparece en el gap RAW: redactó
la condición completa en vez del fragmento. El host añadió después el verbo y
el resto, duplicando operación y continuación. En el caso de datación también
dejó la tarea en español. Se rechazó el mecanismo y se conservaron sus fuentes,
wires y evaluación en `nova-translation-composition-smoke-01`. Qualification28
y campaña final siguen sin ejecutarse.

El reemplazo conserva campos completos. Cada ocurrencia léxica elegible recibe
un marcador inmutable; la respuesta propone acepción, eco exacto de la palabra,
mayúsculas y traducción completa de cada campo. El host verifica todas las
correspondencias y la secuencia de marcadores/entradas antes de resolverlos.
Así se elimina el ensamblaje de fragmentos generados independientemente. Todavía
no demuestra gramática, significado ni conservación del actor. Los 236 controles
combinados pasan; su smoke reutiliza los mismos siete casos y el mismo oráculo
sin modificaciones. Recibo: `nova-translation-flat-contract-01/CONTRACT_GATE.json`.

El smoke FLAT produjo siete propuestas y **7/7 significados conservados** según
el mismo oráculo independiente. Las operaciones de datación y firma ya no se
sustituyen ni duplican. La auditoría reproduce 36 controles del corpus y 76 de
composición; máximo material observado: 24.861 bytes, diez llamadas totales.
Persisten dos reservas gramaticales: `before date` y `before sign`. Están en el
texto RAW con marcador y el host inserta correctamente la forma base; no son un
fallo de sustitución posterior. El criterio original evalúa significado y uso
comprensible, no fluidez nativa ni coincidencia exacta. Se registran como defectos
gramaticales y no se convierte este resultado en aprobación comercial. La
calificación de 28 y la campaña final siguen pendientes. Evidencia:
`nova-translation-flat-smoke-01`.

Qualification28 se ejecutó una vez y **falló: 18/28**. Cinco respuestas se
rechazaron por omitir marcadores; el caso del sello también perdió su entrada.
Otros cinco fallos semánticos fueron una adaptación sin acortar, pérdida de la
continuación única, dos compresiones incompletas y traducción de un nombre
literal. Los cuatro conocidos pasan y los ocho iniciales alcanzan 7/8, pero los
controles obligatorios no pasan. No se integra el candidato ni se ejecutan los
20 finales. Se conservan 38 llamadas, cero reintentos, máximo 25.311 bytes y
37.921 ms por caso en `nova-translation-qualification28-01/capture-01`.

La inspección del adaptador real de Adaptive-AI-Core confirma que elimina
`pattern` de la máscara nativa; el validador final conserva el esquema completo.
Por ello añadir un patrón no impediría omitir marcadores durante la generación.
Los esquemas de posiciones obligatorias con `const`/`enum` se investigan como
reemplazo estructural. Las etiquetas falsas de idioma y nombre ocurren antes
del lookup: no se atribuyen al corpus ni al agrupamiento del host. El catálogo
de compresión sí contenía todas las alternativas necesarias; su ausencia o
truncamiento no explica las decisiones incorrectas observadas.

Se preparan diagnósticos aislados de auditoría de fuente/elección de capacidad,
ablación de familia de herramientas y realización con posiciones obligatorias.
Usan los mismos datos sintéticos, oráculos separados y límites originales.
La compilación del arnés nuevo sólo verifica compilación (prueba real omitida
sin opt-in); no acredita capacidades ni constituye una nueva aprobación R2.

### Corte posterior: mecanismos rechazados y validaciones pendientes

Las posiciones obligatorias produjeron seis respuestas nativas válidas, pero
**0/6 resultados semánticos aprobados**. Cuatro no compilaron por duplicación de
entradas; dos compilaron con duplicación de operaciones y gramática incorrecta.
No se integra este mecanismo. Evidencia y oráculos originales:
`.playwright/nova-translation-required-positions-primitive-01/ASSESSMENT.json`.

La auditoría de fuente separada obtuvo **3/6 casos completos**. La ablación de
herramientas de traducción durante compresión cambió de **3/6 a 4/6**, con una
regresión de la continuación única. No demuestra una reparación general ni
justifica seleccionar retrospectivamente el mejor resultado de cada brazo.
Recibos: `nova-source-audit-chooser-primitive-01/ASSESSMENT.json` y
`nova-compress-family-ablation6-01/ASSESSMENT.json`.

La herramienta real Apertium, aislada en Docker y sin otro modelo neuronal,
obtuvo **3/10 por perfil**. Conservó variables y permitió proteger literales,
pero mantuvo errores léxicos y gramaticales. La posedición de campos completos
está preparada con diez casos, 21 controles offline y paquetes de hasta 24.100
bytes; aún no tiene ejecución Qwen. Tampoco hay aceptación semántica del nuevo
ledger de compresión: sus controles de host no prueban la extracción de hechos
por el modelo. Ledger QA2 congela seis casos a 1.400 tokens, sin reintentos ni
ampliación de presupuesto. El diagnóstico de evidencia de idioma congela ocho
pares con los 14 resultados reales de NTextCat, sin convertir top1 en autoridad.
Las tres primitivas siguen pendientes de inferencia y evaluación independiente.

### Incidente del runtime compartido

Una interrupción dejó la campaña Ledger V1 con un caso fallido capturado y cinco
sin evaluación; no se considera una ejecución completa ni se reanuda
selectivamente. El usuario autorizó actualizar Docker con respaldo local. El
respaldo verificó 14 archivos, 155.793.617.251 bytes; el instalador oficial firmado
y con checksum comprobado terminó con código 0. Docker Desktop 4.97.0.244584 y
Engine 29.9.2 responden. Los 12 archivos del almacén de modelos y el perfil Qwen
son idénticos a sus hashes previos. No hubo factory reset, pull ni eliminación de
modelos, imágenes, volúmenes o datos.

Al corte del recibo de las 22:44:10 UTC, Model Runner continúa devolviendo HTTP
502. El socket nuevo `dockerInference` rechaza conexión con error nativo 10022 y
consulta de metadatos con 1920; un control AF_UNIX independiente sí completó
bind/listen/connect. Esto acota una divergencia de comunicación con Docker,
sin demostrar todavía su causa interna. El usuario también observa un error
en Models. No se atribuye este bloqueo a Qwen ni se cambia de proveedor para
ocultarlo. Evidencia segura: `nova-docker-runtime-incident-01/STATUS.json` y
`model-preservation-after-upgrade.json`. El respaldo completo permanece local.

El control posterior de socket propio bajo el mismo directorio `Docker/run`
tampoco completó bind/listen/connect: devolvió 10022 y su limpieza exacta falló
con 1920. No se registró la subetapa que falló, por lo que no se deduce cuál fue.
El directorio padre es normal, sin enlace observado. Se conserva el socket de
prueba inaccesible; no se fuerza su borrado. Esta diferencia frente al control
fuera de Docker impide atribuir el fallo exclusivamente al proceso de Docker.
Recibo: `same-parent-socket-control.json`. Después de una parada controlada con
código 0 y cero procesos de Desktop, se solicita inicio manual desde Windows
para contrastar contexto de arranque; su resultado sigue pendiente.

El usuario inició Desktop manualmente desde Windows y observó la lista de
modelos. En la comprobación posterior, DMR y llama.cpp `b9879-cuda` respondieron;
el digest de Qwen permaneció igual. La configuración efectiva estaba vacía y se
restauró exclusivamente el perfil anterior: contexto 12.288, completion,
`--parallel 1`, keep_alive 5m0s y reasoning-budget 0. La API confirmó esos valores.
Una llamada real a través del mismo adaptador .NET/Core devolvió el marcador
exacto y suma 12, con 7.456 ms y sin reintentos. Recibos:
`nova-docker-runtime-incident-01/configuration-restored.json`,
`PROVIDER_READY.json` y `core-preflight-01/capture-01/DMR-CORE-PREFLIGHT.json`.
Esto acredita recuperación operativa en ese corte, no identifica por sí solo la
causa del fallo anterior ni demuestra estabilidad universal o calidad NOVA.
Se entrega al propietario de compresión el uso exclusivo del modelo para los
seis QA2; no se solapan las inferencias de otras primitivas.

### Contrastes completos posteriores a recuperar DMR

Ledger QA2 obtuvo 6/6 respuestas y compilaciones, pero sólo **1/6 extracciones
completas** y 4/6 resultados literales correctos. La comparación estructural no
detecta significado omitido antes de construir los hechos: se perdieron la
obligación/orden de preguntar, justificar patrones y la finalidad del informe;
se añadieron dependencias entre paráfrasis. No se integra como fundamento de
conservación. Recibo independiente: `nova-source-obligation-ledger-primitive-02/
capture-01/ASSESSMENT.json`, SHA256 `60C7B43F451E0A5AC872E2FCE1095F5C9E90D9D5E72372135068115CA9A581FC`.

La posedición de diez campos completos obtuvo **7/10 significados conservados**,
9/10 propuestas compiladas y cero reintentos. Cambió un imperativo a «It revises»,
un nombre fijado a «Sign» y la operación mostrar a «Sample». El cambio de nombre
fue rechazado por el host. La herramienta corrigió condiciones de datación/firma,
actor explícito, dos objetos, sello y textos PT/mixtos; esos positivos no borran
los tres fallos. Recibo: `nova-apertium-postedit-primitive-01/capture-01/ASSESSMENT.json`,
SHA256 `4D066D19F1F9667255C1E0A7E5E0FB86F988F0C79F6A508611CFBAC31445DB12`.

El diagnóstico SourceEvidence8 completó 16 respuestas válidas. La evidencia
genuina de NTextCat permitió identificar los ocho mapas en sentido lingüístico,
pero generó códigos ISO3 donde el contrato esperaba sus identificadores de base.
No se reinterpretaron como PASS. Un contraste posterior con códigos canónicos y
enum previo al prompt consiguió **6/8 mapas, 18/22 campos**, con dos confusiones
de idioma reales. NAME sigue 0/1 y la reducción solicitada 7/8. No hay fallo
demostrado de native decoding; todos los JSON cumplen sus esquemas. Recibos:
`nova-source-language-evidence-primitive-01/ASSESSMENT.json` y
`nova-canonical-language-protocol-primitive-01/ASSESSMENT.json`.

La herramienta léxica consultaba formas flexionadas sin sus lemas: «Muestra»
recuperaba sentidos nominales; «Revisa» no tenía aristas. La captura morfológica
real conserva todas las alternativas (incluidos indicativo e imperativo), sin
seleccionar categoría por el evaluador: 79 consultas y 164 análisis. Amplía
cobertura ES de 39/56 a 54/56 formas. No traduce por sí sola ni resuelve actor,
NAME o acepción; Resume/Resuma ES siguen desconocidos. Volcar todos los lemas y
definiciones supera 26.000 bytes en cuatro de diez casos. Evidencia:
`nova-apertium-morphology-qa-01/RECEIPT.json` y `PER_CASE_MEASUREMENTS.json`.

El siguiente contraste, todavía no ejecutado, sustituye ese volcado por consultas
por IDs de palabras reales y una segunda posedición dependiente de la evidencia
solicitada. Los literales de sólo lectura se ocultan en el mismo codec numérico
privado y se restauran en el host. Dos llamadas 1400+3500 y herramientas comparten
90 segundos; una selección que exceda el material completo permitido no produce
segunda llamada. El arnés compiló con un test opt-in omitido y tiene revisión
estática independiente; esto no acredita calidad del candidato. Idiomas y política
de literales son entradas explícitas de esta primitiva, no una solución ya probada
del planner, NAME o solicitudes compuestas. Todos los oráculos y fallos anteriores
permanecen intactos. No hay nueva ejecución final de veinte ni aprobación R2.

La primitiva dependiente WordID completó diez casos y **17 llamadas**, sin
reintentos: siete propuestas conservaron significado; tres selecciones excedieron
el presupuesto y no recibieron segunda inferencia (45.022, 50.636 y 32.814 bytes).
El resultado total sigue **7/10**. La evaluación verificó las consultas reales,
todos los análisis/sentidos enviados y la restauración privada del nombre literal;
no convierte esos positivos en prueba de planner ni fuente autoidentificada.
Recibo: `nova-apertium-query-postedit-qa-01/capture-01/ASSESSMENT.json`, SHA256
`5482FC272A465DDC039867BC0046A08764AC169AEB6E714203D166F814F49561`.

Los tres rechazos activan el refactor de la capacidad de recursos: el contrato
permitía solicitar evidencia que la herramienta no podía entregar dentro del
límite, aun mostrando costes al modelo. El siguiente diseño admite sólo un número
de slots certificado por una cota de bytes derivada de todos los datos disponibles,
sin elegir palabras por el evaluador ni recortar sentidos después de una consulta.
La garantía será de tamaño, nunca de fidelidad semántica. Se mantiene el límite
26.000, y los resultados anteriores no se reparan ni se reclasifican.

La nueva capacidad ResourceSlots completó **10/10 resultados semánticos y diez
compilaciones**, con 20 llamadas, cero reintentos y cero excesos de presupuesto.
Máximo material observado: 18.652 bytes; máximo caso: 33.890 ms. La revisión
independiente comprobó la cadena de slots reales, unión de evidencia, certificado,
paquete y replay; Tool2 mantiene bytes idénticos al mecanismo anterior para la
misma selección única. Recibo: `nova-apertium-resource-slots-qa-01/capture-01/ASSESSMENT.json`,
SHA256 `361B719541236D3A0AA8B36E70BD898F4386BAF9922D41E215B00505A8BB8585`.

La cota paga cada registro/propiedad JSON en UTF-8 y sus separadores. Para cualquier
selección admisible de hasta K palabras, el tamaño no supera la base más el mínimo
entre el peso total de átomos disponibles y las K mayores cargas de consulta.
Ese orden sólo calcula peor coste; no filtra palabras ni sentidos. Es el máximo
certificado por esa cota, no un óptimo global. Los diez casos eligieron siempre
los primeros K IDs: el éxito no demuestra selección contextual de consultas.
Fuente/idiomas y política literal siguen fijados por fixture. No acredita planner,
solicitud compuesta, integración, memoria Cloud ni campaña final de veinte.

La compresión se contrasta aparte mediante propuesta inicial, observación literal
del host y edición final dependiente, siempre dentro de dos llamadas. La auditoría
del writer directo encontró ocho respuestas reales que ya recibían el encargo y
aun así copiaron la tarea completa; no se repite ese mecanismo como supuesto
reemplazo nuevo. La revisión dependiente añade estado observado, sin tratar diff
o menor longitud como prueba de equivalencia. Su disponibilidad de contexto es
condicional: si la fuente más propuesta y observación superan el límite, se
preserva el fallo y no se emite la segunda llamada. El experimento ejecutó siete
llamadas, sin reintentos: cinco casos fallaron en el puente QA antes de la segunda
inferencia y un caso completó ambas. Esos cinco no tienen resultado semántico
final evaluable. En C-MULTI-TASK la primera respuesta reemplazó «No inventes
datos» con la tarea redundante; la segunda repitió exactamente esa respuesta.
La fuente correcta consta en ambos paquetes y la observación del host detectó
el cambio y el aumento de longitud. La primera divergencia semántica observada
es la respuesta del modelo; la causa interna aún no está demostrada. Evaluación
independiente: `nova-host-delta-review-primitive-01/capture-01/ASSESSMENT.json`,
SHA256 `0984461F4C5BED9D249F6E91FA17A647EB0747A91D4C67EC7A957CE54359727A`.

El puente PowerShell emitía bytes OEM bajo el proceso sin consola de .NET, que
Python intentaba leer como UTF-8. La reparación QA separada primitive-02 fija
UTF-8 explícito en el productor, captura bytes y decodifica estrictamente en el
hilo principal. Conserva byte a byte fuentes, oráculos y primeros paquetes.
El propietario ejecutó 29 controles y un probe anidado de seis capturas,
Unicode y tres rechazos controlados, sin modelos ni builds. Esto repara I/O de
pruebas, no recupera las cinco respuestas finales ni demuestra compresión.
Los archivos y fallos de primitive-01 permanecen intactos.

El diagnóstico contemporáneo de orden del esquema ejecutó los seis casos en
dos brazos, alternando AB/BA: doce llamadas, sin reintentos. A conservó el
paquete original; B movió únicamente `responseContract` del final al principio
del JSON de usuario. El preflight verificó igualdad de todos los valores,
sistema, máscara nativa, modelo y opciones. Ambos obtuvieron **1/6**: sólo el
control sin redundancia conserva exactamente su contenido. La tarea original
queda idéntica en las doce respuestas, incluso cuando se declara REVISE.
Mover el esquema corrigió la pérdida de la prohibición de inventar datos en un
caso, pero no realizó su compresión. Diez propuestas compilan; las dos del
control numérico añaden una cantidad a un campo que no la tenía y el dominio las
rechaza. No se perdió el siete original de la tarea. Evidencia preservada en
`nova-schema-order-ab-01`; un test de captura de transporte aprobado no cambia
el resultado semántico ni justifica un cambio del orden en el producto.

La auditoría posterior distingue salida estructurada de function calling real:
CreateRequest convierte las herramientas en un esquema `{toolName, arguments}`
y Decode reconstruye `message.tool_calls`; el adaptador DMR rechaza llamadas
nativas. Por tanto las seis pruebas UI anteriores no acreditan ese protocolo
del proveedor. La siguiente primitiva evalúa una herramienta nativa de sólo
propuesta, sin ejecutar la aplicación, manteniendo pin, fuente y oráculos. No se
instala otro framework ni se cambia perfil. Es una hipótesis de mecanismo nueva,
no una causa confirmada ni una integración ya aprobada. Qwen documenta su
[protocolo de herramientas](https://qwen.readthedocs.io/en/stable/framework/function_call.html)
y Docker declara compatibilidad para modelos apropiados en su
[API](https://docs.docker.com/ai/model-runner/api-reference/).

NOVA R2 permanece **sin aprobar**. Las mejoras aisladas no sustituyen la
calificación completa de 28, la campaña final de 20 ni el recorrido publicado de
Crear. El objetivo final conserva 18/20, 9/10 por grupo, al menos 80% por familia
y cero efectos sin aprobación. No hay una nueva puntuación final válida.

Actualización de evidencia, 2 de noviembre: la primitiva de herramientas nativas
completó seis llamadas HTTP 200 y obtuvo **0/6** resultados semánticos. Su revisión
independiente y replay permanecen en `nova-native-tools-primitive-01/replay-01/ASSESSMENT.json`
(SHA256 `D96A081F9EC78071F0D6B8E60DED1BB93B1A5B732DAA7E0D14DE11434154B05C`).
El cambio de protocolo no se integró: compatibilidad de transporte no demostró
mejor interpretación.

La compresión posterior separa la elección de operación de una selección terminal
sobre el contrato humano original y el catálogo completo. La rama COMPRESS ya no
elige omisiones en la primera respuesta. La aplicación mide ambos contextos antes
de inferir, exige IDs ligados a la fuente y diferencia COMPRESSED de UNCHANGED;
ninguno de esos controles certifica equivalencia semántica. El primer candidato
obtuvo **3/6**; su revisión permanece intacta en
`compression-goal-selector-20261102/capture-01/ASSESSMENT.json`.

R2b conservó fuente, manual, candidatos y esquema de las doce peticiones; sólo
cambió la capacidad de razonamiento de la selección terminal junto con su perfil
de muestreo existente. C1 usa false/T0/1400; los cinco selectores terminales usan
true/T.6/P.95/K20/minP0/seed42017/3500; el control numérico conserva la rama de
reformulación sin razonamiento. Las doce llamadas Core, cero reintentos, el límite
compartido de 90 segundos y la restauración exacta de toda la configuración DMR
constan en la captura. Resultado: **5/6**, con C-MULTI-TASK aún FAIL porque retiene
una repetición solicitada para eliminar. Variables, metadatos, referencias y
obligaciones únicas se conservaron en los seis resultados. Recibo:
`compression-goal-selector-20261102/revision-02/capture-01/ASSESSMENT.json`, SHA256
`9E518FC6D3F4205D2C2D8FFFD7141C75E2D7E0539354E6783BEB04B33994A742`.

La regresión R2b completa registró 1798 PASS, 2 FAIL y 2 SKIP. Los dos fallos preservados
son expectativas anteriores de razonamiento en C1. El propietario integral confirmó
la política unificada C1 false y C2 según capacidad explícita. La actualización de
esas aserciones se realizará en el siguiente snapshot de entrega, conservando los
controles de fuente, contenido, llamadas, cancelación y aprobación. No se modifica
el freeze ni se presenta aquella ejecución como verde.

El perfil instalado continúa pendiente. El snapshot de entrega añade una conexión
versionada seleccionada expresamente, con los mismos scopes y acciones de NOVA,
preflight local de sólo lectura, y revalidación del grant persistido antes de cuota
y bajo el fence existente. La migración aditiva sólo amplía los IDs exactos admitidos
por CHECK y RLS; no se ejecuta contra producción o bases privadas. No hay caché de
consentimiento ni inferencia de permiso a partir de OAuth o presencia. Este trabajo
de componente no suma puntuaciones históricas: la aceptación exige una nueva
campaña conjunta y el recorrido publicado.
