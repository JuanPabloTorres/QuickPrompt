# Preparación de entrega

## Candidato comprobado — 02/11/2026

**NO-GO.** La identidad local actual es `closure-20261102-native-final`, versión
`1.0.1-rc.2`, base `bbefd38626e82c4ce389cd3735a657b0a2f929f0` con delta.
Inventario y recibos: `.playwright/candidates/closure-20261102-native-final/`;
artefactos y decisión local: `.playwright/closure-r2-native-20261102/delivery.json`.
La [aceptación actual](MVP_ACCEPTANCE.md) distingue suites, campañas y los
recorridos no ejecutados; el [roadmap único](development/GO_TO_MARKET.md) conserva
R1–R6 y los fallos. No atribuir las cifras de los cortes inferiores a este árbol.

La primitiva nativa congelada se ejecutó sin integración: seis llamadas de
protocolo válidas y **0/6 semántico** contra sus oráculos completos, con doce
controles offline PASS y replay del dominio actual sin efectos. Se rechaza ese
reemplazo; el código de producto permanece idéntico al corte Accounts. No se
suma este ensayo al 14/20 principal ni a traducción 0/4. La última lectura pública
de opciones agotó 30 s: no demuestra configuración nueva ni disponibilidad final.

Accounts corregido: **731 PASS/0 FAIL/35 SKIP**, 766 casos, 16 min 25 s, con
PostgreSQL real y sin dispatcher QA independiente. No cambió el producto: se
corrigieron selección de operación, fuente sintética y cadencia de sondeo del
arnés, preservando las 66 aserciones. Crear literal se cubre además por HTTP con
cero trabajos de inferencia y sin guardar. Los opt-in, el 14/20 semántico y el
0/4 de traducción no se convierten en PASS. Health público agotó 30 s en el
último control; su causa permanece desconocida aunque Render respondió después.

Se construyó la imagen local `quickprompt-cloud:closure-20261102`, digest
`sha256:6aae8a7270995002a5b986afae8081ce59edba9837e3478cb95450bcdd73c469`.
No se subió a un registro ni se desplegó. El contenedor conserva UID 1654 y pasó
19 controles sintéticos de transporte de claves, sin red. No hay credenciales
reales en el build o paquete de este corte.

Companion ZIP: `727c311faee80f775f69eff471fe78dc6829a3182abcdb8de1fc4dd76f8758f0`,
68 archivos, dos exports idénticos, 15 controles de relocación y nueve del ciclo
portable. Inputs backend del paquete comprobados sin cambios posteriores.
Ese paquete no sustituyó el instalado compat.1 ni acredita SO limpio o worker
actualizado autenticado. Instrucciones en [operación](../deploy/operations/README.md).

El build y la suite backend completa pasan; NOVA no cumple 18/20 y la traducción
por campo falla. Correo y aceptación pública siguen abiertos. Ninguna de esas
fallas se compensa con el número de tests, el manifiesto o un ZIP reproducible.

Referencia: **1.0.1-rc.2**, código `bbefd38626e82c4ce389cd3735a657b0a2f929f0`,
31/10/2026. Este conjunto documental posterior todavía no tiene commit propio.
Estado: **NO-GO**; no es una versión estable ni una autorización de publicación.

