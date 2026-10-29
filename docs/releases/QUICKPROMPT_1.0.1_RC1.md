# QuickPrompt 1.0.1-rc.1

Decisión comercial: **NO-GO**. Este candidato estabiliza mecanismos existentes;
no es una versión estable ni autoriza publicar comercialmente. El piloto previo
conserva su identidad hasta que se complete la aceptación del nuevo candidato.

## Identidad y alcance

- Base publicada: `656101ca1b09ea3a091b8c5002b00b4c672b9dfb`.
- Rama del candidato: `release/quickprompt-1.0.1-rc.1`.
- Versión visible y lock web: `1.0.1-rc.1`, sin cambiar versiones de dependencias.
- Un commit identificado y su manifiesto externo vinculan fuente, paquete y
  recibos. El manifiesto de entrega se genera después del commit para evitar una
  referencia circular al hash del propio documento.
- Se preservan Python/Core, .NET/dominio, interfaz, Companion y conectores.
  No se elimina legado, cambia modelo, amplía scopes o inicia inferencia desde
  una señal de presencia.

El registro activo de pendientes sigue en [GO_TO_MARKET](../development/GO_TO_MARKET.md).
Este documento es la matriz del candidato, no otra lista de tareas independiente.

## Correcciones y evidencia de causalidad

| Mecanismo | Antes | Corrección y regresión |
|---|---|---|
| Ausencia de Companion entre etapas | La nueva excepción de presencia escapaba como fallo de navegador; una allowlist posterior también perdía su código | Adaptación tipada y código permitido hasta la proyección segura. No se admite otro hijo ni se reejecutan recibos |
| Retirada de presencia al salir | Fallos locales de E/S, permisos o criptografía podían sustituir resultado, cancelación o excepción principal | Limpieza acotada que conserva el resultado original. Presencia caduca si no se confirma la retirada; no se revoca el grant |
| Rollback de la migración observacional | El `Down` generado borraba la tabla antes del rechazo de una migración anterior | Rechazo del rollback destructivo antes de SQL, con guarda válida antes y después. No cambia `Up` ni el esquema propuesto |
| Vista vencida o lectura tardía | Una señal antigua seguía rotulada como actual; lecturas anteriores podían reemplazar una observación más nueva | Caducidad visible y orden de lecturas, conservando identidad de ejecución y capacidad de la pestaña. La presencia no da autoridad |
| Enumeración por fallo del correo | Registro, recuperación o reenvío podían distinguir existencia mediante 500/202 | Respuesta pública uniforme y advertencia privada acotada, sin destinatarios, enlaces ni tokens. No acredita indistinguibilidad temporal |

Los fallos anteriores se conservan en sus recibos; no se reclasifican como PASS.
Los positivos de dispositivo hacen polling autenticado explícito. Un helper de
consentimiento no crea presencia ni evita los negativos sin worker.

## NOVA: límites y resultado confirmado

Recorrido auditado:

1. La acción, biblioteca, origen y nombres de variables fijan el contrato.
2. El material del escritor conserva petición original y aclaraciones. Identidad,
   permisos y aprobación permanecen en sus propietarios; la propuesta del modelo
   no puede ampliarlos.
3. La definición inicial se muestra para revisión, sin guardar ni ejecutar.
4. La persona confirma contenido y roles de referencias. Se compone literalmente
   ese contenido; la aplicación añade sólo los marcadores elegidos que falten.
5. Guardar requiere su aprobación separada y persistencia transaccional. Recuperar
   metadatos o eventos no recupera esa aprobación ni una capacidad de otra pestaña.

La campaña separó tres casos históricos y dos dominios holdout. La revisión
independiente de las propuestas iniciales dio **1 PASS, 2 FAIL y 2 PARCIAL**.
No fue doble ciego ni demuestra una tasa general de fiabilidad. La composición
con corrección humana sintética no acredita fidelidad inicial o aceptación humana.

Se reforzó el caso «preguntar antes» para que no pase sustituyendo directamente
el contrato esperado. Un experimento de instrucciones alcanzó una pregunta real,
pero produjo una aclaración posterior innecesaria y no estabilizó otros requisitos;
se retiró. La definición de producción queda conservada y el fallo permanece
visible en la campaña opt-in con Qwen3:8b. No se añadieron reglas por palabras
clave ni un segundo clasificador para aparentar cierre.

## Matriz de aceptación comercial

