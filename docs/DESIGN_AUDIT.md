# Diseño: evidencia y adaptación web

Fuente: AppShell.xaml, Pages/MainPage.xaml, Pages/PromptBuilderPage.xaml, Features/Prompts/Pages, Resources/Styles/AppColors.xaml, Typography.xaml, Spacing.xaml, Borders.xaml. Inspección estática; no se ejecutó MAUI por falta de SDK/workloads.

| Elemento original comprobado | Adaptación |
|---|---|
| PrimaryBlueDark #23486A, Light #3B6790 | Navegación, acciones primarias, jerarquía |
| Teal #4C7B8B | Selección, chips, estados locales |
| Yellow #EFB036 | Identidad y copiar texto, con texto oscuro |
| Red #BE3144, gris #FBFBFB, blanco | Errores, superficies, tema claro |
| Bordes 5/10px, padding 4/5/10 | Radios 8/12/14px; controles web de al menos 44px |
| Escala espaciado 4/8/16/24/32/48/64 | Ritmo de controles, paneles y separación de secciones |
| Typography.xaml: Nasa21 12/13/15/16/18/22/28/40 | Nasa21 local en marca; OpenSans local para lectura; fuentes existentes del repo, sin CDN |
| Material Icons y tabs Quick/Create/Settings/AI | Biblioteca primaria, editor lateral y navegación lateral; IA contextual secundaria |
| Editor texto/chips, Lottie y tarjetas | Chips accesibles, vista dual y transiciones discretas; sin runtime de animaciones |

El informe histórico dice OpenSans e identidad #011F4B; los tokens efectivos son #23486A y Typography.xaml declara Nasa21. Se prioriza código efectivo sobre documentación histórica. Se moderniza legibilidad; no se declara copia píxel a píxel.

La habilidad ui-ux-pro-max se consultó mediante Python ya disponible del runtime. Primera búsqueda propuso landing de documentación (no adecuada); segunda búsqueda `prompt library workspace` propuso Minimalism & Swiss para herramientas. Solo se adoptan jerarquía, contraste/foco, separación y efectos sutiles; paleta y fuentes se conservan de QuickPrompt, sin imports remotos ni analytics recomendados por patrones genéricos.

Desktop: navegación izquierda, biblioteca central y detalle/editor derecho. <1150px: paneles apilados y tarjetas en dos columnas. <700px: navegación horizontal, una columna, controles grandes. Ctrl/Cmd+K usa dialog modal nativo dentro del navegador, Escape y foco atrapado por plataforma. Vista de chips selecciona la variable en el editor textual; selección de texto crea marcador. Respeta prefers-reduced-motion.

QA y capturas reales se registran en MVP_ACCEPTANCE y docs/evidence. No se consideran capturas conceptuales como prueba. La versión visible en el primer render se corrigió de texto literal Razor a interpolación explícita. El skip link se recorta mientras no tiene foco para evitar que aparezca en capturas de página completa al desplazarse.
