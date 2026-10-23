# QuickPrompt NOVA

**Versión inicial 1.0.0 aceptada por el usuario.** Cierre con correcciones: `nova-v1.0.0-r1`. [Acta y límites](docs/releases/NOVA_1.0.0_R1.md). Se conserva el tag inicial `nova-v1.0.0`.

QuickPrompt guarda y organiza prompts, completa variables y permite copiarlos o usarlos con IA local. En NOVA eliges la acción; la IA ayuda con la búsqueda por significado y el contenido. Guardar, eliminar y trasladar texto a otro servicio conservan sus revisiones y confirmaciones.

## Ejecutar

Requiere .NET 8, Node/npm y las dependencias del frontend instaladas. Para IA se configuran una instalación existente de Adaptive-AI-Core, su Python y Ollama con `qwen3:8b`. El arranque no instala paquetes ni descarga modelos. [Configuración de Core](agents/nova-core/README.md).

Desde la raíz, con las dependencias existentes:

```powershell
./scripts/Start-Nova.ps1 -CorePython ../Adaptive-AI-Core/.venv/Scripts/python.exe -CoreSdkRoot ../Adaptive-AI-Core
```

Abre la URL indicada por el script, normalmente `http://127.0.0.1:5190`. `-Port` y `-DataPath` permiten una instancia explícita. La base privada y los perfiles del navegador no pertenecen al repositorio. La biblioteca manual funciona sin inferencia; las operaciones IA requieren su proveedor configurado y fallan de forma explícita si falta.

## Usar

1. Elige un prompt o crea uno con título y contenido. Los campos se escriben como `<nombre>`.
2. Completa los valores y revisa la vista previa antes de copiar o ejecutar.
3. En NOVA elige Crear, Actualizar, Buscar por significado u otra acción disponible.
4. Revisa el cambio antes de guardar una copia, una versión o eliminar.

[Guía de uso](docs/COMO_USAR_QUICKPROMPT.md) · [Documentación vigente](docs/README.md) · [Desarrollo y pruebas](docs/development/DEVELOPMENT.md).

## Arquitectura y contribución

- `apps/web`: React/TypeScript; composición de pantallas, formularios y controles.
- `backend`: API ASP.NET Core, casos de uso y adaptadores.
- `agents/nova-core`: adaptador de inferencia; no decide permisos ni escribe la biblioteca.
- `src`: dominio compartido y consumidor Blazor de compatibilidad.
- `apps/mobile`: aplicación MAUI conservada.
- `Tests`, `scripts`, `contracts`: pruebas, herramientas y contratos.

[Mapa de propietarios](docs/ARCHITECTURE.md) · [Componentes y formularios](docs/development/UI_COMPONENTS.md) · [Reglas para agentes](AGENTS.md).

Una página compone capacidades existentes. Las reglas, la validación de dominio y la persistencia tienen un propietario; no se copian para cada pantalla. Las abstracciones compartidas deben conservar diferencias reales y demostrar su uso en consumidores concretos.

## Estado y evidencia

Los resultados portables de esta revisión están en [su manifiesto](docs/releases/nova-v1.0.0-r1.json). Los logs, bases y capturas quedan locales según la [política de evidencia](docs/evidence/README.md). Una prueba estructural no demuestra comprensión semántica ni fiabilidad universal.

La documentación anterior a esta limpieza describe otros perfiles y se consulta como [historial](docs/archive/README.md). La interpretación automática libre rechazada no es la entrada principal del producto actual. No se afirma aceptación de todos los proveedores externos, paridad móvil completa o todos los criterios del expediente ampliado.
