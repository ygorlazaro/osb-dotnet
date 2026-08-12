CLS
SCREEN 12
DO WHILE INKEY$ = ""

c = INT(RND * 6) + 1

FOR rot = 0 TO 200 STEP 1

CIRCLE (320, 240), rot, 0
CIRCLE (320, 40 + rot), rot, c + 1
CIRCLE (320, 440 - rot), rot, c + 2
CIRCLE (120 + rot, 240), rot, c + 3
CIRCLE (520 - rot, 240), rot, c + 4
CIRCLE (320 - (20000 ^ .5) + (.5 ^ .5) * (rot), 240 + (20000 ^ .5) - (.5 ^ .5) * (rot)), rot, c + 5
CIRCLE (320 + (20000 ^ .5) - (.5 ^ .5) * (rot), 240 + (20000 ^ .5) - (.5 ^ .5) * (rot)), rot, c + 6
CIRCLE (320 - (20000 ^ .5) + (.5 ^ .5) * (rot), 240 - (20000 ^ .5) + (.5 ^ .5) * (rot)), rot, c + 7
CIRCLE (320 + (20000 ^ .5) - (.5 ^ .5) * (rot), 240 - (20000 ^ .5) + (.5 ^ .5) * (rot)), rot, c + 8
REM LINE (rot, 0)-(rot, 480), 0
REM LINE (640 - rot, 0)-(640 - rot, 480), 0
REM LINE (0, rot)-(640, rot), 0
REM LINE (0, 480 - rot)-(640, 480 - rot), 0
NEXT rot
LOOP


