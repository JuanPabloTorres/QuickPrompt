# Certificación local y diagnóstico por evidencia — 22 de octubre de 2026

Este documento describe la revisión del Lab en `http://127.0.0.1:8803/`.
La disponibilidad de la herramienta no certifica QuickPrompt. El candidato fijado
es `input-authority-v9`, Qwen `qwen3:8b`, frontend `local-copy-v11`.

## Recorrido del operador y del agente

### Expediente uniforme de reparación

Verificación de esta incorporación: **11 comprobaciones reales de UI PASS**, más
**5 regresiones de certificación y 7 del visor semántico PASS**. Nueve exports
JSON cumplen el esquema; los controles negativos rechazan causa interna
inventada y envío del oráculo al modelo. Sin llamadas nuevas a Qwen: se usó una
respuesta pública conservada y fixtures UI explícitos. El arnés conserva hashes
de JS/CSS/esquema antes y después, iguales, y snapshots de esos archivos.
Evidencia final:
`AI-Delivery-Lab/artifacts/closure-20261021/browser/repair-dossier-1792668634071/`.
La UI del Lab muestra tanto el intento fallido del selector como la aceptación
posterior de 11/11. Los eventos de importación indican recepción del informe;
no inventan instantes de aserción. Informe/gráficos guardados desde la UI:
`d19116f3331052460432592d`. Esto acredita el expediente, no reparación semántica.

En **Casos**, abrir una petición y consultar **Expediente de reparación**. En
**Flujos**, el grupo enlaza también un expediente de su observación original.
**Exportar JSON** genera `ai-delivery-repair-dossier/1`; esquema en
`AI-Delivery-Lab/ai_delivery_lab/repair-dossier.schema.json`. Está activo en la UI
8803 mediante sus assets existentes. No cambia el motor ni ejecuta otra inferencia.

El expediente reúne entrada/mensajes capturados, modelo/digest, configuración
observada, oráculo independiente, propuesta pública, resultado vinculado,
comparaciones, eventos, archivos, patrón de la muestra, huecos de captura y
controles correctos que deben conservarse. No confunde parámetros observados en
el host con bytes HTTP ni asigna al modelo una causa interna desconocida. Las
fechas y ubicaciones mantienen su procedencia, sin inferir una línea de fallo a
partir de una referencia estática. Los controles conservados identifican replay
o ejecución fresca; repetir un replay no demuestra mejora semántica.

| Acción propuesta por el expediente | Qué debe hacer el agente |
| --- | --- |
| CAPTURE_MISSING_EVIDENCE | Obtener la salida pública/señal faltante; no culpar una etapa posterior |
| CAPTURE_INDEPENDENT_ORACLE | Definir el resultado esperado antes de aceptar COMPLETED o PASS |
| AUDIT_ORACLE_SCOPE | Revisar por qué un caso PASS contiene comparaciones FAIL, conservando ambos |
| ISOLATE_EVALUATION_PROFILE | Separar perfiles asistidos o identidades incompatibles |
| PRESERVE_CONTROL | Proteger el comportamiento correcto, sin refactorizar por este caso |
| ISOLATE_VARIABILITY | Contrastar observaciones de la misma entrada/runtime; no escoger sólo la buena |
| VERIFY_RECORDED_BOUNDARY | Usar la divergencia ya registrada y contrastar una intervención en su propietario |
| COMPARE_PROPOSAL_AND_BINDING | Comparar ambas representaciones antes de atribuir la causa a una de ellas |

Son instrucciones de diagnóstico y no nueva autoridad para modificar código,
ejecutar comandos o aprobar efectos. Se proyectan sobre resultados existentes:
no hay otro juez LLM, intérprete de intención ni oráculo añadido en el frontend.
El formato también admite contratos RAG por configuración; su prueba de
sustitución es sintética y sólo acredita el expediente, no un RAG productivo.

La responsabilidad queda uniforme: **Core/Pydantic AI** adapta proveedor,
estructura, presupuesto y validadores del framework; **.NET** posee datos,
revisiones, permisos y efectos de negocio; **Lab** compara el resultado con el
contrato independiente y conserva evidencia; **el agente de desarrollo** elige
una corrección contrastable a partir del expediente. Un framework que valida la
forma del JSON no acredita todos los objetivos de una petición humana.

En el caso de agenda, el expediente registra dos objetivos esperados y sólo
PREPARE_COPY_PREVIEW en la propuesta. La aceptación de Bind/Compile no lo
convierte en tarea correcta. Construir el contrato de finalización desde esa
misma propuesta tampoco constituye comprobación independiente. Esta distinción
debe conservarse al corregir el producto; el expediente no lo repara por sí solo.

