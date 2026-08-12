CLS
SCREEN 12
RANDOMIZE TIMER

        pal = INT(RND * 5) + 1
        IF pal = 2 THEN pal = 256
        IF pal = 3 THEN pal = 257
        IF pal = 4 THEN pal = 65536
        IF pal = 5 THEN pal = 65537
       
        FOR i = 1 TO 60 STEP 4
        x = x + 1
        PALETTE x, i * pal
        NEXT

        size = 23
        delay = 5000
        winX = 160
        winY = 240
        accel = .8
        maxsp = size / 2
        minsp = -maxsp

        DIM x(size), y(size)
        DIM xx(size), yy(size), nx(size), ny(size)
        DIM col(size)
        DIM r(size)
        DIM t(size), v(size), u(size)




        maxX = 640 - winX
        maxY = 480 - winY
        x = maxX
        y = maxY
        FOR i = 1 TO size
        xx(i) = minsp
        yy(i) = minsp
        x = x - 400 / size
        y = y - 400 / size
        x(i) = x
        y(i) = y
        r(i) = i
        col(i) = i
        NEXT

DO

        FOR i = 1 TO delay STEP 1: NEXT

FOR i = 1 TO size

        CIRCLE (t(i), v(i)), u(i), 0
        'CIRCLE (640 - t(i), 480 - v(i)), u(i), 0
        'CIRCLE (t(i), 480 - v(i)), u(i), 0
        'CIRCLE (640 - t(i), v(i)), u(i), 0

        CIRCLE (x(i), y(i)), r(i), col(i)
       
        'CIRCLE (640 - x(i), 480 - y(i)), r(i), col(i)
        'CIRCLE (x(i), 480 - y(i)), r(i), col(i)
        'CIRCLE (640 - x(i), y(i)), r(i), col(i)

IF i < size THEN

        LINE (x(i), y(i))-(x(i + 1), y(i + 1)), col(i)
        LINE (640 - x(i), 480 - y(i))-(640 - x(i + 1), 480 - y(i + 1)), col(i)
        LINE (640 - x(i), y(i))-(640 - x(i + 1), y(i + 1)), col(i)
        LINE (x(i), 480 - y(i))-(x(i + 1), 480 - y(i + 1)), col(i)


        'LINE (640 - x(i), y(i))-(640 - x(i + 1), 480 - y(i + 1)), col(i)
        'LINE (x(i), y(i))-(640 - x(i + 1), 480 - y(i + 1)), col(i)
        'LINE (x(i), y(i))-(x(i + 1), 480 - y(i + 1)), col(i)
        'LINE (x(i), 480 - y(i))-(640 - x(i + 1), y(i + 1)), col(i)

END IF

        t(i) = x(i): v(i) = y(i): u(i) = r(i)

NEXT

FOR i = 1 TO size
IF i = size THEN

                xx(i) = xx(i) + nx(i)
                yy(i) = yy(i) + ny(i)
                IF xx(i) > maxsp THEN xx(i) = maxsp
                IF xx(i) < minsp THEN xx(i) = minsp
                IF yy(i) > maxsp THEN yy(i) = maxsp
                IF yy(i) < minsp THEN yy(i) = minsp

                x(i) = x(i) + xx(i)
                y(i) = y(i) + yy(i)
                IF x(i) > maxX THEN nx(i) = -RND * accel - accel
                IF x(i) < winX THEN nx(i) = RND * accel + accel
                IF y(i) > maxY THEN ny(i) = -RND * accel - accel
                IF y(i) < winY THEN ny(i) = RND * accel + accel
END IF

        IF i < size THEN
                x(i) = x(i + 1)
                y(i) = y(i + 1)
        END IF

NEXT

LOOP UNTIL INKEY$ <> ""

