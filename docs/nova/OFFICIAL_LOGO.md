# Logo oficial QuickPrompt NOVA

Se integra el PNG transparente proporcionado por el usuario el 17 de octubre de 2026. Sustituye al isotipo Orbit de la etapa anterior. Se conservan los colores, las proporciones y los píxeles originales; las copias de distribución tienen el mismo SHA-256.

- Original de referencia: [quickprompt_official.png](../../Resources/Branding/quickprompt_official.png), 1.254 × 1.254 px, 721.449 bytes.
- SHA-256: `6c173f78c9be2d515b39b4ea3b1aff4cfe0ab973b607e4930442864d1891f17c`.
- Autoridad para utilizarlo: solicitud expresa del usuario. No se atribuye una licencia de terceros ni se afirma que sea una fuente vectorial.

## Puntos de integración

| Superficie | Implementación |
|---|---|
| Cabecera, botón circular, panel de NOVA | `NovaLogo`, misma URL PNG compartida |
| Cursor e indicadores de trabajo | Mismo componente; anillo de estado externo, arte sin recolorear ni girar |
| Acciones de asistencia | Logo oficial junto a «Mejorar/Consultar con NOVA» |
| Favicon y acceso de inicio web | PNG original en React y Blazor; rutas favicon.svg existentes contienen ese mismo PNG incrustado |
| Web Blazor anterior | Cabecera y hojas de estilo actualizadas; copia PNG idéntica |
| Icono y splash MAUI | Recursos PNG y referencias MauiIcon/MauiSplashScreen actualizados |
| Plancha de tamaños | [Preview local](NOVA_ORBIT_PREVIEW.html), ambos fondos y tamaños 16/24/32/64/128 |

La identidad de los estados sigue usando los eventos reales del sistema. Las etiquetas accesibles y los iconos de acciones funcionales (copiar, guardar, eliminar, etc.) conservan su función. Los logos repetidos comparten una URL local; no hay servicios de imágenes ni recursos remotos nuevos.

## Validación

- Build TypeScript/Vite correcto; se conserva el aviso de tamaño del chunk principal ya existente.
- React: 77/77 pruebas. Web Blazor .NET 8: 150/150 pruebas con output aislado.
- [Edge real](../evidence/identity-after-1792282041643/validation.json): claro/oscuro, 320/390/768/1024/1440 px, drawer, formularios, navegación, copia, creación/guardado, proveedores, errores, teclado y movimiento reducido. Datos exclusivamente sintéticos.
- [Móvil claro](../evidence/identity-after-1792282041643/assistant-light-320.png), [escritorio oscuro](../evidence/identity-after-1792282041643/discover-dark-1440.png), [escala de tamaños](../evidence/identity-after-1792282041643/orbit-sizes.png).
- MAUI queda actualizado en sus fuentes y recursos. No se compiló el binario de escritorio: necesita el SDK .NET 9 ausente en este equipo. No se instaló otro SDK.

Las capturas antiguas de aceptación y los videos ya grabados se conservan como evidencia histórica; no se alteran sus píxeles. Los assets Orbit anteriores ya no forman parte de la aplicación activa.

No se modificaron el backend NOVA, el ejecutor, la biblioteca privada ni dependencias. No se crearon commits ni se publicaron cambios.