1. Abrir **Certificación**. Verificar candidato, alcance local, las ocho etapas y
   los 87 criterios obligatorios. Expandir un criterio para leer su contrato y
   evidencia. Un cierre histórico de la matriz no es un PASS actual.
2. Ejecutar **Batería completa**. La cola pertenece a esa campaña hasta terminar;
   no introducir otra inferencia. Sus comandos están configurados localmente: la
   UI no acepta código arbitrario. Se congelan definición, candidato y hashes de
   ejecutores antes de la primera etapa.
3. Desde cada etapa, abrir **Ver casos y flujo**. Filtrar un caso fallido y comparar
   esperado/obtenido; recorrer eventos, archivo y ubicación capturada. El árbol de
   código muestra contexto y línea real cuando existe evidencia de esa ubicación.
   Una referencia estática no demuestra ejecución; la línea del importador de
   tests no es una línea de fallo interno del producto.
4. En casos semánticos, inspeccionar separadamente petición/contexto, contrato,
   salida pública del modelo, validación JSON, binding y objetivos independientes.
   JSON válido y plan aceptado pueden coexistir con una tarea fallida.
5. Abrir **Flujos** para comparar observaciones del mismo runtime, modelo/digest,
   perfil y entrada. El enlace de una observación abre su caso original. Las
   ejecuciones copiadas/reproducidas no cuentan como otra inferencia fresca.
6. Guardar informe y gráficos. Conservar campañas fallidas y candidatos
   descartados. Repetir una prueba produce evidencia nueva, no reemplaza un fallo.

## Interpretación de los patrones

| Patrón observado | Decisión que ayuda a tomar | Qué no demuestra |
| --- | --- | --- |
| Una sola observación correcta | Proteger ese control y ampliar evidencia | Fiabilidad general |
| Varias correctas comparables | Preservar lógica y regresiones de esa muestra | Que toda petición funcionará |
| Una observación fallida | Reproducir y rastrear primera divergencia | Causa confirmada |
| Varias fallidas comparables | Auditar propietario/contrato antes de corregir | Que todo el módulo esté roto |
| Correctas y fallidas comparables | Aislar contexto, identidad, salida y efectos | Aleatoriedad demostrada de todos los parámetros internos |
| Falta identidad o señal | Capturar la frontera que falta | PASS ni permiso para refactorizar |

La tarjeta de flujo conserva propietario, propósito, método, campos evaluados y
referencias de implementación. Estas descripciones son configuración explícita;
el Lab no deduce automáticamente la semántica de cualquier repositorio.

## Condiciones de certificación

- Todas las etapas deben terminar con proceso correcto, contrato de informe
  satisfecho, resultados completos e identidades únicas; ninguna omitida.
- Cada criterio exige una relación explícita con sus comprobaciones. **Sin
  vincular**, **sin ejecutar**, **bloqueado** y **sin confirmar** impiden cerrar.
- Validation necesita desarrollo correcto; holdout necesita desarrollo y
  validation correctos. No se usa holdout para ajustar el modelo.
- UI y copia se ejecutan independientemente para conservar su evidencia incluso
  cuando falla la semántica.
- El decisor final recibe sólo informes de esta campaña; ausencia permanece
  `null`, sin recurrir a un informe histórico correcto.
- Se verifica identidad del SDK Python, worker, DLL, frontend, dataset,
  modelo/digest y perfil. Cambiar ejecutores o candidato durante una campaña la
  deja sin confirmar.
- La copia local incluye Windows Unicode/CRLF y archivos UTF-8/LF exactos para
  plantilla guardada y borrador. El fallo histórico de LF crudo en Windows sigue
  conservado; la nueva definición corresponde a la decisión explícita del usuario.

El botón ejecuta la batería configurada. La lista completa hace visibles también
las capacidades todavía sin prueba vinculada. **No significa que cada requisito
ya tenga implementado su test de aceptación**. La configuración inicial sólo
vincula los dos contratos de copia. Hay que auditar los demás vínculos antes de
proclamar cobertura integral; no asignarlos por parecido de nombres.

La revisión posterior probada en código agrega REL-01, REL-02 y REL-05: grupos
backend con cardinalidad exacta, casos React explícitos y los tres splits con
aceptación conjunta. Inventario revisado en dos TRX: 1117 ejecuciones/1110 grupos;
React: 241 casos. Quedan 82 criterios sin vincular en esa configuración. No cambia
los resultados de la campaña anterior, que conservó 85 sin vincular y dos PASS.
La revisión automática bloqueó iniciar el servicio adicional 8804 (`blocked by
policy`); esos nuevos vínculos **no están cargados** en el proceso 8803.

