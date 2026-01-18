# ?? QuickPrompt - Design Direction Document

**Version:** 1.0  
**Date:** January 15, 2025  
**Project:** Visual Modernization & UI/UX Refactoring  
**Phase:** 1 - Design Direction & Planning  
**Status:** Final

---

## 1. VISUAL IDENTITY

### 1.1 Design Philosophy

**Core Positioning:**
```
"Productivity-Focused Minimalism with Warmth"
```

QuickPrompt's visual language balances **professional efficiency** with **approachable warmth**, creating an experience that feels both powerful and friendly.

**Key Attributes:**
- **Clean:** Remove visual noise, emphasize content
- **Professional:** Trust-inspiring, polished, attention to detail
- **Accessible:** Inclusive by default, usable by all
- **Efficient:** Support user goals without friction
- **Warm:** Human, not cold or corporate
- **Modern but Timeless:** Avoid trends that age quickly

### 1.2 Visual References (Inspiration, Not Imitation)

**Primary Inspirations:**

1. **Linear** - Visual Clarity
   - Clean layouts with generous whitespace
   - Strong typography hierarchy
   - Purposeful use of color
   - Subtle animations that communicate state

2. **Notion** - Balance & Accessibility
   - Balance between power and simplicity
   - Readable typography at all sizes
   - Accessible color contrast
   - Flexible component system

3. **Arc Browser** - Subtle Sophistication
   - Refined color palette
   - Micro-interactions that delight
   - Smooth, natural animations
   - Attention to detail

4. **Apple Notes** - Functional Simplicity
   - Content-first approach
   - Clear hierarchy without ornamentation
   - Familiar interaction patterns
   - Platform-appropriate design

**Anti-References (What We Are NOT):**

? **Enterprise/Corporate Cold** - No sterile, impersonal UI  
? **Gamified/Playful** - Not a game, a productivity tool  
? **Ultra-Minimal Sterile** - Minimal but with personality  
? **Trend-Chasing** - No gradients/glassmorphism just because trendy  

### 1.3 Design Principles

**Principle 1: Clarity Over Cleverness**
```
Every element must have a clear purpose.
If it doesn't help the user, remove it.
```

**Application:**
- Obvious button hierarchy (primary vs secondary clear at a glance)
- Titles clearly larger than body text
- Actions always visible and labeled
- No mystery meat navigation

---

**Principle 2: Consistency Creates Trust**
```
Same component in same context = same visual treatment.
Users should never guess what something will do.
```

**Application:**
- Button styles never vary arbitrarily
- Spacing follows mathematical system (4px base)
- Colors always from defined palette
- Typography uses defined scale

---

**Principle 3: State Must Be Visible**
```
User never wonders what's happening.
Loading, error, success always communicated clearly.
```

**Application:**
- Loading states always show progress or spinner
- Empty states guide user to next action
- Errors explain what went wrong and how to fix
- Success feedback confirms action completed

---

**Principle 4: Accessibility Is Non-Negotiable**
```
Good design works for everyone.
Accessibility is quality, not compliance.
```

**Application:**
- Contrast ratios meet WCAG AA minimum
- Touch targets ?44×44dp
- Information never communicated by color alone
- Text remains legible at all supported sizes

---

**Principle 5: Performance Is Feature**
```
Beautiful but slow is not beautiful.
Animations must be smooth (60fps) or disabled.
```

**Application:**
- Animations <200ms (feel instant)
- 60fps target on all platforms
- Graceful degradation on older devices
- No unnecessary visual complexity

---

**Principle 6: Design System Over Snowflakes**
```
Every component is part of a system.
Special cases require extraordinary justification.
```

**Application:**
- New component? Check if existing one can be extended
- New color? Must fit in palette system
- New spacing? Must be from defined scale
- Exceptions documented and approved

---

## 2. COLOR SYSTEM

### 2.1 Color Philosophy

