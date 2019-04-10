GIT COMMIT ID
53e3600094ce0721163ad16d7ac98f15ae422678

Release 68 with prototype buzzer interops added --> R68A

KIWI PIN NUMBERING
Hold the Kiwi such that the screw terminals are on the top of the board, component side up.
This means the silk should be naturally legible (not upside down), the pot (knob) on the left, and buzzer on the right.
From this perspective, pin 1 is the left screw terminal and pin 12 is on the right.

The Kiwi must be connected to the dotNOW as follows:
J11-1 to Kiwi pin 12 (power)
J11-2 to Kiwi pin 9  (buzzer power enable)
J11-4 to Kiwi pin 1  (buzzer pwm)
J12-10 to Kiwi pin 11 (ground)

Kiwi pin 9 (buzzer power enable) can be substituted with any other GPIO.
This must be enabled (set high) prior to using the buzzer.
J11-2 is permanently high and so is the zero-config choice.

NPS 2019-04-03