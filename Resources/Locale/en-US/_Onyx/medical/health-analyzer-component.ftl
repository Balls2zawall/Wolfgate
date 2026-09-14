# Wound findings shown by the Wolfmed diagnostic panel. The four disease keys and the dead
# health-analyzer-wound-pain key from Onyx's file have no consumer in Wolfgate and are not ported.
health-analyzer-wound-part-summary = { $part }: { $details }
health-analyzer-wound-bleeding-short = external bleeding
health-analyzer-wound-internal-bleeding-short = internal bleeding
health-analyzer-wound-scars-short = scars: { $count }
health-analyzer-wound-pain-short = pain: { $pain }
health-analyzer-wound-functionality-impaired = reduced function
health-analyzer-wound-functionality-disabled = function lost
health-analyzer-wound-functionality-unavailable = part absent
health-analyzer-wound-clotting-inprogress = clotting in progress
health-analyzer-wound-clotting-complete = bleeding stopped
health-analyzer-wound-clotting-mixed = partial hemostasis

# WOLFGATE (P4-D25): Onyx carries the fracture grade and treatment in the payload and never renders them.
# The grade word itself comes from the fracture-grade-* keys the reagent guidebook already ships.
health-analyzer-wound-fracture-short = fracture: { $grade }
health-analyzer-wound-fracture-treated-short = fracture: { $grade } ({ $treatment })
health-analyzer-wound-fracture-treatment-reduced = reduced
health-analyzer-wound-fracture-treatment-mended = mended
