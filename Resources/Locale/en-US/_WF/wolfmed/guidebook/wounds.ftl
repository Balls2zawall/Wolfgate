# WOLFGATE: reworded from ONYX Resources/Locale/en-US/_Onyx/guidebook/wounds.ftl for what Wolfgate actually
# shipped (P4-D15) - organic species only (D3), guns and lasers can finish an amputation (SS14-answers §8.6-1),
# fracture multipliers stated qualitatively since §8.2-1 corrected the shipped numbers, and every IPC/cybernetic/
# slime/plant paragraph dropped. BodyPartDamage's three paragraphs are folded in rather than shipped as a
# fourth guide page (its XML is outside the ONYX sparse checkout).
guide-entry-wounds = Wounds
guide-entry-wound-treatment = Wound treatment

guidebook-wolfmed-wounds-content =
    # Wounds
    Blunt, slash, piercing, thermal, and electrical damage affects individual body parts. Suffocation, radiation, and other general conditions affect the whole body.
    A sufficiently strong individual hit creates a wound. Weak hits usually deal damage only. Later hits worsen an existing wound.
    An [bold]open[/bold] wound has its full effects. A [bold]stabilized[/bold] wound is treated but not healed. A [bold]closed[/bold] wound is physically sealed. Another strong hit can reopen it.

guidebook-wolfmed-wounds-examination =
    ## Examination
    The health analyzer lists damage, wounds, pain, bleeding, fractures, and organ condition for each part. Select an area on the targeting doll before treatment. Without a selection, treatment finds the most damaged compatible part.
    Body-part damage counts toward overall health. A wound is a separate result of a strong hit: a cut, burn, electrical injury, fracture, or structural defect. Damage can be removed while a wound and its effects remain.

guidebook-wolfmed-wounds-effects =
    ## Effects
    Wounds progress through minor, moderate, severe, and critical stages. Pain, bleeding, and loss of limb function increase with severity.
    Internal bleeding leaves no puddles and is revealed by an analyzer. A fracture is separate from blunt damage and needs separate treatment. Damaged organs also require their own treatment.

guidebook-wolfmed-wounds-traumatic-heading =
    ## Common trauma

guidebook-wolfmed-wounds-blunt =
    ### Blunt wound
    The result of a strong blunt hit. It causes pain and may bleed slightly; severe stages impair the part, while critical stages disable it.

guidebook-wolfmed-wounds-slash =
    ### Slash wound
    An open wound caused by slash damage. It bleeds more reliably than a blunt wound, causes pain, and compromises part function at severe stages.

guidebook-wolfmed-wounds-piercing =
    ### Piercing wound
    A deep puncture. It is usually more painful and bleeds more heavily than a slash wound; severe punctures impair or disable the part.

guidebook-wolfmed-wounds-burn =
    ### Burn wound
    The result of heat or caustic exposure, including a laser or plasma weapon. Its main danger is increasing pain, and it may scar suitable tissue.

guidebook-wolfmed-wounds-electrical =
    ### Electrical wound
    A lasting consequence of a strong electrical discharge. It causes one-time pain; severe stages impair the part and critical stages disable it. It is treated separately from an ordinary burn.

guidebook-wolfmed-wounds-special-heading =
    ## Special wounds

guidebook-wolfmed-wounds-fracture =
    ### Fracture
    Separate bone damage ranging from a hairline crack to a comminuted fracture. It slows movement and hand work, more so the worse the break. Reduction with a bonesetter weakens the penalty; mending with bone gel removes it entirely.

guidebook-wolfmed-wounds-incision =
    ### Surgical incision
    An intentional open wound used to access deep procedures. It bleeds, especially while the patient is awake, and may scar even when closed cleanly. It must be clamped and closed after work is complete.

guidebook-wolfmed-wounds-dismemberment-wound =
    ### Dismemberment wound
    A bleeding injury created when a part is lost. It represents the fresh severed surface and requires bleeding control, most quickly with a tourniquet.

guidebook-wolfmed-wounds-amputation-consequence =
    ### Amputation consequence
    The state of an untreated stump. Ordinary damage healing does not remove it, and a stump left untreated hides the surgeries that would attach a new part to it - a torso stump hides the surgeries for the head, both arms, both legs, and hands; an arm or leg stump hides only that limb's hand or foot. A separate surgical procedure clears the stump before attachment becomes possible again.

guidebook-wolfmed-wounds-internal-bleeding =
    ### Internal bleeding
    Blood loss into the body after destruction of a suitable organ. It produces no external puddle, appears on an analyzer, and is removed by a separate operation.

guidebook-wolfmed-wounds-medical-scar =
    ### Medical scar
    A record of completed treatment. It deals no damage and causes no functional penalty by itself; it marks prior trauma on tissue capable of scarring, including a surgical incision closed without complication.

guidebook-wolfmed-wounds-dismemberment =
    ## Dismemberment
    Dismemberment has two phases. Accumulated structural damage first leaves a limb ready to sever. A follow-up hit - blunt, slash, piercing, or a sufficiently strong laser - completes the amputation; a firearm or beam weapon can finish a limb just as a blade can. Repairing below the dangerous level removes this state.
    A missing part loses all of its functions. Its stump needs separate treatment; reducing overall damage does not replace that procedure.

guidebook-wolfmed-wound-treatment-content =
    # Wound treatment
    1. Restore breathing and stabilize critical condition.
    2. Stop major external bleeding - a tourniquet buys time on a limb that will not stop bleeding on its own.
    3. Scan the patient and select the damaged part.
    4. Remove damage with treatment matching its type.
    5. Scan again: remaining wounds, fractures, and organ damage need separate treatment.

guidebook-wolfmed-wound-treatment-biological =
    ## Biological tissue
    { "[" }bold]Bruise packs and ointment:[/bold] remove damage, not wounds.
    { "[" }bold]Gauze:[/bold] reduces external bleeding.
    { "[" }bold]Tourniquet:[/bold] stops bleeding on one limb entirely while applied, at the cost of that limb's use.
    { "[" }bold]Medicated sutures and regenerative mesh:[/bold] treat damage and minor wounds.
    { "[" }bold]Moderate wound:[/bold] external surgical treatment.
    { "[" }bold]Severe wound:[/bold] treatment through an open incision.

guidebook-wolfmed-wound-treatment-chemistry =
    ## Medicine
    Healing reagents distribute their effect among damaged biological parts. Damage and wounds remain separate: medicine that removes damage does not necessarily close a wound.
    A ladder of painkillers suppresses pain by increasing amounts: ibuprofen, ketorolac, tramadol, and oxycodone, from mildest to strongest. Cognac, bicaridine, and desoxyephedrine also carry a lesser pain-suppressing effect alongside their other uses.

guidebook-wolfmed-wound-treatment-fractures =
    ## Fractures
    A fracture is mended in two steps: a bonesetter reduces it, then bone gel mends it, both through an open incision.
