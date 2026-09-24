# Flat casual puzzle buttons

PNG sprites in this folder use flat fills, compact shapes and transparent backgrounds. The three label-button backgrounds are the same 4:3 size; the projectile button is a 1:1 circle.

| Sprite | Shape | Suggested use |
|---|---|---|
| `Button_Primary_Sunshine.png` | Rounded rectangle, sunny yellow | Main action, such as Retry |
| `Button_Secondary_Aqua.png` | Rounded rectangle, aqua | Secondary action |
| `Button_Accent_Lime.png` | Rounded rectangle, leaf green | Alternate action |
| `Button_Fire_Projectile.png` | Orange circle with white projectile icon | Fire/shoot control |

The first three sprites are blank. Put editable labels in a separate TextMesh Pro child. The fire sprite already has its icon; keep it square and scale the whole control uniformly.

## Unity import

- Texture Type: `Sprite (2D and UI)`; Sprite Mode: `Single`; Alpha Is Transparency: enabled.
- Keep label-button images at a 4:3 aspect ratio and the projectile button at 1:1.
- Add each sprite to a Unity `Button` image. `Color Tint` can provide highlighted, pressed and disabled feedback without extra state sprites.
- For the yellow, aqua and green sprites, use dark plum label text for contrast over the flat fills.

The previous outlined, elongated PNGs were replaced in place by these flat versions. No code or scene objects were changed.
