DECLARE SUB Init ()
DECLARE SUB Vector ()
    
COMMON SHARED NumPoints, ConPts, AngleX, AngleY, AngleZ

       NumPoints = 8
       ConPts = 12

DIM SHARED Points(NumPoints, 3)
DIM SHARED Connect(ConPts, 3)
DIM SHARED Rotated(NumPoints, 3)
DIM SHARED Sinus(1 TO 720), CoSin(1 TO 720)

        Init

        DO WHILE INKEY$ = ""
                PCOPY 3, 2: SCREEN , , 2, 0
               
                WAIT &H3DA, 8
                WAIT &H3DA, 8, 8

                Vector

                PCOPY 2, 0: SCREEN , , 2, 0
        LOOP
        END



        DATA -50,-50,-50
        DATA  50,-50,-50
        DATA  50, 50,-50
        DATA -50, 50,-50
        DATA -50,-50, 50
        DATA  50,-50, 50
        DATA  50, 50, 50
        DATA -50, 50, 50
       
        
        DATA 5,1,8
        DATA 1,4,8
        DATA 6,5,7
        DATA 5,8,7
        DATA 2,6,3
        DATA 6,7,3
        DATA 1,2,4
        DATA 2,3,4
        DATA 4,3,8
        DATA 3,7,8
        DATA 5,6,1
        DATA 6,2,1

SUB Init
        SCREEN 7
             
        PI = 3.141592

        FOR X = 1 TO 720
                Sinus(X) = INT(SIN(PI * 2 / 512 * X) * 256)
                CoSin(X) = INT(COS(PI * 2 / 512 * X) * 256)
        NEXT X



        FOR N = 1 TO NumPoints
                READ X, Y, Z
                Points(N, 1) = X
                Points(N, 2) = Y
                Points(N, 3) = Z
        NEXT N

        FOR N = 1 TO ConPts
                READ A, B, C
                Connect(N, 1) = A
                Connect(N, 2) = B
                Connect(N, 3) = C
        NEXT N
       
END SUB

SUB Vector
    

     IF AngleX < 512 THEN AngleX = AngleX + 3 ELSE AngleX = 1
     IF AngleY < 512 THEN AngleY = AngleY + 5 ELSE AngleY = 1
     IF AngleZ < 512 THEN AngleZ = AngleZ + 4 ELSE AngleZ = 1
     FOR N = 1 TO NumPoints
                X = Points(N, 1)
                Y = Points(N, 2)
                Z = Points(N, 3)
   
                Ty = ((Y * CoSin(AngleX)) - (Z * Sinus(AngleX))) \ 256
                Tz = ((Y * Sinus(AngleX)) + (Z * CoSin(AngleX))) \ 256
                Tx = ((X * CoSin(AngleY)) - (Tz * Sinus(AngleY))) \ 256
                Tz = ((X * Sinus(AngleY)) + (Tz * CoSin(AngleY))) \ 256
                Ox = Tx
                Tx = ((Tx * CoSin(AngleZ)) - (Ty * Sinus(AngleZ))) \ 256
                Ty = ((Ox * Sinus(AngleZ)) + (Ty * CoSin(AngleZ))) \ 256
               
                Nx = ((128 * Tx) / (200 - Tz)) + 160
                Ny = (100 - (128 * Ty) / (200 - Tz))

                Rotated(N, 1) = Nx
                Rotated(N, 2) = Ny
                Rotated(N, 3) = Tz
        NEXT N

     FOR N = 1 TO ConPts
        X1 = Rotated(Connect(N, 1), 1)
        X2 = Rotated(Connect(N, 2), 1)
        X3 = Rotated(Connect(N, 3), 1)
        Y1 = Rotated(Connect(N, 1), 2)
        Y2 = Rotated(Connect(N, 2), 2)
        Y3 = Rotated(Connect(N, 3), 2)
        Z1 = Rotated(Connect(N, 1), 3)
        Z2 = Rotated(Connect(N, 2), 3)
        Z3 = Rotated(Connect(N, 3), 3)
       
        Visible = (X3 - X1) * (Y2 - Y1) - (X2 - X1) * (Y3 - Y1)
        
        IF Visible < -256 THEN
                LINE (X1, Y1)-(X2, Y2), N
                LINE (X2, Y2)-(X3, Y3), N
                LINE (X3, Y3)-(X1, Y1), N
      
                

                PAINT ((X1 + X2 + X3) \ 3, (Y1 + Y2 + Y3) \ 3), N
        END IF
       
        NEXT N

END SUB

