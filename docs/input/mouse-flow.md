# Mouse and pointer flow

All positions are in window coordinates. The scene passes them to the `PointerRouter`, which turns them into Control-local coordinates before it calls a Control.

## Move

```mermaid
sequenceDiagram
  participant SDL
  participant Win as SdlWindow
  participant Scene as WindowScene
  participant Router as PointerRouter
  participant Tip as TooltipPresenter
  SDL->>Win: MOUSE_MOTION
  Win->>Scene: PointerMoved(x, y)
  Scene->>Router: Move(x, y)
  Router->>Router: find Control under the pointer (overlays first)
  Router-->>Router: update hover on it and its ancestors
  Router-->>Router: a held handler gets PointerDrag, else the handler under the pointer gets PointerMove and the old one PointerLeave
  Scene->>Tip: Hover(control, x, y, now)
  Scene-->>Win: changed?
```

- **Hover** is a Pseudo-state on the Control under the pointer and on every ancestor.
- **A drag** is a move while a press is held: the handler that got the press keeps the pointer until release, even if the pointer leaves it.
- **Tooltips** start a delay timer; the scene's `Tick()` shows them later.
- `WINDOW_MOUSE_LEAVE` calls `PointerLeft()`, which clears hover and dismisses tooltips.

## Press (primary button)

```mermaid
flowchart TD
  A[PointerPressed x, y, modifiers] --> B{Press on an overlay backdrop?}
  B -- yes --> C[Close the top overlay, or toggle the open menu bar title]
  B -- no --> D[Find the Button under the pointer and mark it pressed]
  D --> E[Find the innermost pointer handler and call PointerDown]
  E --> F{A modal overlay is shown?}
  F -- no --> G[FocusManager moves focus to the nearest focusable Control]
  F -- yes --> H[Keyboard stays where it was]
```

- A **pointer handler** is a Control that implements `IPointerHandler` (sliders, list views, text boxes, scroll bars and so on). It receives `PointerDown`, then `PointerDrag` while held, then `PointerUp`.
- A **Button** is tracked separately: it shows the `pressed` Pseudo-state while held.

## Release

`PointerReleased` calls `PointerUp` on the handler that got the press, then on the Button. The Button raises `Click` only if the pointer is still over **the same Button** it went down on.

```mermaid
sequenceDiagram
  participant Router as PointerRouter
  participant H as Pointer handler
  participant B as Button
  Router->>H: PointerUp (even if the pointer moved away)
  Router->>B: PointerUp(isStillOver)
  B-->>B: raise Click only when still over
```

## Right button

`SecondaryPressed` finds the nearest ancestor of the hovered Control with a `ContextMenu` and shows it at the pointer. Like a primary press, a press outside the top overlay only closes it.

## Mouse wheel

`Wheel` dismisses tooltips, then walks up from the Control under the pointer to the first `IScrollable` that actually scrolls. If none scrolls, the wheel is ignored.

## Where to look in the code

- `src/Silo.Controls/PointerRouter.cs` – hover, press, drag, release, wheel
- `src/Silo.Controls/IPointerHandler.cs` and `PointerEvent.cs` – what a Control implements and receives
- `src/Silo.Controls/WindowScene.cs` – the entry points the platform window calls
- `src/Silo.Platform.Sdl/SdlWindow.cs` – the SDL event switch
