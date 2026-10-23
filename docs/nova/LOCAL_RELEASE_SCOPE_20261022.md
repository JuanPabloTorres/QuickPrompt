# Decisión de alcance: versión local completa

El 22 de octubre de 2026 el usuario respondió «Certificar sólo la versión local completa» y, para la fidelidad de copia, «Ambas capacidades».

El perfil `local-v1` comprende QuickPrompt web local, Qwen configurado, Adaptive Core, el agente visible y Lab. Conserva todas las funciones y criterios locales de la matriz original. Incluye preparar destinos del catálogo sin enviar, copiar o abrir implícitamente; preparar Claude sigue siendo un criterio obligatorio.

Exclusiones autorizadas: EXT-02 (verificación autenticada en sitios externos) y EXT-03 (extensión MV3). No se declaran implementadas. La matriz histórica de 88 requisitos permanece intacta.

VAR-04 se desglosa en dos contratos independientes:

- `windows-unicode-crlf`: portapapeles nativo CF_UNICODETEXT conserva Unicode, contenido y líneas lógicas mediante CRLF de Windows.
- `utf8-lf-file`: descarga real de un archivo UTF-8 sin BOM, con LF, sin recortar espacios o líneas finales. Descargar no guarda en biblioteca, no registra un uso ni escribe el portapapeles.

La comparación histórica entre LF y los bytes CRLF del portapapeles sigue siendo FAIL bajo su contrato original. Este perfil no cambia ese resultado. Sólo acepta las dos nuevas capacidades con evidencia ejecutada y vinculada al backend y frontend probados. No se certifica toda la versión porque una de ellas pase.

No quedan excluidos los fallos semánticos, seguridad, migraciones, configuración, paridad local, documentación, pruebas integrales ni aceptación humana. Un estado COMPLETED del agente no prueba por sí solo el cumplimiento de la petición original.
