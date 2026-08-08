# Screenshots

| File | Shows |
| --- | --- |
| `text-light.png` | Text workspace, light theme, with a result in the output card |
| `text-dark.png` | Text workspace, dark theme |
| `files-light.png` | Files workspace, light theme, mid-operation with the progress bar active |
| `password-dark.png` | Password strength meter at "Strong" plus a success toast, dark theme |

## Capture guide

Consistency matters more than artistry. Same size, same content, same window state across all four, so
the README table does not jump around.

**Setup**

1. Build and run Release, not Debug — the title bar shows the version and you do not want `-dev` in a
   README image.
2. Display scaling **100%**, then resize the window to **1280 × 800**. That is a 16:10 ratio that scales
   down cleanly to GitHub's rendered width.
3. Use plausible but obviously fake content. Suggested input text:
   `Meet me at the usual place. Bring the documents.`
4. Password: type something that lands on **Strong** so the meter is showing its interesting state, but
   **never** use a real password, since the strength bar's fill width leaks its length.
5. Turn off any desktop clutter that will appear in the shadow — no notification banners.

**Capture**

Use <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>PrintScreen</kbd>, which captures the focused window including
its rounded corners and drop shadow with a transparent background. <kbd>Alt</kbd>+<kbd>PrintScreen</kbd>
loses the shadow and gives you a hard rectangle.

Then, for each file:

```bash
# Trim excess transparent shadow, flatten onto the theme's canvas colour, cap the width
magick capture.png -trim +repage -bordercolor none -border 24 \
  -background "#F4F5F8" -flatten -resize 1280x -strip text-light.png

# Dark-theme shots use the dark canvas colour instead
magick capture.png -trim +repage -bordercolor none -border 24 \
  -background "#121418" -flatten -resize 1280x -strip text-dark.png
```

`-strip` removes EXIF, which on some capture tools includes your machine name.

**Before committing**

- Look at every image at 100% and confirm no real filename, username or path is visible. `C:\Users\yourname`
  in a file picker is the classic leak.
- Confirm the output box does not contain anything you would mind being public forever.
- Keep each file under about 400 KB. `oxipng -o4 --strip safe *.png` or `pngquant --quality 70-90` if not.
- Check the README renders correctly in both GitHub's light and dark themes. Flattening onto the theme
  canvas colour, as above, is what stops a dark screenshot from sitting on a white page looking like a hole.

## Social preview

GitHub's repository social card is 1280 × 640. Compose it separately rather than cropping a screenshot:
the logo from `assets/icons/cipherdesk.svg`, the wordmark, and one line of description on the accent
colour. Upload it under **Settings → General → Social preview**; it is not stored in the repository.
