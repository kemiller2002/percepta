# Blinded control lane instructions

This directory is a blinded control condition for EX-PERCEPTA-2026-0002.

For this lane only, the experiment protocol overrides the repository instruction to inspect a Percepta screen contract before UI implementation.

You MUST:
- use only ../frozen-product-brief.md as the product/UI specification;
- write only index.html and NOTES.md in this lane;
- implement the neutral evaluator hooks in the frozen brief faithfully;
- make your own composition and interaction decisions.

You MUST NOT inspect or use:
- .percepta/**
- docs/CONTRACT-MODEL.md
- docs/VERIFICATION-MODEL.md
- examples/indy-init-investigation-workspace.yaml
- treatment-* lane content
- control sibling output
- prior experiment result artifacts
- generated Percepta implementation guidance

Do not run Percepta verification yourself. The evaluator runs it after your first implementation is frozen.

Do not alter these instructions.
