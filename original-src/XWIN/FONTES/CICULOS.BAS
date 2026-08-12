CLS
SCREEN 12
DO WHILE INKEY$ = ""
1 RANDOMIZE TIMER
a = INT(RND * 15) + 1
b = INT(RND * 15) + 1
FOR rot = 0 TO 62.83 STEP .01
CIRCLE (320 + 100 * COS(rot), 240 + 100 * SIN(rot)), rot * 3, a
CIRCLE (320 + 100 * SIN(rot), 240 + 100 * COS(rot)), (rot * 3), b

NEXT rot
rot = 0
GOTO 1
LOOP
END