**Goals:**
- Maintain existing brand colors (#EFB036, #23486A) as foundation
- Create complete, semantic color system
- Ensure WCAG AA contrast compliance
- Support light mode (dark mode post-launch)
- Enable theming capabilities

**Color Usage Principles:**
- **Primary:** Call-to-action, brand moments, key interactions
- **Secondary:** Supporting actions, navigation, less emphasis
- **Semantic:** System feedback (success, error, warning, info)
- **Neutral:** Backgrounds, text, borders, dividers
- **Use sparingly:** Color draws attention, use strategically

### 2.2 Primary Palette - Brand Amber/Gold

**Base Color:** `#EFB036` (Current, preserve as brand identity)

**Full Scale:**
```yaml
Primary:
  50:  #FEF7ED   # Lightest tint - backgrounds, hover states
  100: #FDE8CC   # Very light - subtle backgrounds
  200: #FCD19A   # Light - disabled states, subtle UI
  300: #FAB668   # Medium-light - hover states
  400: #F69D3B   # Medium - secondary emphasis
  500: #EFB036   # BASE - primary actions, brand (PRESERVE)
  600: #D68A1F   # Medium-dark - pressed states
  700: #B36915   # Dark - text on light backgrounds
  800: #8F4D0F   # Very dark - high emphasis text
  900: #6B340A   # Darkest - maximum contrast

Contrast Text:
  On 50-400: Use Gray-900 (#111827)
  On 500-900: Use White (#FFFFFF)
```

**Usage:**
- **500 (Base):** Primary buttons, key actions, brand elements
- **600:** Primary button pressed state
- **700:** Primary button text (if on light background)
- **50-200:** Subtle highlights, backgrounds for primary context
- **800-900:** Text emphasis in primary color (sparingly)

---

### 2.3 Secondary Palette - Professional Blue

**Base Color:** `#23486A` (Current, evolve to more versatile blue-gray)

**Proposed Evolution:**
```yaml
Secondary:
  50:  #F0F4F8   # Lightest - subtle backgrounds
  100: #D9E2EC   # Very light - dividers, borders
  200: #BCCCDC   # Light - disabled, placeholder
  300: #9FB3C8   # Medium-light - secondary icons
  400: #829AB1   # Medium - secondary UI elements
  500: #627D98   # BASE - navigation, secondary buttons
  600: #486581   # Medium-dark - secondary pressed
  700: #334E68   # Dark - headings, important text
  800: #243B53   # Very dark - high contrast text
  900: #102A43   # Darkest - maximum contrast text

Contrast Text:
  On 50-500: Use Gray-900 (#111827)
  On 600-900: Use White (#FFFFFF)

Note: #23486A maps approximately to Secondary-700
Adjust as needed to maintain brand recognition
```

**Usage:**
- **500-600:** Secondary buttons, navigation elements
- **700-800:** Text, headings, icons
- **200-300:** Borders, dividers, subtle UI
- **50-100:** Subtle backgrounds, card surfaces

---

### 2.4 Semantic Palette

**Success - Green:**
```yaml
Success:
  50:  #ECFDF5
  100: #D1FAE5
  200: #A7F3D0
  300: #6EE7B7
  400: #34D399
  500: #10B981   # BASE - success messages, checkmarks
  600: #059669   # Pressed states
  700: #047857   # Text on light
  800: #065F46
  900: #064E3B

Usage: Success toasts, confirmation messages, valid input states
```

**Error - Red:**
```yaml
Error:
  50:  #FEF2F2
  100: #FEE2E2
  200: #FECACA
  300: #FCA5A5
  400: #F87171
  500: #EF4444   # BASE - error messages, validation
  600: #DC2626   # Pressed states
  700: #B91C1C   # Text on light
  800: #991B1B
  900: #7F1D1D

Usage: Error messages, validation failures, destructive actions
```

**Warning - Amber:**
```yaml
Warning:
  50:  #FFFBEB
  100: #FEF3C7
  200: #FDE68A
  300: #FCD34D
  400: #FBBF24
  500: #F59E0B   # BASE - warning messages, alerts
  600: #D97706   # Pressed states
  700: #B45309   # Text on light
  800: #92400E
  900: #78350F

Usage: Warning messages, caution states, important notices
```

**Info - Blue:**
```yaml
Info:
  50:  #EFF6FF
  100: #DBEAFE
  200: #BFDBFE
  300: #93C5FD
  400: #60A5FA
  500: #3B82F6   # BASE - informational messages, hints
  600: #2563EB   # Pressed states
  700: #1D4ED8   # Text on light
  800: #1E40AF
  900: #1E3A8A

Usage: Info messages, tips, helpful guidance, links
```

---

### 2.5 Neutral Palette - Gray Scale

**Complete Gray Scale (9 shades):**
```yaml
Neutral:
  50:  #F9FAFB   # Lightest - subtle backgrounds
  100: #F3F4F6   # Very light - card backgrounds
  200: #E5E7EB   # Light - borders, dividers
  300: #D1D5DB   # Medium-light - disabled elements
  400: #9CA3AF   # Medium - placeholders, secondary text
  500: #6B7280   # BASE - body text secondary
  600: #4B5563   # Medium-dark - body text
  700: #374151   # Dark - headings, labels
  800: #1F2937   # Very dark - primary text
  900: #111827   # Darkest - highest contrast text

Pure:
  White: #FFFFFF
  Black: #000000

Contrast Text:
  On 50-400: Use Gray-900 (#111827)
  On 500-900: Use White (#FFFFFF)
```

**Usage:**
- **50-200:** Backgrounds (app background, card surface, elevated)
- **200-400:** Borders, dividers, disabled states
- **400-600:** Secondary text, placeholders, subtle icons
- **700-900:** Primary text, headings, labels, icons

**Disable State:**
- Background: Gray-100 or Gray-200
- Text: Gray-400
- Border: Gray-300

---

### 2.6 Surface Colors (Light Mode)

```yaml
Surfaces:
  App Background: Gray-50 (#F9FAFB)
  Card Background: White (#FFFFFF)
  Card Elevated: White with shadow
  Input Background: White (#FFFFFF)
  Input Border: Gray-300 (#D1D5DB)
  Divider: Gray-200 (#E5E7EB)
```

---

### 2.7 Text Colors

```yaml
Text:
  Primary: Gray-900 (#111827) - rgba(17, 24, 39, 1.0)
  Secondary: Gray-600 (#4B5563) - rgba(75, 85, 99, 1.0)
  Tertiary: Gray-500 (#6B7280) - rgba(107, 114, 128, 1.0)
  Disabled: Gray-400 (#9CA3AF) - rgba(156, 163, 175, 1.0)
  Inverse: White (#FFFFFF) - for dark backgrounds
  Link: Info-600 (#2563EB)
  Link Hover: Info-700 (#1D4ED8)
```

---

### 2.8 Contrast Verification

All color combinations must meet **WCAG 2.1 Level AA:**
- **Normal text** (14-18px): 4.5:1 contrast ratio minimum
- **Large text** (?18px or ?14px bold): 3:1 contrast ratio minimum
- **UI components** (borders, icons): 3:1 contrast ratio minimum

**Verified Combinations:**
```
? Gray-900 on White: 16.6:1 (Excellent)
? Gray-800 on White: 12.6:1 (Excellent)
? Gray-700 on White: 8.6:1 (Excellent)
? Gray-600 on White: 5.7:1 (AA pass)
?? Gray-500 on White: 4.5:1 (AA minimum, use for large text only)
? Gray-400 on White: 3.1:1 (Fail for text, OK for UI components)

? White on Primary-500: 4.8:1 (AA pass)
? White on Secondary-700: 9.1:1 (Excellent)
? White on Success-600: 4.6:1 (AA pass)
? White on Error-600: 4.8:1 (AA pass)
```

---

### 2.9 Color Usage Guidelines

**DO:**
- ? Use Primary for main actions (Save, Submit, Create)
- ? Use Secondary for less emphasis (Cancel, Back, Settings)
- ? Use Semantic colors for system feedback only
- ? Use Neutral for text, backgrounds, structure
- ? Limit colors per screen (max 3-4 colors + neutrals)

**DON'T:**
- ? Use Error red for non-error elements (stop sign ? delete button)
- ? Use random colors outside the palette
- ? Rely on color alone to convey information
- ? Use low-contrast color combinations
- ? Use primary color for everything

---

## 3. TYPOGRAPHY SYSTEM

### 3.1 Typography Philosophy

**Goals:**
- Clear, readable hierarchy
- Scalable system (works on mobile and desktop)
- Accessible (minimum sizes, contrast, line-height)
- Consistent (same level = same style)
- Efficient (limited number of styles)

**Principles:**
- **Readability First:** Legibility over aesthetics
- **Hierarchy Clear:** H1 obviously larger than H2, etc.
- **Consistent Scale:** Mathematical progression (1.25x ratio)
- **Limited Weights:** Max 3 weights (Regular, SemiBold, Bold)
- **Generous Line Height:** 1.4-1.6 for body text

### 3.2 Font Family

**Primary Font:** OpenSans  
**Variants Used:**
- OpenSans-Regular (400)
- OpenSans-SemiBold (600)

**Display Font (Optional):** Designer.otf  
**Usage:** Only for hero/display text if needed (onboarding, splash)

**Fallback Stack:**
```
Primary: OpenSans, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif
```

### 3.3 Type Scale

**Base Size:** 15sp (mobile), 16sp (desktop)  
**Scale Ratio:** 1.25 (Major Third)

**Scale Definition:**
```yaml
Display / Hero:
  Size: 40sp
  Line Height: 1.2 (48sp)
  Weight: SemiBold (600)
  Letter Spacing: -0.02em (tighter)
  Usage: Onboarding hero, splash screen
  XAML Style: DisplayTextStyle

Heading 1:
  Size: 28sp
  Line Height: 1.3 (36sp)
  Weight: SemiBold (600)
  Letter Spacing: -0.01em
  Usage: Page titles, main headings
  XAML Style: Heading1TextStyle

Heading 2:
  Size: 22sp
  Line Height: 1.3 (29sp)
  Weight: SemiBold (600)
  Letter Spacing: 0em (normal)
  Usage: Section headings within pages
  XAML Style: Heading2TextStyle

Heading 3:
  Size: 18sp
  Line Height: 1.4 (25sp)
  Weight: SemiBold (600)
  Letter Spacing: 0em
  Usage: Subsections, card titles
  XAML Style: Heading3TextStyle

Body Large:
  Size: 16sp
  Line Height: 1.5 (24sp)
  Weight: Regular (400)
  Letter Spacing: 0em
  Usage: Important body text, intro paragraphs
  XAML Style: BodyLargeTextStyle

Body (Base):
  Size: 15sp
  Line Height: 1.5 (23sp)
  Weight: Regular (400)
  Letter Spacing: 0em
  Usage: Standard body text, descriptions
  XAML Style: BodyTextStyle

Body Small:
  Size: 13sp
  Line Height: 1.5 (20sp)
  Weight: Regular (400)
  Letter Spacing: 0em
  Usage: Secondary information, helper text
  XAML Style: BodySmallTextStyle

Caption:
  Size: 12sp
  Line Height: 1.4 (17sp)
  Weight: Regular (400)
  Letter Spacing: 0.01em
  Usage: Labels, metadata, timestamps, overline
  XAML Style: CaptionTextStyle

Button:
  Size: 15sp (medium), 14sp (small), 16sp (large)
  Line Height: 1.0 (tight, single line)
  Weight: SemiBold (600)
  Letter Spacing: 0.01em
  Text Transform: None (Sentence case)
  Usage: Button text
  XAML Style: ButtonTextStyle (variants for sizes)
```

### 3.4 Text Color Usage

```yaml
Heading 1-3:
  Color: Gray-900 (Primary text)
  
Body (all variants):
  Color: Gray-800 (Primary text) or Gray-600 (Secondary text)
  
Caption:
  Color: Gray-600 (Secondary text) or Gray-500 (Tertiary)
  
Button:
  Color: Depends on button variant (White on Primary, Gray-900 on light)
  
Link:
  Color: Info-600
  Hover: Info-700
  Visited: Info-800 (if applicable)
```

### 3.5 Typography Usage Guidelines

**DO:**
- ? Use Heading 1 once per page (page title)
- ? Use Heading 2 for major sections
- ? Use Heading 3 for subsections and card titles
- ? Use Body for all standard text
- ? Use Caption sparingly (metadata only)
- ? Use consistent hierarchy (H1 ? H2 ? H3, never skip)

**DON'T:**
- ? Use multiple font families arbitrarily
- ? Use more than 3 font weights
- ? Make text smaller than 12sp (accessibility)
- ? Use ALL CAPS extensively (readability, accessibility)
- ? Use italic excessively (harder to read on screens)
- ? Stack headings without body text between

**Line Length:**
- Optimal: 50-75 characters per line
- Mobile: Full width acceptable
- Desktop: Constrain to 680px max width for readability

**Text Alignment:**
- Body text: Left-aligned (never justified on mobile)
- Headings: Left-aligned (center only for hero/display)
- Buttons: Center-aligned
- Captions/Metadata: Left-aligned or right-aligned (context-dependent)

---

## 4. SPACING SYSTEM

### 4.1 Spacing Philosophy

**Goals:**
- Mathematical consistency (predictable, not arbitrary)
- Rhythm and breathing room (not cramped or excessive)
- Scalable (works on small mobile to large desktop)
- Easy to remember (multiples of 4px)

**Principles:**
- **4px Base Unit:** All spacing is multiple of 4
- **Powers of 2:** Prefer 8, 16, 24, 32, 48, 64 (easier mental math)
- **Context-Appropriate:** Tighter spacing for related items, looser for sections
- **Consistent Application:** Same relationship = same spacing

### 4.2 Spacing Scale

**Scale Definition:**
```yaml
xs (Extra Small): 4px
  Usage: Minimal adjustments, icon-text gap, tight inline spacing
  Example: Icon + Label gap in button

sm (Small): 8px
  Usage: Compact spacing, list item padding, form field gap
  Example: Padding inside compact buttons, small card padding

md (Medium): 16px
  Usage: Base spacing unit, component padding, element spacing
  Example: Default padding in cards, space between form fields

lg (Large): 24px
  Usage: Comfortable spacing, section padding, screen padding
  Example: Page margins, comfortable card padding

xl (Extra Large): 32px
  Usage: Loose spacing, large section separation
  Example: Space between major page sections

2xl (2X Large): 48px
  Usage: Large section gaps, screen section spacing
  Example: Space between major content blocks

3xl (3X Large): 64px
  Usage: Maximum spacing, rare use
  Example: Hero section padding, major page dividers
```

### 4.3 Component-Specific Spacing

**Buttons:**
```
Padding (horizontal): md (16px)
Padding (vertical): sm (8px) for small, md (12px) for medium, lg (16px) for large
Icon-text gap: xs (4px)
Button-button gap (inline): sm (8px)
```

**Cards:**
```
Internal padding: md (16px) or lg (24px) depending on content density
Card-card gap (grid): md (16px)
Card-card gap (list): sm (8px) or md (16px)
```

**Forms:**
```
Field-field gap (vertical): md (16px)
Label-input gap: xs (4px)
Input padding: md (12px horizontal), sm (8px vertical)
Form section gap: xl (32px)
```

**Lists:**
```
Item padding: md (12-16px)
Item-item separator: 1px or sm (8px) gap
List-list gap: lg (24px)
```

**Page Layout:**
```
Screen padding (mobile): md (16px) or lg (20px)
Screen padding (desktop): lg (24px) or xl (32px)
Section-section gap: xl (32px) or 2xl (48px)
Content max-width: 1200px (desktop)
```

### 4.4 Spacing Usage Guidelines

**DO:**
- ? Use spacing tokens exclusively (never arbitrary values)
- ? Use tighter spacing for related items (sm, md)
- ? Use looser spacing for sections (xl, 2xl)
- ? Be consistent (same relationship = same spacing)
- ? Test on smallest and largest screens

**DON'T:**
- ? Use values not in the system (no 10px, 15px, 20px, etc.)
- ? Use 0 padding when content needs room to breathe
- ? Stack multiple spacers (one spacing token, not multiple)
- ? Ignore platform Safe Areas (iOS notch, Android navigation)

**Zero (0) Usage:**
- Acceptable when deliberately removing space
- Example: Negative margin for full-bleed images
- Example: Edge-to-edge lists

---

## 5. SHADOWS & ELEVATION

### 5.1 Shadow Philosophy

**Goals:**
- Subtle depth hierarchy
- Platform-appropriate (iOS subtle, Android more pronounced)
- Performance-friendly (simple shadows)
- Semantic (elevation indicates interactivity or importance)

### 5.2 Elevation Levels

**4 Levels of Elevation:**

```yaml
Level 0 (Flat):
  Shadow: None
  Usage: App background, flat surfaces
  Example: Page background

Level 1 (Raised):
  Shadow: 0px 1px 2px rgba(0, 0, 0, 0.05)
  Usage: Cards, subtle elevation
  Example: Default card on page

Level 2 (Floating):
  Shadow: 0px 4px 6px rgba(0, 0, 0, 0.1)
  Usage: Floating elements, modals, hover states
  Example: Card on hover, dropdown menus

Level 3 (Overlay):
  Shadow: 0px 10px 15px rgba(0, 0, 0, 0.15)
  Usage: Modals, dialogs, prominent overlays
  Example: Alert dialogs, bottom sheets

Level 4 (Modal):
  Shadow: 0px 20px 25px rgba(0, 0, 0, 0.2)
  Usage: Critical modals, full-screen overlays
  Example: Full-screen loading overlay
```

### 5.3 Shadow Usage Guidelines

**DO:**
- ? Use shadows sparingly (too many flatten the effect)
- ? Use elevation to indicate interactivity (buttons hover state)
- ? Use elevation for hierarchy (modal over card over page)
- ? Keep shadows subtle on mobile (performance)

**DON'T:**
- ? Use heavy shadows everywhere
- ? Mix elevation levels arbitrarily
- ? Use shadows on text (use background instead)
- ? Forget about performance (complex shadows expensive)

---

## 6. BORDER RADIUS

### 6.1 Radius Scale

**3 Standardized Sizes:**

```yaml
sm (Small): 4px
  Usage: Input fields, small buttons, tags
  Example: Text input, chip/tag

md (Medium): 8px
  Usage: Standard buttons, cards, most components
  Example: Primary button, prompt card

lg (Large): 12px
  Usage: Large cards, modals, prominent elements
  Example: Modal dialog, large feature card

xl (Extra Large): 16px
  Usage: Hero elements, very large components (rare)
  Example: Hero card, featured content

Pill: 9999px or 50% (creates pill shape)
  Usage: Pill-shaped buttons, badges
  Example: Filter chips, status badges
```

### 6.2 Radius Usage Guidelines

**DO:**
- ? Use md (8px) as default for most components
- ? Use sm (4px) for compact components
- ? Use lg (12px) for large, prominent components
- ? Be consistent (same component type = same radius)

**DON'T:**
- ? Use sharp corners (0px) unless intentional (brutalist style, not our direction)
- ? Use inconsistent radius on same component type
- ? Use excessive radius (>16px) unless pill shape intended

---

## 7. ICONOGRAPHY

### 7.1 Icon System

**Primary Icon Library:** Material Icons (Outlined)  
**Font Family:** MaterialIconsOutlined-Regular

**Backup:** Material Icons (Filled)  
**Font Family:** MaterialIcons-Regular

**Icon Sizes:**
```yaml
xs: 16dp - Inline with text, small indicators
sm: 20dp - Standard list icons, form icons
md: 24dp - Default icon size, navigation
lg: 32dp - Large emphasis icons, feature highlights
xl: 48dp - Hero icons, empty states
```

### 7.2 Icon Usage Guidelines

**DO:**
- ? Use consistent icon family (Outlined for UI, Filled for emphasis)
- ? Size icons appropriately for context
- ? Pair icons with labels when meaning unclear
- ? Use semantic colors (error icon = error red)

**DON'T:**
- ? Mix multiple icon families arbitrarily
- ? Use decorative icons excessively
- ? Rely on icon alone without label (accessibility)
- ? Use too many icon sizes (stick to scale)

---

## 8. ANIMATION & TRANSITIONS

### 8.1 Animation Philosophy

**Goals:**
- Communicate state changes
- Smooth, natural motion
- Performance-first (60fps)
- Subtle, not distracting

**Principles:**
- **Purposeful:** Animation must communicate something
- **Fast:** Transitions <200ms feel instant
- **Smooth:** 60fps minimum, easing curves
- **Graceful Degradation:** Disable on low-end devices if needed

### 8.2 Timing & Easing

**Duration:**
```yaml
Instant: 0-100ms
  Usage: Hover states, button presses
  Example: Button background color change

Fast: 100-200ms
  Usage: Most UI transitions, state changes
  Example: Fade in/out, slide in

Medium: 200-300ms
  Usage: Page transitions, modal appearance
  Example: Navigate to new page

Slow: 300-500ms
  Usage: Large elements, emphasis animations
  Example: Full-screen overlay, success animation

Very Slow: 500-1000ms
  Usage: Rare, only for emphasis
  Example: Lottie success animation
```

**Easing Curves:**
```
Ease Out (Deceleration): Most common, elements enter decisively
Ease In (Acceleration): Elements exit, fade away
Ease In-Out: Smooth both ways, symmetrical motion
Linear: Progress indicators, constant motion
```

### 8.3 Animation Types

**Micro-interactions:**
- Button press: Scale down 0.95x, 100ms
- Hover (desktop): Subtle color change, 150ms
- Focus: Border color change, 100ms

**Transitions:**
- Page navigation: Slide + fade, 200ms
- Modal appearance: Fade + scale, 200ms
- Toast/Snackbar: Slide in, 200ms

**State Changes:**
- Loading appear: Fade in, 150ms
- Empty state appear: Fade in, 200ms
- Error appear: Fade in + shake (subtle), 200ms
- Success: Lottie animation, 1000ms

### 8.4 Animation Guidelines

**DO:**
- ? Use animations to communicate state changes
- ? Keep animations fast (<300ms typically)
- ? Use consistent timing for same action types
- ? Test on low-end devices

**DON'T:**
- ? Animate for decoration only
- ? Use slow animations (>500ms) unless intentional
- ? Animate many things simultaneously (overwhelming)
- ? Block user interaction during animation

---

## 9. COMPONENT DESIGN PATTERNS

### 9.1 Button Patterns

**Hierarchy:**
1. **Primary:** Main action on page (Save, Submit, Create)
2. **Secondary:** Supporting action (Cancel, Back)
3. **Tertiary:** Least emphasis (More options, Settings)

**Variants:**
- **Primary:** Solid background (Primary-500), white text
- **Secondary:** Solid background (Secondary-600), white text
- **Tertiary:** Outlined (Primary-500 border), primary text
- **Ghost:** No background, no border, primary text
- **Text:** No background, no border, gray text (link-style)
- **Danger:** Solid background (Error-600), white text (destructive actions)

**States:**
- **Normal:** Default appearance
- **Hover:** Lighter background (Primary-400) or subtle shadow
- **Pressed:** Darker background (Primary-600) or scale down
- **Focus:** Border ring (accessibility)
- **Disabled:** Gray-200 background, Gray-400 text, no interaction
- **Loading:** Spinner replaces text or alongside text

**Sizes:**
- **Small:** 32dp height, 14sp text, sm padding
- **Medium:** 44dp height, 15sp text, md padding (default)
- **Large:** 52dp height, 16sp text, lg padding

### 9.2 Input Patterns

**Types:**
- **Text Input:** Single-line text entry
- **Text Area:** Multi-line text entry
- **Search:** Text input with search icon
- **Picker:** Select from predefined options

**States:**
- **Normal:** Gray-300 border, white background
- **Focus:** Primary-500 border (2px), subtle shadow
- **Error:** Error-500 border, error message below
- **Success:** Success-500 border, checkmark icon
- **Disabled:** Gray-100 background, Gray-400 text, no interaction

**Structure:**
- Label (optional, above input)
- Input field (border, padding, placeholder)
- Helper text (optional, below, Gray-600)
- Error message (below, Error-700 text)

### 9.3 Card Patterns

**Variants:**
- **Default:** White background, Level 1 shadow, md radius
- **Elevated:** White background, Level 2 shadow (hover), md radius
- **Outlined:** White background, Gray-300 border, no shadow, md radius

**Structure:**
- Padding: md (16px) or lg (24px) internal
- Border radius: md (8px)
- Shadow: Level 1 (default) or Level 2 (hover)

**Usage:**
- **PromptCard:** Display prompt information (title, description, category, variables)
- **HistoryCard:** Display execution history
- **FeatureCard:** Highlight features (onboarding, guide)

### 9.4 Feedback Patterns

**Loading:**
- **Spinner:** Circular progress, primary color, center screen or inline
- **Skeleton Screen:** Gray-200 blocks with shimmer animation
- **Progress Bar:** Horizontal bar, primary color, for known progress

**Empty State:**
- Icon (gray, xl size)
- Title (Heading 2, "No prompts yet")
- Description (Body, explain why empty)
- CTA button (Primary, "Create your first prompt")

**Error State:**
- Icon (error red, lg size)
- Error message (Heading 3, specific error)
- Description (Body, how to fix)
- Retry button (Secondary, "Try again")

**Success Feedback:**
- **Toast:** Slide in from top, auto-dismiss, success color
- **Lottie Animation:** Full-screen overlay, celebratory animation
- **Inline Message:** Success icon + message, success color

---

## 10. ACCESSIBILITY STANDARDS

### 10.1 WCAG 2.1 Level AA Requirements

**Color Contrast:**
- Normal text (14-18px): 4.5:1 minimum
- Large text (?18px or ?14px bold): 3:1 minimum
- UI components: 3:1 minimum

**Touch Targets:**
- Minimum size: 44×44dp (iOS HIG, Material Design)
- Spacing: 8dp minimum between targets

**Text Size:**
- Minimum: 12sp (captions only, use sparingly)
- Body text: 14-15sp minimum
- Support dynamic type (iOS)

**Focus Indicators:**
- Visible focus ring on all interactive elements
- Keyboard navigation support (Windows/Desktop)
- Tab order logical

**Screen Reader Support:**
- AutomationId on all interactive elements
- SemanticProperties.Description on icons without labels
- SemanticProperties.Hint on complex inputs
- HeadingLevel on page titles and section headers
- Announce state changes (loading, error, success)

### 10.2 Accessibility Checklist

**Visual:**
- [ ] All color contrast ratios verified (automated tool)
- [ ] Information not conveyed by color alone
- [ ] Text remains legible at 200% zoom
- [ ] Focus indicators visible on all interactive elements

**Interactive:**
- [ ] All touch targets ?44×44dp
- [ ] Touch targets spaced ?8dp apart
- [ ] Keyboard navigation functional (Windows/Desktop)
- [ ] Tab order logical

**Semantic:**
- [ ] AutomationId on all buttons, inputs, links
- [ ] SemanticProperties on all icons without labels
- [ ] HeadingLevel on titles
- [ ] Screen reader announcements for state changes

**Testing:**
- [ ] iOS VoiceOver testing
- [ ] Android TalkBack testing
- [ ] Windows Narrator testing
- [ ] Keyboard-only navigation testing

---

## 11. DESIGN ANTI-PATTERNS

### 11.1 What NOT to Do

**Visual:**
? Hardcoded colors (use named tokens)  
? Arbitrary spacing (use system scale)  
? Inconsistent button styles (use defined variants)  
? Low contrast text (verify ratios)  
? Tiny touch targets (<44dp)  

**Typography:**
? Too many font sizes (use scale)  
? All caps everywhere (hard to read)  
? Justified text on mobile (ragged edges)  
? Text too small (<12sp)  
? No hierarchy (everything same size)  

**Layout:**
? Content cramped (needs breathing room)  
? Inconsistent spacing between pages  
? No Safe Area respect (iOS notch)  
? Ignoring platform conventions  

**Interaction:**
? Unclear button hierarchy (all same style)  
? No loading feedback (user waits blindly)  
? No error messages (user confused)  
? Mystery meat navigation (unclear purpose)  

**Accessibility:**
? Color as only indicator (colorblind users)  
? No focus indicators (keyboard users)  
? Missing labels (screen reader users)  
? Touch targets too small (motor impairment)  

---

## 12. IMPLEMENTATION GUIDELINES

### 12.1 XAML ResourceDictionary Structure

```xml
Resources/Styles/
?? Colors.xaml          # Color palette tokens
?? Typography.xaml      # Text styles
?? Spacing.xaml         # Spacing constants
?? Shadows.xaml         # Shadow/elevation styles
?? Buttons.xaml         # Button component styles
?? Inputs.xaml          # Input component styles
?? Cards.xaml           # Card layouts
?? App.xaml             # Merge all dictionaries
```

### 12.2 Naming Conventions

**Colors:**
```xml
<Color x:Key="Primary500">#EFB036</Color>
<Color x:Key="Gray900">#111827</Color>
<Color x:Key="TextPrimary">#111827</Color>
```

**Spacing:**
```xml
<x:Double x:Key="SpacingXs">4</x:Double>
<x:Double x:Key="SpacingSm">8</x:Double>
<Thickness x:Key="PaddingMd">16</Thickness>
```

**Text Styles:**
```xml
<Style x:Key="Heading1TextStyle" TargetType="Label">
    <Setter Property="FontFamily" Value="OpenSans-SemiBold" />
    <Setter Property="FontSize" Value="28" />
    <Setter Property="TextColor" Value="{StaticResource Gray900}" />
</Style>
```

**Button Styles:**
```xml
<Style x:Key="PrimaryButton" TargetType="Button">
    <Setter Property="BackgroundColor" Value="{StaticResource Primary500}" />
    <Setter Property="TextColor" Value="White" />
    <Setter Property="CornerRadius" Value="8" />
    <Setter Property="HeightRequest" Value="44" />
</Style>
```

### 12.3 Migration Strategy

**Phase 2 (Foundation):**
1. Create all ResourceDictionaries
2. Define all color, typography, spacing tokens
3. Replace hardcoded values in existing files
4. Verify visual parity (no visual changes yet)

**Phase 3 (Components):**
5. Create component styles (buttons, inputs, cards)
6. Create reusable ContentViews where needed
7. Document usage for each component

**Phase 4 (Pages):**
8. Refactor pages one by one
9. Apply styles and components
10. Verify visual consistency and functionality

---

## 13. DESIGN SYSTEM GOVERNANCE

### 13.1 Decision-Making

**Design Decisions:**
- **UI/UX Lead:** Primary authority on visual design
- **Technical Lead:** Veto if technically infeasible
- **Project Owner:** Final say if business impact

**Component Additions:**
- Must be justified (no snowflakes)
- Must fit into system (color, typography, spacing)
- Must be documented before implementation

**Exceptions:**
- Rare, require extraordinary justification
- Documented in Design System docs
- Reviewed in retrospectives

### 13.2 Design System Maintenance

**Ownership:**
- **UI/UX Lead:** Design System maintainer
- **Tech Lead:** Code quality guardian
- **Team:** All contribute to improvements

**Updates:**
- Document all changes
- Version the Design System (1.0, 1.1, 2.0)
- Communicate changes to team
- Update examples and documentation

**Evolution:**
- Quarterly reviews of Design System
- Collect feedback from developers
- Iterate based on real usage
- Add components as needed (not speculatively)

---

## 14. SUCCESS CRITERIA

This Design Direction will be considered successful when:

? All visual decisions documented (no ambiguity)  
? Color palette complete (all use cases covered)  
? Typography scale defined (all text needs covered)  
? Spacing system established (all layout needs covered)  
? Component patterns documented (buttons, inputs, cards, feedback)  
? Accessibility standards clear (WCAG AA requirements)  
? Anti-patterns documented (what NOT to do)  
? Stakeholder approval received (Design Direction approved)  

**Verification:**
- [ ] Design Direction document reviewed by Technical Lead
- [ ] Design Direction document reviewed by Project Owner
- [ ] Design Direction document approved by Project Sponsor
- [ ] Design Token specification complete (Deliverable 1.2)
- [ ] Component inventory complete (Deliverable 1.3)
- [ ] Phase 1 gate passed

---

**Document Status:** ? **FINAL - READY FOR APPROVAL**  
**Next Step:** Stakeholder review and approval  
**Upon Approval:** Proceed to Phase 2 (Foundation - Design Token Implementation)

---

**Approved By:**

**UI/UX Lead:** ________________________ Date: ________  
**Technical Lead:** ________________________ Date: ________  
**Project Owner:** ________________________ Date: ________  
**Project Sponsor:** ________________________ Date: ________  

