# IMPLEMENTATION PLAN - PHASE 3: Background Operation & Conditional Automation

## Overview
Phase 3 focuses on minimizing user friction by enabling **Background Operation** via System Tray integration and introducing **Conditional Automation**. The goal is for the application to remain unobtrusive during normal operation ("Monitoring") and only request operator attention when necessary or when configured to do so.

## 1. System Tray Integration

### Requirement
The application must reside in the Windows System Tray (Notification Area) when minimized, removing its presence from the Taskbar to save space. It must restore to the screen upon double-clicking the tray icon or when a specific event ("Wake-on-Event") occurs.

### Technical Strategy

#### 1.1 Library Selection
We will use the **Hardcodet.NotifyIcon.Wpf** library (available via NuGet).
*   **Reasoning**: It provides a rich WPF-friendly wrapper around the Win32 NotifyIcon API, supporting Data Binding (Command, ToolTip, IconSource) and standard WPF features (Styles, Triggers) which simplifies MVVM integration compared to using `System.Windows.Forms.NotifyIcon` directly.

#### 1.2 Window & Taskbar Behavior
Logic will be implemented to handle the transitions between "Normal" and "Minimized/Tray" states.

*   **MainWindow.xaml**:
    *   Add `tb:TaskbarIcon` control.
    *   Bind `IconSource` to a property in ViewModel (for dynamic icons).
    *   Bind `DoubleClickCommand` to a command in ViewModel (e.g., `RestoreWindowCommand`).
*   **MainWindow.xaml.cs (Code Behind)**:
    *   Intercept `StateChanged` event.
    *   **Logic**:
        ```csharp
        if (WindowState == WindowState.Minimized)
        {
            ShowInTaskbar = false;
        }
        else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
        {
            ShowInTaskbar = true;
        }
        ```
    *   *Note*: While MVVM is preferred, WindowState management often requires View-layer handling. Alternatively, an `IWindowManager` service can be used if strict decoupling is desired, but Code Behind is acceptable for pure view-state logic.

#### 1.3 Wake-on-Event (Automatic Restoration)
When a new file is detected and manual intervention is required (or if Auto-Mode is disabled), the application must bring itself to the foreground.

*   **Mechanism**:
    *   Inside `MainViewModel.OnFileCreated`:
    *   If user attention is needed, trigger an event or call a service method (e.g., `_windowService.RestoreAndActivate()`).
    *   **Implementation Detail**: The View subscribes to this notification or the Service manipulates the `Application.Current.MainWindow`.
    *   **Action**: Set `WindowState = Normal`, `ShowInTaskbar = true`, and call `Activate()` and `Topmost = true` (briefly) to ensure visibility.

## 2. Conditional Automatic Mode

### UI Changes
*   Add a **CheckBox** labeled "Processamento Automático" (Automatic Processing) in the Main Window, adjacent to the "Send" and "Cancel" buttons.
*   Bind to `IsAutoMode` (boolean) in `MainViewModel`.

### Business Logic (ViewModel)
Modify the `OnFileCreated` workflow in `MainViewModel`.

1.  **Parse File**: Parse the incoming file into `MeasurementData`.
2.  **Check Auto-Mode**:
    *   **IF** `IsAutoMode == true`:
        *   **Validation**: Check if `MeasurementData` is valid (PartName is not empty, Batch is not empty, Characteristics exist, Values exist).
        *   **Success Path**:
            *   Execute the "Save/Send" logic immediately (bypassing the 'Pending Action' state).
            *   Do **not** block the monitoring loop.
            *   Trigger **Discrete Notification** (see Section 3).
        *   **Failure/Fallback Path**:
            *   If validation fails (e.g., missing Batch Number), **abort** auto-save.
            *   Proceed to standard "Human-in-the-Loop" workflow: Pause monitoring, show data in UI, enter 'Pending Action' state, and **Restore Window** (Wake-on-Event) to prompt user.
    *   **IF** `IsAutoMode == false`:
        *   Proceed to standard "Human-in-the-Loop" workflow.

## 3. Discrete Notifications (Balloon Tips)

### Scenario
Display notifications only when the application is minimized to the Tray AND running in Automatic Mode.

### Implementation
*   **Library Feature**: Use `TaskbarIcon.ShowBalloonTip(title, message, icon)` method from Hardcodet library.
*   **Trigger**: In the "Success Path" of Auto-Processing.
*   **Content**: "Arquivo [FileName] processado com sucesso."
*   **Configuration**: Set timeout to 3-4 seconds.

## 4. Dynamic Tray Icon (Visual Feedback)

### Requirement
The Tray Icon must provide visual feedback indicating the application status, specifically a "pulsing" green effect when "Online" (Monitoring).

### Technical Challenge
`.ico` files do not support native animations via WPF Storyboards in the Tray.

### Solution: Timer-Based Icon Swapping
*   **Assets**: Prepare two distinct icons (or generate them in memory):
    1.  `Icon_Green_Bright.ico`
    2.  `Icon_Green_Dark.ico` (or standard)
    3.  `Icon_Gray.ico` (Offline/Idle)
*   **ViewModel Logic**:
    *   Property `TrayIconSource` (ImageSource).
    *   `System.Timers.Timer` or `DispatcherTimer` running when `IsMonitoring == true`.
    *   **Tick Event**: Toggle `TrayIconSource` between Bright and Dark images every 500ms-1000ms.
    *   **On Stop Monitoring**: Set `TrayIconSource` to `Icon_Gray.ico` and stop timer.

## Execution Checklist

### Dependencies
- [ ] Add NuGet package `Hardcodet.NotifyIcon.Wpf` to `SelectML.Client`.

### Assets
- [ ] Create or acquire icons for: Green (Bright), Green (Dark), Gray (Offline).

### Code Implementation
- [ ] **MainWindow.xaml**:
    - [ ] Integrate `tb:TaskbarIcon` resource.
    - [ ] Bind Icon, ToolTip, and DoubleClickCommand.
- [ ] **MainWindow.xaml.cs**:
    - [ ] Implement `StateChanged` handler for Minimize/Restore logic.
- [ ] **MainViewModel.cs**:
    - [ ] Add `IsAutoMode` property.
    - [ ] Add `TrayIconSource` property.
    - [ ] Add `RestoreWindowCommand`.
    - [ ] Implement `IconAnimationTimer` for the pulsing effect.
    - [ ] Refactor `OnFileCreated` to include the Conditional Auto-Mode logic branch.
    - [ ] Implement validation logic for `MeasurementData`.
- [ ] **WindowService / Interaction**:
    - [ ] Define mechanism for ViewModel to trigger "ShowBalloonTip" and "RestoreWindow" on the View (e.g., via `Action` delegates or an Interface).

## Success Criteria
1.  Application successfully hides to Tray when minimized.
2.  Double-clicking Tray icon restores the window.
3.  "Automatic Mode" processes valid files without user intervention and notifies via Balloon Tip.
4.  Invalid files in "Automatic Mode" correctly interrupt the process and bring the window to front.
5.  Tray Icon pulses green when monitoring and is static gray when stopped.
