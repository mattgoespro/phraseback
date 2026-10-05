---
name: desktop-frontend-design
description: Plan, critique, prototype, or implement desktop app UI/UX redesigns, including complete visual and interaction direction changes. Use when desktop layout, navigation, hierarchy, density, surfaces, or workflow needs design judgment. Do not use for isolated copy or asset edits.
---

# Desktop frontend design

Design the workspace around the user's primary task. Treat existing UI and supplied references as evidence, not as a template to reproduce or a design system to assume. Follow explicit product requirements and preserve established behavior unless the request changes it.

## Find the design problem

- Identify the primary object of work, the action users perform most often, and what they must see while doing it. Give that object the strongest visual priority and enough space to use it.
- Inspect the actual application state, target window sizes, and relevant reference images when available. A reference may specify palette, structure, density, behavior, or mood; identify which of these the user wants before translating it.
- Inventory persistent regions and controls. For each one, decide whether it belongs in the main workspace, a contextual pane, a menu, a collapsible region, or a separate view. Remove repeated labels and controls that compete with the primary task.
- For a directional overhaul, reconsider the information architecture and interaction flow before changing colors, shadows, or border radii. Do not preserve a crowded dashboard merely because it is already implemented.

## Choose a coherent direction

Write a brief internal design thesis: what should dominate, how users move through the workspace, and what makes this product recognizable. Compare at least one materially different layout when the current structure is the problem. Choose deliberately; do not default to identical cards, symmetric columns, or permanent panels.

Use hierarchy in this order: space and placement, type and scale, surface tone or elevation, then borders where a boundary still needs emphasis. A surface can use fill, shadow, contrast, or inset treatment. Reserve visible outlines for states or boundaries they explain. If many adjacent regions need borders to remain legible, revisit the grouping and layout.

Use familiar desktop and web controls where they improve the task: menus for infrequent actions, selects for compact choices, breadcrumbs for location, icons with clear labels or accessible names, and collapsible panels for secondary work. Do not add any control only to make the interface look modern. Keep essential actions discoverable without hover.

Choose typography, color, icons, and motion for this product and platform. One distinctive element may carry the visual identity; surrounding controls should support it. Use motion to show the result of an action, not as constant decoration. Respect the user's specified theme and reference constraints. Do not infer that a dark palette requires a bordered or dense layout.

## Make desktop behavior credible

- Check narrow and wide windows, display scaling, long names, empty content, and the most important editing or review state.
- Support keyboard focus, shortcuts where appropriate, native window behavior, and resizable or collapsible panes when they improve the workflow.
- Keep expensive work off the UI thread. Avoid changing data ownership, persistence, or platform behavior as part of a visual redesign unless requested.
- Distinguish an interactive concept from implemented product behavior. A mockup can demonstrate a flow without implying the production app has it.

## Critique before accepting

Inspect the rendered result at the target size and compare it with the user's reference and feedback. Test the primary interaction, not only the first screenshot. Ask:

1. Is the main object of work unmistakable within a few seconds?
2. Can users reach secondary actions without keeping all of them visible?
3. Are surfaces separated mostly by hierarchy rather than repeated boxes and rules?
4. Does each visible component earn its space and match the chosen direction?
5. Do interaction states, small windows, and keyboard use remain clear?

If the result still resembles a generic component kit, revise the layout or workflow before polishing details. Report what changed, how it was checked, and any gap between mockup and production behavior.