Las cifras de este corte son históricas. Los 1504 PASS terminaron el 01/11 a las
10:09:33 a. m. (UTC−04:00); no validan los cambios posteriores del productor.
El snapshot posterior de autoría conserva 105 PASS/11 FAIL de 116, con integración
pendiente. Véanse [recibos y hashes](MVP_ACCEPTANCE.md#procedencia-de-las-cifras--01112026)
y el [estado vigente](development/GO_TO_MARKET.md). No se ejecutó otra suite en
esta aclaración documental ni se retiraron fixtures fallidos.

## Corte local del equipo — 01/11/2026

Se conserva **NO-GO** y el presupuesto confirmado de un proveedor cloud/$0.
La corrección activa de fábrica afecta dos fuentes de `QuickPrompt.Api` y añade
`ModelConnectionCompositionTests`; respeta una conexión Core explícita y rechaza
bindings incompatibles antes de lanzar Python. Se mantiene el cliente HTTP de
disponibilidad anterior después de detectar y retirar su regresión experimental.
No cambia esquemas, permisos, publicación o el contrato de inferencia.

Backend Release de ese corte: 1504 PASS/0 FAIL/2 SKIP opt-in; frontend: 502 PASS;
Cloud Release build: 0 errores/0 warnings; web build PASS con warning 537,13 kB;
Recovery: 45 PASS/0 FAIL/0 SKIP. Fronteras estáticas 40 PASS y layout 7 PASS.
Los hashes de esas dos fuentes y su test no variaron durante la validación.
Recibos: `.playwright/priority-closure-20261101/commercial-team/` y
`team-recovery/recovery-current.trx`. No hay aceptación pública de este delta,
commit nuevo, CI remota nueva ni un ZIP de fuentes actualizado a este corte.
Los ZIP anteriores conservan su identidad y su alcance.
Un opt-in separado comprobó la fábrica contra Qwen/DMR real: una llamada y
extracción sintética exacta (1 PASS, 16 376 ms), sin cuentas, DB, UI, persistencia
o Companion instalado. Se ejecutó sin recompilar el assembly Release validado;
se conserva el bloqueo de DLL del intento previo como fallo de arnés. Recibos
`factory-connection/model-connection-real-executed.trx` y `real/factory-dmr.json`
bajo el mismo directorio de evidencia del 01/11.

Los candidatos semánticos nuevos de escritor/selector se rechazaron; no están en
el producto ni cambian el resultado histórico 9/20. Recovery actual no es un
executor recurrente: la propuesta de política aún carece de decisiones de
origen/inventario, identidades, certificado/custodia, destino, retención y alertas.
No se habilitó un scheduler ni se copiaron datos privados.

Revisión actual dirigida de hosting: Northflank mantiene un incidente oficial de
seguridad abierto; Koyeb limita la base gratuita a 5 horas activas mensuales y
desaconseja producción en su instancia web Free. Ninguno queda seleccionado para
QuickPrompt. Se conserva el límite de 30 días de Render PostgreSQL Free y no se
afirma que sea imposible todo alojamiento gratuito, sólo que no hay destino
compatible verificado. Fuentes: [Northflank](https://status.northflank.com/),
[Koyeb cuotas](https://www.koyeb.com/docs/faqs/pricing) e
[instancias Koyeb](https://www.koyeb.com/docs/reference/instances).

La lectura pública anónima final de `/account/options` no completó en 25 segundos;
causa desconocida, sin inferir caída o recuperación. Recibo
`commercial-team/public-options-timeout.json` bajo el prefijo del 01/11. Los flags
previos de correo/Google conservan su fecha y no se actualizan por este timeout.

## Candidato y entornos

- Checkout aislado RC2, preservando el árbol original con cambios sin integrar.
- React/TypeScript/Vite para interfaz; .NET 10 para Cloud/Accounts y dominio;
  PostgreSQL con migraciones y aislamiento por actor/biblioteca.
- Inference comparte contratos con Core/Python; Qwen3:8b está administrado por
  Docker Model Runner, separado de Cloud y accesible mediante Companion emparejado.
- Público actual: web Vercel y backend persistente Render. La última revisión
  pública registrada es `4cb104b`: el panel de Render confirmó Live para ese SHA
  el 31/10, en plan Free. La rama configurada es distinta de RC2; no demuestra
  bbefd38. No desplegar «latest» de otra rama por suposición.

## Comprobaciones disponibles

[CI exacta](https://github.com/JuanPabloTorres/QuickPrompt/actions/runs/41228098851)
SUCCESS para `bbefd38`; conteos, omitidas y límites en [aceptación](MVP_ACCEPTANCE.md).
Restore Cloud con lock aprobado; sin dependencias nuevas en el adaptador Mailjet.
Build Cloud Release local nuevo: 0 errores/0 advertencias; build frontend aprobado,
con advertencia de chunk de 536,94 kB que se conserva.

Fuente archivada `quickprompt-1.0.1-rc.2-bbefd38-source.zip`, SHA-256
`62049af25c67cabb0845ffdbd436578eca971099fa1f829104d8a03d948fa349`.
El paquete Companion de la reparación anterior tiene 68 entradas y dos ZIP
idénticos, SHA-256 `5e9b2c01bce01a6d9a045bc1f1b4f8f4454b1ce5017d4076d90618096f2919b5`.
Los inputs Companion no cambian en bbefd38; ese paquete no es una instalación
limpia ni demuestra arranque autenticado del cliente.

## Operación antes de publicar

Aplicar las guías propietarias de [desarrollo](development/DEVELOPMENT.md),
[Companion](development/COMPANION.md), [correo](development/ACCOUNT_MAIL.md),
[MCP](development/MCP.md) y [SaaS](development/SAAS_DELIVERY.md).
Configurar secretos fuera de Git, paquetes, frontend y logs. No activar dos
proveedores de correo ni hacer fallback silencioso; la elección vigente vuelve
a ser Brevo por petición del usuario.

Falta verificar correo real, origen/alojamiento comercial definitivo autorizado,
recuperación cifrada recurrente, instalación/actualización/retirada y aceptación
conjunta del candidato publicado. Antes de una migración, identificar esquema,
rollback y acceso autorizado; no recrear una base ni reusar una aprobación consumida.
La nueva especificación de cierre reserva despliegue, push y acciones destructivas
a autorización expresa; no se realizan por completar este documento.

## Delta local posterior a bbefd38

El 01/11 se separó Crear literal de Mejorar/Adaptar con IA por decisión expresa
del usuario, sin cambios de permisos ni esquemas. Backend 1492 PASS/1 hardware
SKIP; web 502 PASS/build PASS; Cloud build PASS; Accounts 90 controles PASS sin
SQL. DMR real .NET y segundo consumidor genérico PASS, con una adaptación útil
acotada PASS en dos llamadas. No son aceptación pública ni fidelidad universal.
Dos exports independientes del delta tienen el mismo ZIP SHA-256
53583098fd3a22f8963edd72bc4dee6a3c6094bf925d2927f0a4302d89fd70ff;
15 controles de traslado PASS. Este paquete sustituye al hash anterior sólo
para este corte local; no se atribuye al commit base ni a producción. Recibos
en `.playwright/priority-closure-20261101/`. No se ha actualizado o arrancado el
Companion autenticado instalado durante este corte.

El lector compartido de recibos de correo incorpora una corrección de IOException
y cancelación, con 139 tests controlados PASS tras seis fallos reproducidos.
Después se añadió Brevo M2M explícito dentro del adaptador existente, sin nuevas
dependencias ni cambios de Identity, esquema o permisos de usuarios/MCP. La campaña
final afectada suma 180 PASS/0 FAIL/0 SKIP con HTTP controlado, incluido el control
de conformidad OAuth scope omitido que falló antes de corregirse.
Ese delta y sus tests todavía no tienen commit ni CI remoto; no están en el ZIP
de fuentes bbefd38 ni en producción. No atribuirle la CI anterior por equivalencia.
Credencial M2M real cifrada localmente y token 200 comprobados. El envío recibe 403
por plataforma SMTP sin activar; la solicitud autorizada quedó publicada. La
entrega real y configuración remota siguen pendientes; véase
[activación y modos incompatibles](development/ACCOUNT_MAIL.md).