| Control | Estado | Alcance y evidencia necesaria |
|---|---|---|
| Regresiones de presencia, permisos, salida y rollback | PASS focal | Recibos de reproducción y corrección; la ejecución conjunta se atribuye al commit exacto en CI |
| Caducidad, orden de eventos y recuperación visual | PASS focal | Fixtures controlados. No equivale a una ejecución pública con el modelo real |
| Fuente, dependencias y paquete portable | Ver manifiesto de entrega | Hashes de inputs y payload, contratos y reubicación. Windows limpio y vinculación real siguen sin acreditar |
| Fidelidad inicial y aclaración solicitada de NOVA | FAIL | Las propuestas iniciales aún introducen restricciones o eluden una decisión del usuario; la revisión humana no sustituye este resultado |
| HTTPS y transporte PostgreSQL del piloto existente | PASS limitado | Conexión existente VerifyFull, TLS 1.3/256 bits y rol limitado; sólo metadatos, sin nueva migración productiva |
| Cifrado PostgreSQL en reposo | PASS documental | Garantía de plataforma AES-256; no inspección de discos/claves ni copia externa recuperable |
| Correo real de confirmación y recuperación | BLOQUEADO | Runtime `mailDelivery=unconfigured`; no se observó un remitente y credencial autorizados utilizables. Los contratos sintéticos no son mensajes recibidos |
| Alojamiento comercial definitivo | BLOQUEADO | Vercel Hobby es personal no comercial; PostgreSQL Free de Render caduca. Cuenta/capacidad/recursos OCI no verificados |
| Backup periódico cifrado y recuperación funcional | BLOQUEADO | El backup privado puntual ya probado no es un scheduler. Faltan destino, custodia de claves, autoridad recurrente y recuperación de aplicación |
| Dos cuentas en el candidato público final | BLOQUEADO | CRUD, compartir, aislamiento, ejecución y cancelación deben comprobarse con el candidato ofrecido, no con mocks o una versión anterior |
| ChatGPT/MCP: dos identidades, renovación y revocación | BLOQUEADO | Una búsqueda real desde el conector de Codex funciona; no prueba cliente ChatGPT web ni la segunda identidad |
| Liberación comercial y tag estable | BLOQUEADO | Requiere resolver los FAIL y BLOQUEADO críticos anteriores y registrar aceptación humana SaaS |

Las suites que desactivan RealQwen deben informar omitidos separadamente.
Una CI verde no elimina la campaña real fallida ni los controles externos bloqueados.

## Reproducción

Desde el commit del candidato, con los prerrequisitos documentados:

```powershell
npm ci --ignore-scripts
npm ci --ignore-scripts --prefix apps/web
node scripts/Check-RepositoryLayout.cjs
node scripts/Check-ModuleBoundaries.cjs
dotnet restore backend/QuickPrompt.Cloud/QuickPrompt.Cloud.csproj --locked-mode
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj -c Release
dotnet test backend/QuickPrompt.Tests/QuickPrompt.Tests.csproj -c Debug --filter FullyQualifiedName~NovaTelemetryTests
dotnet test backend/QuickPrompt.Companion.Tests/QuickPrompt.Companion.Tests.csproj -c Release
dotnet test backend/QuickPrompt.Mcp.Tests/QuickPrompt.Mcp.Tests.csproj -c Release
dotnet test Tests/recovery/QuickPrompt.Recovery.Tests.csproj -c Release
dotnet test src/QuickPrompt.Tests/QuickPrompt.Tests.csproj -c Release
./Tests/companion/package-contract.ps1
npm run build --prefix apps/web
npm run test:release --prefix apps/web
```

El workflow [nova-web.yml](../../.github/workflows/nova-web.yml) prepara PostgreSQL
sintético con roles limitados y ejecuta Accounts, el contraste de catálogo y la
restauración integral. No usar una base privada para reproducirlo. El contrato
nuevo espera 36 tablas y 23 FORCE RLS; los recibos de 35/22 son del piloto previo.

La campaña real requiere configurar explícitamente
`QUICKPROMPT_TEST_REAL_QWEN=1`, `QUICKPROMPT_TEST_CORE_PYTHON`,
`QUICKPROMPT_TEST_CORE_WORKER`, `QUICKPROMPT_TEST_CORE_SDK`,
`QUICKPROMPT_TEST_OLLAMA_ENDPOINT` y un directorio sintético de evidencia en
`QUICKPROMPT_TEST_REAL_EVIDENCE`. No incluir valores privados en Git ni en el
paquete. Ejecutar el filtro `FullyQualifiedName~ConfirmedBriefRealProviderTests&Category=RealQwen`
en Accounts y conservar el fallo de aclaración si se reproduce.

## Operación y reversión

- [Componente local](../development/COMPANION.md) y [presencia](../development/COMPANION_PRESENCE.md).
- [Correo](../development/ACCOUNT_MAIL.md), [operación](../../deploy/operations/README.md)
  y [política de copia propuesta](../../deploy/operations/backup-policy.template.json).
- La migración nueva es aditiva y observacional; no ejecutar `Down` destructivo.
  Una reversión binaria requiere compatibilidad comprobada y no restaura permisos
  o recibos consumidos. No se ha aplicado esta migración en producción.
- El paquete no lleva perfiles, tokens, bibliotecas, venv ni pesos del modelo.
  Actualizar o retirar binarios no debe borrar credenciales de otra instalación;
  la revocación sigue siendo una operación explícita en QuickPrompt.
- No se contrataron planes, crearon credenciales ni reutilizaron aprobaciones
  puntuales de respaldo para operaciones recurrentes.

Fuentes: [Vercel Hobby](https://vercel.com/docs/plans/hobby),
[Render Free](https://render.com/docs/free),
[PostgreSQL de Render](https://render.com/docs/postgresql-creating-connecting).
Son límites de plataforma consultados para el piloto, no promesas de SLA del MVP.
