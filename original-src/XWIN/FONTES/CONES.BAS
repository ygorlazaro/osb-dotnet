SCREEN 12
DEFINT A-Z
DO WHILE INKEY$ = ""
speed = 1
RANDOMIZE TIMER

'Select plaid colors-for backdrop---
k1 = 4: k2 = 8: k3 = 0: cc = 31: aa = 480
b2 = 480: d2 = 480: i1 = 8: i2 = 8
GOSUB backdrop
'--------------
a = 50: B = 50: c = 25
d1 = 1: d2 = 1: d3 = 1: d4 = 1
k1 = 10: k2 = 13: k3 = 0
  FOR m = 1 TO 9463
    IF INKEY$ <> "" THEN END
    CIRCLE (a, B - 1), c, k1
    CIRCLE (a, B), c, k2
    CIRCLE (a, B + 1), c, k3
    a = a + d1: B = B + d2
    IF a > 620 THEN d1 = -1: k1 = INT(RND * 7) + 9
    IF a < 6 THEN d1 = 1: k3 = 0
    IF B > 480 THEN d2 = -1: k2 = INT(RND * 7) + 9
    IF B < 6 THEN d2 = 1: k1 = INT(RND * 7) + 9
    IF k2 = 11 THEN k1 = 15
    IF k1 = 11 THEN k1 = 12
    IF m > 4283 THEN
    c = c + d3
    'cone effect
    IF c > 50 THEN d3 = -1: vx = 2.5
    IF c < 1 THEN d3 = 1
    IF c < 10 THEN vx = 4
    END IF
      FOR delay = 1 TO speed
      NEXT delay
    NEXT m
    SLEEP 3
'clear the screen in white
k1 = 0
GOSUB clswipe
GOTO 1

clswipe:
'---black cls-----------------
o1 = 640: p1 = -1: o2 = -1: p2 = 480
f1 = 1: f2 = 1
FOR m = 1 TO 325
IF INKEY$ <> "" THEN END
LINE (o1, p1)-(o2, p2), k1, B
o1 = o1 - f1: o2 = o2 + f2
FOR zz = 1 TO 20 * spd
NEXT zz
NEXT m
PALETTE
RETURN

backdrop:
LINE (0, 0)-(640, aa), k1, BF
a1 = 4: b1 = 0: a2 = 636
c1 = 0: d1 = 0: c2 = 640
FOR m = 1 TO 40
LINE (a1, b1)-(a2, b2), k2, B
a1 = a1 + 8
a2 = a2 - 8
NEXT m
FOR m = 1 TO cc
LINE (c1, d1)-(c2, d2), k3, B
d1 = d1 + i1
d2 = d2 - i2
NEXT m
RETURN
1 LOOP

