# Squeezes a recording's pitch contour towards one note, which is what makes a voice read as a
# deadpan intercom announcer. Factor 1 leaves it alone, 0 is a pure monotone. Target 0 keeps the
# speaker's own mean pitch.
#
#     Praat.exe --FULL-TRUST --run flatten.praat in.wav out.wav 0.25 0

form Flatten
    sentence Input
    sentence Output
    real Factor 0.25
    real Target 0
endform

sound = Read from file: input$
manipulation = To Manipulation: 0.01, 75, 500
tier = Extract pitch tier
mean = Get mean (points): 0, 0

if target = 0
    target = mean
endif

Formula: "target + (self - mean) * factor"

selectObject: manipulation, tier
Replace pitch tier

selectObject: manipulation
flat = Get resynthesis (overlap-add)
Save as WAV file: output$