## Reproducción del problema semántico actual

Campaña `certification-8ed4d1fc75a0`, etapa validation,
informe `docs/evidence/closure-lab-20261021/lab-live-ab8fccfde057/validation.json`:

| Caso | Esperado | Propuesta pública recibida | Primera divergencia |
| --- | --- | --- | --- |
| validation-11, agenda para decisiones y preparar copia | PROPOSE_ADAPTATION + PREPARE_COPY_PREVIEW; biblioteca intacta | Sólo PREPARE_COPY_PREVIEW | Falta adaptación antes de Bind/Compile |
| validation-16, preparada para Claude sin enviar | PREPARE_PROVIDER para claude | PROPOSE_ADAPTATION, providerId claude | Operación incorrecta aunque el destino textual sea correcto |

Desarrollo pasó 16/16; validation pasó 14/16. Esas dos cifras no se agregan para
ocultar los casos. La captura HTTP anterior y el replay de la frontera demuestran
que el contrato acepta propuestas semánticamente incompletas y el host las
compila fielmente. No muestran una línea de error dentro de Qwen ni prueban que
Python esté eliminando objetivos durante la serialización.

El `TaskIntentContract` derivado de la misma propuesta no es un segundo oráculo
independiente. `DirectEvidence` comprueba presencia literal de la cita y no puede
demostrar por sí solo implicación semántica o autorización de un efecto.

## Límites conocidos y correcciones de la herramienta

- El importador de componentes inicialmente identificó casos por nombre visible
  de xUnit. Algunos datos se truncan y los nombres se repiten: la campaña conservó
  1459 resultados PASS y un FAIL adicional de identidad. No son 1459 casos de
  agente ni un fallo nuevo de producto.
- `trx-results.py` conserva grupos por ID del adaptador y hojas por ejecución.
  Se comprobó contra dos TRX originales: 1117 ejecuciones, 1110 grupos en ambos,
  sin perder resultados. Los valores completos de parámetros no capturados no
  se inventan. Ya se integró al ejecutor y se ejecutó desde la UI del Lab:
  `lab-live-93f8f33f1922`, **1465/1465 resultados PASS, cero omitidos**. Incluye
  seis controles del importador. 814 resultados backend enlazan su definición
  estática de test en el código congelado; el resto no inventa una línea.
  Ese resultado posterior no sustituye el componente fallido de la campaña.
- El experimento `schema-grounding-v10` quedó descartado: 3/4 controles. El
  candidato aislado `native-output-tools-v11` cambia sólo el portador de salida
  del framework; pasó 57 métodos de componente, pero el contraste real
  `lab-native-output-tools-contrast01` terminó **0/4**: tres
  `CORE_INTERPRETATION_FAILED` y un `TaskCanceledException`. Quedó descartado,
  sin segunda tanda ni despliegue. No se capturó el detalle interno de esas
  fallas del worker; no se atribuye el timeout a una causa no demostrada.
- Estado real de la campaña: consultar `state.json` y Certificación; este documento
  no transforma un resultado en curso en cierre. Un piloto humano pendiente no
  puede sustituirse por una declaración automática.

## Resultado completo conservado de la campaña

`certification-8ed4d1fc75a0`: inicio 06:31 y final 06:52 del 22 de octubre,
UTC−04. Estado **NOT_CERTIFIED**, `certified: false`.

| Etapa | Resultado de esa campaña |
| --- | --- |
| Componentes | FAIL del importador: 1459 resultados correctos y un guard fallido de identidad |
| Lab | PASS, 133 registros de aserción; 92 métodos de test |
| Desarrollo | PASS, 16/16 |
| Validación | FAIL, 14/16; casos 11 y 16 |
| Holdout | BLOCKED por validación; no ejecutado |
| UI real | FAIL, 12/13; preparación para Claude |
| Copia local | PASS, 3/3; CRLF y dos descargas LF |
| Decisión | FAIL; 11 comprobaciones, cierre impedido |

Después de esa campaña: Lab **94 métodos PASS**, controles de certificación
**12/12**, UI del Lab **5/5** contra el estado final y 1465 resultados paginados,
390px y cero errores de página. Evidencia UI:
`AI-Delivery-Lab/artifacts/closure-20261021/browser/certification-1792666789314/results.json`.

No hay corrección semántica aceptada ni certificación al 100 %. Permanecen la
omisión/composición incorrecta de objetivos, el fallo UI de Claude, la cobertura
de criterios sin vínculo y los demás requisitos abiertos de la matriz. No se
declara que 52 requisitos abiertos sean 52 errores nuevos de código.
