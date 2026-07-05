# IMPLEMENATION PLAN V1.1.0 PHASE 3 - UI RENOVATION

## Goal Description
Renovate the entire User Interface of SelectML to a modern, "SaaS-like" Design System. The goal is to move away from standard Windows WPF controls to a custom-styled look with **Rounded Corners (Radius 6)**, **Subtle Shadows**, and a **Professional Color Palette** (Dark/Light modes).

## User Review Required
> [!IMPORTANT]
> **Theme Strategy**: We are moving from literal brush keys (e.g., `AppBackgroundBrush`) to a more semantic naming convention (e.g., `Brush.Background.Base`, `Brush.Surface.Card`, `Brush.Text.Primary`). This requires updating all XAML references.

> [!NOTE]
> **Accent Color**: The specific blue `#136dec` will be used as the Primary Accent in **BOTH** Light and Dark modes to maintain brand identity.

## Proposed Changes

### 1. Structure & Organization
We will organize styles to prevent "God Classes" (huge files).
- `SelectML.Client/Styles/Colors.xaml` (Optional: Shared colors if any)
- `SelectML.Client/Themes/Dark.xaml` (Semantic Brushes for Dark Mode)
- `SelectML.Client/Themes/Light.xaml` (Semantic Brushes for Light Mode)
- `SelectML.Client/Styles/Controls.xaml` (Templates for Button, TextBox, etc.)
- `SelectML.Client/Styles/DataGrid.xaml` (Specific complex styles)

### 2. Design System Tokens (Themes)
**Semantic Keys to be introduced:**
- `Brush.Background.Base`
- `Brush.Surface.Card`
- `Brush.Border.Base`
- `Brush.Border.Focus`
- `Brush.Text.Primary`
- `Brush.Text.Secondary`
- `Brush.Action.Primary` (The Blue #136dec)
- `Brush.Action.Primary.Foreground` (White)
- `Brush.Status.Success` (#22c55e)
- `Brush.Status.Error` (Red)

#### [MODIFY] [Dark.xaml](file:///c:/Aplicativos/Antigravity/SelectML/SelectML.Client/Themes/Dark.xaml)
- Update values to match ConnectML reference:
    - Background: `#101822`
    - Surface: `#1d2835`
    - Border: `#2d3b4e`
    - Text: `#FFFFFF` / `#94a3b8`

#### [MODIFY] [Light.xaml](file:///c:/Aplicativos/Antigravity/SelectML/SelectML.Client/Themes/Light.xaml)
- Define equivalent "Clean" palette:
    - Background: `#F3F4F6`
    - Surface: `#FFFFFF`
    - Border: `#E5E7EB`
    - Text: `#111827`

### 3. Control Templates
#### [NEW] [Styles/Controls.xaml](file:///c:/Aplicativos/Antigravity/SelectML/SelectML.Client/Styles/Controls.xaml)
- **Button**:
    - `Style="PrimaryButton"`: Blue bg, Radius 6, Shadow.
    - `Style="GhostButton"`: Transparent, Border, Text Color (for Cancel).
- **Inputs** (TextBox, PasswordBox, ComboBox):
    - Height ~35px.
    - CornerRadius 6.
    - Border thickness 1 (Normal) -> Focus Color (Blue).
    - Padding for comfort.

#### [NEW] [Styles/DataGrid.xaml](file:///c:/Aplicativos/Antigravity/SelectML/SelectML.Client/Styles/DataGrid.xaml)
- **Header**: Transparent or Surface background, Bold text, no grid separators in header.
- **Row**: Increased padding (approx 8-10px), rounded selection if possible or simple coloring.
- **Lines**: Subtle horizontal lines only.

### 4. MainWindow Refactor
#### [MODIFY] [MainWindow.xaml](file:///c:/Aplicativos/Antigravity/SelectML/SelectML.Client/MainWindow.xaml)
- Replace `StackPanel` grouping with `Border` (Card style):
    - Background: `{DynamicResource Brush.Surface.Card}`
    - CornerRadius: `6`
    - Effect: `DropShadowEffect` (BlurRadius 10, Opacity 0.1)
- Update all `Foreground="{DynamicResource ...}"` to use new Semantic keys.
- Apply new `Style="{StaticResource PrimaryButton}"` to main actions.

### 5. App.xaml
#### [MODIFY] [App.xaml](file:///c:/Aplicativos/Antigravity/SelectML/SelectML.Client/App.xaml)
- Merge `Styles/Controls.xaml` and `Styles/DataGrid.xaml` into `Application.Resources` so they are globally available.

## Verification Plan

### Manual Verification
1. **Theme Switching**:
    - Launch app. Toggle Sun/Moon icon.
    - Verify all colors invert correctly.
    - Check that "Primary Blue" remains Blue.
2. **Visual Check**:
    - Verify Rounded Corners on Buttons and Inputs (Radius 6).
    - Verify DataGrid looks "Web-like" (Spacing, Fonts).
    - Verify Focus states (Blue border on inputs).
3. **Resizing**:
    - Resize window, ensure "Cards" flow or stretch correctly.
