> HISTÓRICO: este documento describe un perfil retirado. Para el producto actual, consultar [documentación vigente](../README.md).

# Resultado de NOVA en una ventana

El resultado de una petición en inicio se abre automáticamente en un modal. Se puede cerrar con la X, **Cerrar resultado**, Escape o pulsando el fondo. Inicio conserva un acceso compacto, **Ver resultado de NOVA**, sin dejar la plantilla y el formulario desplegados debajo.

Cerrar conserva el borrador, los campos pendientes y el ajuste escrito en memoria de la pantalla. Reabrir no repite la petición ni guarda el prompt en la biblioteca. Guardar sigue siendo una acción explícita. Una operación de completar o ajustar que termine después de cerrar no vuelve a abrir el modal.

La vista previa sigue visible dentro de la ventana. **Usar en…** abre su diálogo encima y devuelve el foco a su botón al cerrar. El foco queda contenido en el modal y vuelve al campo de inicio o al botón que lo abrió. Las etiquetas de campos son independientes de sus valores para mantener nombres accesibles estables al reabrir.

Se reutiliza el diálogo Radix existente y su overlay local, con eventos del puntero habilitados en el fondo. No se modificó la política CSP, la API ni la persistencia.

## Validación

- Compilación TypeScript/Vite correcta.
- 17 pruebas de frontend correctas, incluida conservación de campos al cerrar y finalización en segundo plano sin reapertura automática.
- [Edge real con Qwen y SQLite aislada](../evidence/preparation-modal/acceptance.json): escritorio de 1280 px y móvil de 390 px; apertura automática, cierre con cuatro mecanismos, foco, completar campos, ajuste conservado y diálogo de uso anidado. Una petición por recorrido, biblioteca sin guardados nuevos, sin errores de consola ni desbordamientos.
- [Guía y biblioteca](../evidence/learning/acceptance.json): ocho comprobaciones correctas, incluidos los cuatro videos, pantalla completa y borrador del editor conservado.
- Los fallos detectados durante el desarrollo se conservan en `docs/evidence/preparation-modal/iterations` y `docs/evidence/learning/iterations`. Se corrigieron las etiquetas al reabrir y la recepción de clics del fondo, manteniendo la CSP.

Reproducción desde la raíz, con dependencias y modelo ya instalados:

```powershell
npm run build --prefix apps/web
npm test --prefix apps/web
node Tests/nova/preparation-modal.cjs
node Tests/nova/learning.cjs
```

El E2E del modal inicia y detiene su propia API en 5214, utiliza una SQLite de ejemplo y un contexto Edge separado. No controla la pestaña personal ni envía texto a proveedores externos. `--headed` muestra su navegador.
