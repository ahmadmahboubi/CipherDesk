# Icons and visual identity

| File | Purpose |
| --- | --- |
| `cipherdesk.svg` | Full-colour mark on the brand plate. Source of truth for every other file here. |
| `cipherdesk-mono.svg` | Single-colour mark, no plate. Inherits `currentColor`, so it works on any background. |
| `cipherdesk.ico` | Windows application icon. Multi-resolution: 16, 20, 24, 32, 40, 48, 64, 128, 256. |
| `cipherdesk-256.png` `-512` `-1024` | Raster exports for README headers, store listings and social cards. |

## Regenerating the raster files

```bash
# ImageMagick, cross platform
magick -background none cipherdesk.svg \
  -define icon:auto-resize=256,128,64,48,40,32,24,20,16 cipherdesk.ico

magick -background none -density 600 cipherdesk.svg -resize 512x512 cipherdesk-512.png
```

Inkscape gives crisper small sizes if you care:

```bash
for s in 16 20 24 32 40 48 64 128 256; do
  inkscape cipherdesk.svg -w $s -h $s -o "icon-$s.png"
done
magick icon-*.png cipherdesk.ico && rm icon-*.png
```

The build treats the icon as optional. `CipherDesk.App.csproj` guards both the `ApplicationIcon`
property and the content copy with `Condition="Exists(...)"`, so the project still compiles if the
file is absent - it just falls back to the default WinForms icon.

## Design notes

The mark is a **keyhole resting on a desk rule**. Two decisions drive it:

- **No interior detail.** The whole mark is four solid white shapes with no strokes and no negative
  space narrower than about 6% of the artboard. Thin strokes and interior cutouts are the usual reason
  application icons turn to mush in a 16px taskbar slot, and this one is drawn to survive that.
- **A tapered stem, not a rectangle.** The keyhole widens toward its base like a real warded lock. At
  large sizes that reads as craft; at small sizes it makes the silhouette more distinctive than the
  circle-on-a-stick used by most encryption tools.

The horizontal rule underneath does double duty: it grounds the composition, and it is the *Desk* half
of the name.

## Palette

| Role | Light | Dark |
| --- | --- | --- |
| Accent | `#6D5AE6` | `#8B7CF6` |
| Accent (pressed) | `#5A47D4` | `#7A6AF0` |
| Success | `#0E9F6E` | `#2FC48D` |
| Warning | `#D98E04` | `#F5B13D` |
| Danger | `#D9353B` | `#F06267` |
| Canvas | `#F4F5F8` | `#121418` |
| Surface | `#FFFFFF` | `#1B1E25` |
| Text primary | `#14161A` | `#F2F3F7` |
| Text secondary | `#5C6270` | `#9AA1B1` |

`src/CipherDesk.App/Theming/ThemePalette.cs` is the single source of truth for these values in code.
If you change one, change both.

## Typography

The application resolves fonts at runtime through a fallback chain, so it degrades gracefully on older
Windows installs rather than silently substituting something ugly:

| Role | Chain |
| --- | --- |
| Display | Segoe UI Variable Display -> Segoe UI Semibold -> Segoe UI |
| Body | Segoe UI Variable Text -> Segoe UI |
| Monospace | Cascadia Mono -> Consolas -> Courier New |
| Icons | Segoe Fluent Icons -> Segoe MDL2 Assets -> Segoe UI Symbol |

See `src/CipherDesk.App/Theming/Typography.cs`.
