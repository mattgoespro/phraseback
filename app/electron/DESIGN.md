# Phraseback Electron design checkpoint

The Review canvas is the primary working surface. Its image occupies the available space; playback and selected moments sit immediately beneath it. The header uses a lighter neutral grey than the canvas and carries a breadcrumb whose recording name opens the recording picker. A chevron sits inside that picker label.

The approved compact dark direction applies throughout: near-black canvas, restrained violet accent, charcoal elevated controls, minimal visible borders, no green glow, and no permanent dashboard of cards. The selected moment uses a quiet raised surface. The playback button centers its label. The Moments strip has no separate heading or curation toolbar; curation stays in contextual actions where needed. The Review header has no Edit moment button or reviewed-count label.

Library uses a lightweight list and recording picker. Capture setup focuses on display/region selection and a short explanation of the countdown. Recording uses a single progress surface and floating stop control. Settings and model management use conventional sections and selects. Export uses a destination dialog and an explicit progress/result state. Secondary controls live in the overflow or a collapsible details pane.

At 1280 × 800 the evidence, playback, and moments remain visible. At 1024 × 700, details collapse and menus keep their contents scrollable. Keyboard focus is visible without outlining every static surface. Native testing must inspect these sizes and the actual recording flow before this shell replaces Avalonia.
