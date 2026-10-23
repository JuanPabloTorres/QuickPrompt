# Agentes

`nova-core/` contiene el adaptador de NOVA al SDK existente de Adaptive AI Core,
su puente de lectura, configuración de ejemplo, lanzadores y pruebas de contrato.
El SDK compartido permanece en el repositorio hermano `Adaptive-AI-Core`.

La API compila objetivos y autoriza efectos; React opera los controles reales y
devuelve observaciones. El worker Python interpreta el encargo y no escribe en
la biblioteca. Los contratos y revisiones existentes conservan su propietario.

Desde la raíz:

```powershell
python agents/nova-core/test_bridge.py -v
python agents/nova-core/test_worker.py -v
powershell -File agents/nova-core/start-nova.ps1
```

El lanzador exige artefactos compilados e intérprete/SDK existentes. La tarea de
VS Code **NOVA: iniciar con Adaptive Core y Qwen local** prepara API/web primero.
No hay fallback silencioso a otro modelo o proveedor.
