# Generation record

The source queue requests 696 PNG assets for 60 units: 36 heroes with 14 assets each, and 24 residents with 8 assets each.

Requested files are stored in heroes/<unit>/ or residents/<unit>/. PROMPTS.md in each unit folder records the source prompt, canvas request, transparency and reference. manifest.json tracks each requested file. The gallery is refreshed periodically and is a snapshot until generation finishes.

Generator: Codex built-in image generator. Original generator outputs remain in the Codex generated_images directory; project assets are copies.

For the current batch, each source prompt is followed by:

Canvas requirement: <requested size>

For 1024x1024 requests the appended line additionally includes:

; SQUARE aspect ratio 1:1, recompose to fit the square.

The generator receives transparent_background from the source queue and, when supplied, the referenced_image_paths points to the named reference in the same unit folder. The generator may return native dimensions different from the requested dimensions. file-verification.json records actual dimensions and the top-left pixel alpha for files checked so far. Corner alpha is a partial check, not a complete transparency audit.

Corrections are retained as sibling -v2 images. See review-notes.md for recorded issues and corrections. A generated file is not automatically an accepted production asset; visual review and any outstanding corrections remain part of the work.
